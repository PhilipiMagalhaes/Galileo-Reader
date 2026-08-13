using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
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
            OpenFilesFromCommandLine(ViewModel, desktop.Args);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Abre os arquivos passados na linha de comando — é o que faz o duplo clique num .md
    /// no Explorer chegar aqui. Adiado para o dispatcher porque este método é síncrono e
    /// não pode aguardar: ler o arquivo aqui atrasaria a janela a aparecer.
    /// </summary>
    private static void OpenFilesFromCommandLine(MainViewModel viewModel, string[]? args)
    {
        var paths = MainViewModel.FilePathArguments(args ?? Array.Empty<string>()).ToList();
        if (paths.Count == 0) return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await viewModel.LoadFilesAsync(paths);
            }
            catch (Exception ex)
            {
                // Rede de segurança: aqui é async void, e exceção não observada mata o app.
                viewModel.StatusMessage = $"Erro ao abrir arquivo: {ex.Message}";
            }
        });
    }

    public static void SetTheme(bool isDark)
    {
        if (Current is App app)
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
