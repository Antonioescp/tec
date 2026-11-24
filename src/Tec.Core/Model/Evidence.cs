namespace Tec.Core.Model;

/// <summary>Text that accompanies a piece of evidence.</summary>
public sealed record InteractionCaption(string Title, string Details);

/// <summary>Screenshot taken for an interaction, before annotation.</summary>
public sealed record InteractionSnapshot(Interaction Interaction, CapturedFrame Frame, DateTimeOffset CapturedAt);

/// <summary>Final evidence: annotated image plus the interaction metadata.</summary>
public sealed record Evidence(
    int Sequence,
    Interaction Interaction,
    InteractionCaption Caption,
    ScreenRect HighlightBounds,
    RgbColor HighlightColor,
    RasterImage Image,
    DateTimeOffset CapturedAt);

/// <summary>Evidence collection session.</summary>
public sealed record EvidenceSession(
    string Id,
    string TargetDescription,
    DateTimeOffset StartedAt,
    string OutputDirectory)
{
    public static EvidenceSession Create(string rootDirectory, string targetDescription, DateTimeOffset now)
    {
        var id = $"{now:yyyyMMdd_HHmmss}_{Sanitize(targetDescription)}";
        return new EvidenceSession(id, targetDescription, now, Path.Combine(rootDirectory, id));
    }

    private static string Sanitize(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = text.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim('_');
        if (result.Length > 40) result = result[..40];
        return result.Length == 0 ? "sesion" : result;
    }
}

/// <summary>Process that can be observed.</summary>
public sealed record TargetProcessInfo(int ProcessId, string ProcessName, string MainWindowTitle)
{
    public override string ToString() =>
        string.IsNullOrWhiteSpace(MainWindowTitle)
            ? $"{ProcessName} ({ProcessId})"
            : $"{ProcessName} ({ProcessId}) — {MainWindowTitle}";
}
