using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using MarkReader.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace MarkReader.Tests;

/// <summary>
/// Aplicação headless dos testes: renderiza markdown de verdade, sem abrir janela.
/// </summary>
public class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
