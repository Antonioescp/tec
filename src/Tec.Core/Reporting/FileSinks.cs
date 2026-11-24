using System.Text.Encodings.Web;
using System.Text.Json;
using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Reporting;

/// <summary>Nombres del tipo <c>001_click.png</c>.</summary>
public sealed class SequentialEvidenceFileNamer(string fileExtension) : IEvidenceFileNamer
{
    public string GetImageFileName(Evidence evidence) =>
        $"{evidence.Sequence:000}_{evidence.Interaction.Kind.Code}{fileExtension}";
}

/// <summary>Guarda la imagen anotada de cada evidencia en la carpeta de la sesión.</summary>
public sealed class ImageFileEvidenceSink(IImageEncoder encoder, IEvidenceFileNamer namer) : IEvidenceSink
{
    public Task OnSessionStartedAsync(EvidenceSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(session.OutputDirectory);
        return Task.CompletedTask;
    }

    public async Task WriteAsync(EvidenceSession session, Evidence evidence, CancellationToken cancellationToken)
    {
        var path = Path.Combine(session.OutputDirectory, namer.GetImageFileName(evidence));
        using var buffer = new MemoryStream();
        encoder.Encode(evidence.Image, buffer);
        await File.WriteAllBytesAsync(path, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    public Task OnSessionCompletedAsync(EvidenceSession session, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Bitácora de metadatos en formato JSON Lines (<c>evidencias.jsonl</c>).</summary>
public sealed class JsonLinesEvidenceLogSink(IEvidenceFileNamer namer) : IEvidenceSink
{
    public const string FileName = "evidencias.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task OnSessionStartedAsync(EvidenceSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(session.OutputDirectory);
        return Task.CompletedTask;
    }

    public async Task WriteAsync(EvidenceSession session, Evidence evidence, CancellationToken cancellationToken)
    {
        var i = evidence.Interaction;
        var entry = new
        {
            evidence.Sequence,
            Image = namer.GetImageFileName(evidence),
            Kind = i.Kind.Code,
            evidence.Caption.Title,
            evidence.Caption.Details,
            i.Timestamp,
            evidence.CapturedAt,
            i.ProcessId,
            i.WindowTitle,
            Point = i.Point is { } p ? new { p.X, p.Y } : null,
            Highlight = new { evidence.HighlightBounds.Left, evidence.HighlightBounds.Top, evidence.HighlightBounds.Width, evidence.HighlightBounds.Height },
            HighlightColor = evidence.HighlightColor.ToHex(),
            Element = i.Element is { } e
                ? new { e.Name, e.ControlType, e.AutomationId, Value = e.IsPassword ? null : e.Value, e.IsPassword }
                : null,
            i.Notes,
        };

        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(Path.Combine(session.OutputDirectory, FileName), line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task OnSessionCompletedAsync(EvidenceSession session, CancellationToken cancellationToken) => Task.CompletedTask;
}
