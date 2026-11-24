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
    public const int MaxDescriptionLength = 40;

    /// <summary>Session for a set of observed applications; the folder name joins their names.</summary>
    public static EvidenceSession Create(string rootDirectory, IReadOnlyList<string> targetNames, DateTimeOffset now) =>
        Create(rootDirectory, DescribeTargets(targetNames), now);

    public static EvidenceSession Create(string rootDirectory, string targetDescription, DateTimeOffset now)
    {
        var id = $"{now:yyyyMMdd_HHmmss}_{Sanitize(targetDescription)}";
        return new EvidenceSession(id, targetDescription, now, Path.Combine(rootDirectory, id));
    }

    /// <summary>
    /// Application names joined with "+" ("Farmax+Postman"). When they do not fit in a folder
    /// name the last ones are dropped and counted ("Farmax+Post+2").
    /// </summary>
    public static string DescribeTargets(IReadOnlyList<string> targetNames)
    {
        List<string> names = [.. targetNames.Select(n => n.Trim()).Where(n => n.Length > 0)];
        if (names.Count == 0) return "sesion";

        var joined = string.Join("+", names);
        if (joined.Length <= MaxDescriptionLength) return joined;

        // Leave room for the "+N" that counts the names left out.
        var budget = MaxDescriptionLength - 4;
        var kept = new List<string>();
        var used = 0;
        foreach (var name in names)
        {
            var needed = name.Length + (kept.Count > 0 ? 1 : 0);
            if (kept.Count > 0 && used + needed > budget) break;
            kept.Add(name);
            used += needed;
        }
        return string.Join("+", kept) + "+" + (names.Count - kept.Count);
    }

    private static string Sanitize(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = text.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim('_');
        if (result.Length > MaxDescriptionLength) result = result[..MaxDescriptionLength];
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
