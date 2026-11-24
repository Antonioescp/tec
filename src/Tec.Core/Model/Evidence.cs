namespace Tec.Core.Model;

/// <summary>Texto que acompaña a una evidencia.</summary>
public sealed record InteractionCaption(string Title, string Details);

/// <summary>Captura de pantalla tomada para una interacción, antes de anotarse.</summary>
public sealed record InteractionSnapshot(Interaction Interaction, CapturedFrame Frame, DateTimeOffset CapturedAt);

/// <summary>Evidencia final: imagen anotada más los metadatos de la interacción.</summary>
public sealed record Evidence(
    int Sequence,
    Interaction Interaction,
    InteractionCaption Caption,
    ScreenRect HighlightBounds,
    RgbColor HighlightColor,
    RasterImage Image,
    DateTimeOffset CapturedAt);

/// <summary>Sesión de recolección de evidencia.</summary>
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

/// <summary>Proceso candidato a ser observado.</summary>
public sealed record TargetProcessInfo(int ProcessId, string ProcessName, string MainWindowTitle)
{
    public override string ToString() =>
        string.IsNullOrWhiteSpace(MainWindowTitle)
            ? $"{ProcessName} ({ProcessId})"
            : $"{ProcessName} ({ProcessId}) — {MainWindowTitle}";
}
