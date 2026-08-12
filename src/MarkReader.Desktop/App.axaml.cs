using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MarkReader.Core.Services;
using MarkReader.Core.ViewModels;

namespace MarkReader.Desktop;

public partial class App : Application
{
    public static MainViewModel? ViewModel { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var markdownService = new MarkdownService();
        var settingsService = new SettingsService();

        ViewModel = new MainViewModel(markdownService, settingsService);

        // Apply saved theme preference
        RequestedThemeVariant = settingsService.IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(ViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void SetTheme(bool isDark)
    {
        if (Current is App app)
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}