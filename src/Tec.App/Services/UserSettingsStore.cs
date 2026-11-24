using System.IO;
using System.Text.Json;
using Tec.Core.Diagnostics;

namespace Tec.App.Services;

/// <summary>Preferences kept between runs.</summary>
public sealed record UserSettings
{
    /// <summary>Language code (e.g. "es"); <c>null</c> to use the Windows language.</summary>
    public string? Language { get; init; }
}

public interface IUserSettingsStore
{
    UserSettings Load();
    void Save(UserSettings settings);
}

/// <summary>Stores the preferences in a JSON file. A missing or damaged file means the defaults.</summary>
public sealed class JsonUserSettingsStore(string filePath) : IUserSettingsStore
{
    private const string LogCategory = "Settings";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(filePath)) return new UserSettings();
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(filePath), JsonOptions) ?? new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(LogCategory, $"No se pudieron leer las preferencias de {filePath}; se usan las predeterminadas.", ex);
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(LogCategory, $"No se pudieron guardar las preferencias en {filePath}.", ex);
        }
    }
}
