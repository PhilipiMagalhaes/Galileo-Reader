using System.Text.Json;

namespace MarkReader.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "MarkReader");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Load();
    }

    public bool IsDarkTheme
    {
        get => _settings.IsDarkTheme;
        set
        {
            _settings.IsDarkTheme = value;
            Save();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    private class AppSettings
    {
        public bool IsDarkTheme { get; set; } = false;
    }
}
