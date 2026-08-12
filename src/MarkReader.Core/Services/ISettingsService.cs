namespace MarkReader.Core.Services;

public interface ISettingsService
{
    bool IsDarkTheme { get; set; }
    void Save();
    void Load();
}
