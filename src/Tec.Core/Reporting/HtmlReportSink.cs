using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using Tec.Core.Abstractions;
using Tec.Core.Localization;
using Tec.Core.Model;

namespace Tec.Core.Reporting;

/// <summary>
/// HTML report (<c>reporte.html</c>) with the evidence in order. It is rewritten with each
/// piece of evidence so the report exists even if the session does not close cleanly.
/// </summary>
[SuppressMessage("Reliability", "CA1001", Justification = "SemaphoreSlim does not use AvailableWaitHandle, so it holds no native resources.")]
public sealed class HtmlReportSink(
    IEvidenceFileNamer namer,
    IClock clock,
    TecLanguage language,
    Func<IReadOnlyList<string>>? lostApplications = null) : IEvidenceSink
{
    public const string FileName = "reporte.html";

    private sealed record Entry(int Sequence, string Image, InteractionCaption Caption, string Color);

    private readonly List<Entry> _entries = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Texts _texts = new(CoreResources.Report, language.Culture);

    public Task OnSessionStartedAsync(EvidenceSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(session.OutputDirectory);
        return WriteReportAsync(session, finishedAt: null, cancellationToken);
    }

    public async Task WriteAsync(EvidenceSession session, Evidence evidence, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _entries.Add(new Entry(evidence.Sequence, namer.GetImageFileName(evidence), evidence.Caption, evidence.HighlightColor.ToHex()));
        }
        finally
        {
            _gate.Release();
        }
        await WriteReportAsync(session, finishedAt: null, cancellationToken).ConfigureAwait(false);
    }

    public Task OnSessionCompletedAsync(EvidenceSession session, CancellationToken cancellationToken) =>
        WriteReportAsync(session, clock.Now, cancellationToken);

    private async Task WriteReportAsync(EvidenceSession session, DateTimeOffset? finishedAt, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lost = lostApplications?.Invoke() ?? [];
            var html = Render(session, finishedAt, [.. _entries.OrderBy(e => e.Sequence)], language, _texts, lost);
            await File.WriteAllTextAsync(Path.Combine(session.OutputDirectory, FileName), html, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Render(
        EvidenceSession session,
        DateTimeOffset? finishedAt,
        IReadOnlyList<Entry> entries,
        TecLanguage language,
        Texts texts,
        IReadOnlyList<string> lostApplications)
    {
        static string E(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
        static string D(DateTimeOffset d) => d.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.Append("<html lang=\"").Append(language.Code).AppendLine("\">");
        sb.AppendLine("<head>");
        sb.Append("<title>").Append(E(texts.Get("Title"))).AppendLine("</title>");
        sb.Append("""
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              body { font-family: "Segoe UI", sans-serif; margin: 0; background: #f4f4f5; color: #1f1f23; }
              header { background: #1e1e1e; color: #fff; padding: 16px 24px; }
              header h1 { margin: 0 0 6px; font-size: 20px; }
              header p { margin: 2px 0; color: #c8c8c8; font-size: 13px; }
              header p.lost { color: #ffb4b4; }
              main { padding: 16px 24px; max-width: 1400px; }
              nav ol { columns: 2; font-size: 13px; }
              article { background: #fff; border-radius: 6px; padding: 12px 16px; margin: 16px 0; box-shadow: 0 1px 3px rgba(0,0,0,.12); }
              article h2 { margin: 0 0 4px; font-size: 16px; border-left: 6px solid var(--c); padding-left: 8px; }
              article p { margin: 0 0 10px; color: #555; font-size: 13px; }
              article img { max-width: 100%; border: 1px solid #ddd; }
            </style>
            </head>
            <body>
            """);
        sb.Append("<header><h1>").Append(E(texts.Get("Title"))).Append(" — ").Append(E(session.TargetDescription)).Append("</h1>");
        sb.Append("<p>").Append(E(texts.Get("Started"))).Append(": ").Append(D(session.StartedAt));
        sb.Append(" · ").Append(E(finishedAt is { } f ? $"{texts.Get("Finished")}: {D(f)}" : texts.Get("InProgress")));
        sb.Append(" · ").Append(E(texts.Get("EvidenceCount"))).Append(": ").Append(entries.Count).Append("</p>");
        sb.Append("<p>").Append(E(texts.Get("Session"))).Append(": ").Append(E(session.Id)).Append("</p>");
        if (lostApplications.Count > 0)
            sb.Append("<p class=\"lost\">").Append(E(texts.Get("ApplicationsLost"))).Append(": ")
              .Append(E(string.Join(", ", lostApplications))).Append("</p>");
        sb.Append("</header><main>");

        if (entries.Count > 0)
        {
            sb.Append("<nav><ol>");
            foreach (var e in entries)
                sb.Append("<li><a href=\"#e").Append(e.Sequence).Append("\">").Append(E(e.Caption.Title)).Append("</a></li>");
            sb.Append("</ol></nav>");
        }

        foreach (var e in entries)
        {
            sb.Append("<article id=\"e").Append(e.Sequence).Append("\" style=\"--c:").Append(e.Color).Append("\">");
            sb.Append("<h2>").Append(E(e.Caption.Title)).Append("</h2>");
            sb.Append("<p>").Append(E(e.Caption.Details)).Append("</p>");
            sb.Append("<a href=\"").Append(E(e.Image)).Append("\"><img loading=\"lazy\" src=\"").Append(E(e.Image))
              .Append("\" alt=\"").Append(E(e.Caption.Title)).Append("\"></a>");
            sb.Append("</article>");
        }

        sb.Append("</main></body></html>");
        return sb.ToString();
    }
}
