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
    string OutputDirectory,
    string? DisplayName = null)
{
    public const int MaxDescriptionLength = 40;

    /// <summary>Name of the session as the tester wrote it; the applications when there is no name.</summary>
    public string Name => string.IsNullOrEmpty(DisplayName) ? TargetDescription : DisplayName;

    /// <summary>Session for a set of observed applications; the folder name joins their names.</summary>
    public static EvidenceSession Create(string rootDirectory, IReadOnlyList<string> targetNames, DateTimeOffset now) =>
        Create(rootDirectory, targetNames, sessionName: null, now);

    /// <summary>
    /// Session named by the tester: the name goes in the folder after making it folder-safe, and in the
    /// report as it was written. An unusable name is no name at all.
    /// </summary>
    public static EvidenceSession Create(string rootDirectory, IReadOnlyList<string> targetNames, string? sessionName, DateTimeOffset now)
    {
        var description = DescribeTargetsForFolder(targetNames);
        var slug = SanitizeName(sessionName);
        var id = UniqueFolderName(rootDirectory, BuildFolderName(now, slug ?? description));
        return new EvidenceSession(id, description, now, Path.Combine(rootDirectory, id), slug is null ? null : sessionName!.Trim());
    }

    public static EvidenceSession Create(string rootDirectory, string targetDescription, DateTimeOffset now) =>
        CreateNamed(rootDirectory, Sanitize(targetDescription), null, now);

    /// <summary>
    /// Folder a session with these applications would get, without the collision suffix: what the
    /// name field shows while it is still empty.
    /// </summary>
    public static string PreviewSessionName(IReadOnlyList<string> targetNames, DateTimeOffset now) =>
        BuildFolderName(now, DescribeTargetsForFolder(targetNames));

    private static EvidenceSession CreateNamed(string rootDirectory, string description, string? displayName, DateTimeOffset now)
    {
        var id = UniqueFolderName(rootDirectory, BuildFolderName(now, description));
        return new EvidenceSession(id, description, now, Path.Combine(rootDirectory, id), displayName);
    }

    private static string BuildFolderName(DateTimeOffset now, string name) => $"{now:yyyyMMdd_HHmmss}_{name}";

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

    /// <summary>The application set as a folder piece, so the folder and the name field agree.</summary>
    private static string DescribeTargetsForFolder(IReadOnlyList<string> targetNames) => Sanitize(DescribeTargets(targetNames));

    /// <summary>Frees the folder name with "_2", "_3"… when a session already used it.</summary>
    private static string UniqueFolderName(string rootDirectory, string folderName)
    {
        if (!Directory.Exists(Path.Combine(rootDirectory, folderName))) return folderName;
        for (var n = 2; n <= 99; n++)
        {
            var candidate = $"{folderName}_{n}";
            if (!Directory.Exists(Path.Combine(rootDirectory, candidate))) return candidate;
        }
        throw new IOException($"No hay carpetas libres para la sesión «{folderName}». Usa otro nombre.");
    }

    /// <summary>Folder-safe version of the name the tester typed; null when nothing of it is usable.</summary>
    private static string? SanitizeName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var invalid = Path.GetInvalidFileNameChars();
        var chars = text.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
        var result = new string(chars);
        if (result.Length > MaxDescriptionLength) result = result[..MaxDescriptionLength];
        // Windows drops trailing dots and spaces by itself, which would make the real folder
        // name differ from the session id.
        result = result.Trim('_').TrimEnd('.').TrimEnd('_');
        return result.Length == 0 ? null : result;
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
