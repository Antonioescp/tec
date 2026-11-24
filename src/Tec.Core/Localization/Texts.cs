using System.Globalization;
using System.Resources;

namespace Tec.Core.Localization;

/// <summary>Reads texts from a <c>.resx</c> resource in a given language.</summary>
public sealed class Texts(ResourceManager resources, CultureInfo culture)
{
    public CultureInfo Culture { get; } = culture;

    /// <summary>The text for <paramref name="key"/>, or <c>null</c> if no language defines it.</summary>
    public string? Find(string key) => resources.GetString(key, Culture);

    /// <summary>The text for <paramref name="key"/>; the key itself if it is missing, so the gap is visible.</summary>
    public string Get(string key) => Find(key) ?? key;

    /// <summary>The text for <paramref name="key"/> with <paramref name="args"/> formatted in this language.</summary>
    public string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);
}

/// <summary>Resources of Tec.Core.</summary>
public static class CoreResources
{
    public static ResourceManager Captions { get; } = new("Tec.Core.Resources.Captions", typeof(CoreResources).Assembly);
    public static ResourceManager Report { get; } = new("Tec.Core.Resources.Report", typeof(CoreResources).Assembly);
}
