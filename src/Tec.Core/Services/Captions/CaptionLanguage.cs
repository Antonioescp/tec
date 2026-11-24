using System.Globalization;

namespace Tec.Core.Services.Captions;

/// <summary>
/// Texts used by <see cref="CaptionFormatter"/> for one language. Interactions carry
/// language-independent data (kind codes, key names, UI Automation control types, note
/// codes) and these tables turn it into text. Entries that are missing fall back to the
/// raw value, so new kinds, keys or notes still produce a readable caption.
/// </summary>
public sealed partial class CaptionLanguage
{
    /// <summary>ISO 639-1 code, e.g. "es".</summary>
    public required string Code { get; init; }

    /// <summary>Name of the language in that language, e.g. "Español".</summary>
    public required string Name { get; init; }

    /// <summary>Culture used to format numbers.</summary>
    public required CultureInfo Culture { get; init; }

    /// <summary>Interaction kind names by <see cref="Model.InteractionKind.Code"/>.</summary>
    public required IReadOnlyDictionary<string, string> InteractionKinds { get; init; }

    /// <summary>Control type names by UI Automation control type (e.g. "Button").</summary>
    public required IReadOnlyDictionary<string, string> ControlTypes { get; init; }

    /// <summary>Key names by language-independent key name (e.g. "Delete").</summary>
    public required IReadOnlyDictionary<string, string> Keys { get; init; }

    /// <summary>Note texts by note code; <c>{0}</c> is replaced with the note detail.</summary>
    public required IReadOnlyDictionary<string, string> Notes { get; init; }

    /// <summary>Name for a UI element whose type is unknown.</summary>
    public required string UnknownElement { get; init; }

    /// <summary>Window title detail; <c>{0}</c> is the title.</summary>
    public required string WindowFormat { get; init; }

    /// <summary>Pointer position detail; <c>{0}</c> and <c>{1}</c> are X and Y.</summary>
    public required string PositionFormat { get; init; }

    /// <summary>Keyboard command on an element; <c>{0}</c> is the keys, <c>{1}</c> the element.</summary>
    public required string CommandOnElementFormat { get; init; }

    /// <summary>Screen change size; <c>{0}</c> is the changed fraction (0-1).</summary>
    public required string ContentChangedFormat { get; init; }

    /// <summary>Languages available for captions.</summary>
    public static IReadOnlyList<CaptionLanguage> All => [Spanish, English];

    /// <summary>Finds a language by its code; <c>null</c> if it is not available.</summary>
    public static CaptionLanguage? Find(string code) =>
        All.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => Name;
}
