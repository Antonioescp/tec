using System.Globalization;

namespace Tec.Core.Localization;

/// <summary>
/// A language TEC can be used in: the interface, captions and report. The texts live in
/// <c>.resx</c> files (Spanish is the neutral language, the others are <c>*.{code}.resx</c>).
/// To add a language, add its <c>.resx</c> files and register it in <see cref="All"/>.
/// </summary>
/// <param name="Code">ISO 639-1 code, e.g. "es".</param>
/// <param name="Name">Name of the language in that language, e.g. "Español".</param>
/// <param name="Culture">Culture used to pick resources and format numbers.</param>
public sealed record TecLanguage(string Code, string Name, CultureInfo Culture)
{
    public static readonly TecLanguage Spanish = new("es", "Español", CultureInfo.GetCultureInfo("es-MX"));
    public static readonly TecLanguage English = new("en", "English", CultureInfo.GetCultureInfo("en-US"));

    public static IReadOnlyList<TecLanguage> All { get; } = [Spanish, English];

    /// <summary>Finds a language by its code; <c>null</c> if it is not available.</summary>
    public static TecLanguage? Find(string? code) =>
        All.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>The language of <paramref name="culture"/> if available; otherwise Spanish.</summary>
    public static TecLanguage FromCulture(CultureInfo culture) => Find(culture.TwoLetterISOLanguageName) ?? Spanish;

    public override string ToString() => Name;
}
