using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using Markdown.Avalonia;
using MarkReader.Core.Services;
using MarkReader.Core.ViewModels;

namespace MarkReader.Desktop;

public partial class MainWindow : Window, IDocumentSearchView
{
    private readonly MainViewModel _viewModel;
    private readonly SearchHighlighter _highlighter;

    private const string HighlightBackgroundKey = "SearchHighlightBrush";
    private const string HighlightForegroundKey = "SearchHighlightForegroundBrush";
    private const string CurrentBackgroundKey = "SearchCurrentBrush";
    private const string CurrentForegroundKey = "SearchCurrentForegroundBrush";

    // Required for Avalonia XAML loader
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = null!; // Dummy for designer
        _highlighter = null!;
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _highlighter = new SearchHighlighter(ActiveDocumentViewer);

        // Wire up file dialog request
        _viewModel.OpenFileDialogRequested += OpenFileDialogAsync;

        // A busca opera sobre o documento renderizado — quem destaca e conta é esta janela
        _viewModel.AttachSearchView(this);

        // React to theme toggle
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
            {
                App.SetTheme(_viewModel.IsDarkTheme);
                UpdateThemeIcon();
                RepaintHighlights();
            }
        };

        // Keyboard shortcuts
        KeyDown += OnKeyDown;

        // Ctrl+Tab tem de ser interceptado no tunelamento: o Avalonia consome o Tab para
        // navegação de foco antes de o evento borbulhar até a janela.
        AddHandler(KeyDownEvent, OnTunneledKeyDown, RoutingStrategies.Tunnel);

        // Drag & drop support
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DragDrop.SetAllowDrop(this, true);

        // Set initial theme icon
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        if (this.FindControl<TextBlock>("ThemeIcon") is { } icon)
            icon.Text = _viewModel.IsDarkTheme ? "☀️" : "🌙";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    _viewModel.OpenFileCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F:
                    _viewModel.ToggleSearchCommand.Execute(null);
                    if (_viewModel.IsSearchVisible)
                        FocusSearchBox();
                    e.Handled = true;
                    break;
                case Key.T:
                    _viewModel.ToggleThemeCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.W:
                    _viewModel.CloseCurrentDocumentCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            if (e.Key == Key.Escape && _viewModel.IsSearchVisible)
            {
                _viewModel.CloseSearchCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _viewModel.IsSearchVisible)
            {
                _viewModel.NextResultCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.Shift && e.Key == Key.Enter && _viewModel.IsSearchVisible)
        {
            _viewModel.PreviousResultCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnTunneledKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || _viewModel.OpenDocuments.Count < 2) return;

        // Modificadores exatos: Ctrl+Alt+Tab não é atalho de aba, e sem abas suficientes
        // o Tab continua sendo navegação de foco.
        if (e.KeyModifiers == KeyModifiers.Control)
            _viewModel.SelectNextDocumentCommand.Execute(null);
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            _viewModel.SelectPreviousDocumentCommand.Execute(null);
        else
            return;

        e.Handled = true;
    }

    private void FocusSearchBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TextBox>("SearchBox") is { } box)
            {
                box.Focus();
                box.SelectAll();
            }
        }, DispatcherPriority.Render);
    }

    private async Task<string?> OpenFileDialogAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Abrir arquivo Markdown",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Markdown")
                {
                    Patterns = new[] { "*.md", "*.markdown", "*.mdown", "*.mkd" },
                    MimeTypes = new[] { "text/markdown" }
                },
                new FilePickerFileType("Todos os arquivos")
                {
                    Patterns = new[] { "*" }
                }
            }
        };

        var result = await StorageProvider.OpenFilePickerAsync(options);
        return result?.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <summary>
    /// O visualizador da aba da frente. Buscar na subárvore inteira do host desceria por
    /// cada aba escondida antes de achar a certa — custo que cresce com o número de abas.
    /// Aqui só a aba ativa é percorrida.
    /// </summary>
    private Control? ActiveDocumentViewer() =>
        this.FindControl<ItemsControl>("DocumentHost")?
            .GetRealizedContainers()
            .FirstOrDefault(container => ReferenceEquals(container.DataContext, _viewModel.SelectedDocument))?
            .GetVisualDescendants()
            .OfType<MarkdownScrollViewer>()
            .FirstOrDefault();

    // ── IDocumentSearchView ──────────────────────────────────────────

    public int Highlight(string term) => _highlighter.Highlight(term, CurrentPalette());

    public void GoToResult(int index) => _highlighter.GoToResult(index);

    public void ClearHighlights() => _highlighter.Clear();

    /// <summary>Trocar de tema troca os pinceis; sem repintar, o destaque fica com a cor antiga.</summary>
    private void RepaintHighlights()
    {
        if (_viewModel.SearchResultCount == 0) return;

        var index = _viewModel.CurrentSearchIndex;

        // O termo pode ter mudado desde o último destaque (busca em voo): a contagem
        // que volta é a verdadeira, senão o índice guardado pode cair fora de faixa.
        _viewModel.SearchResultCount = Highlight(_viewModel.SearchQuery);

        if (index < _viewModel.SearchResultCount)
            GoToResult(index);
    }

    private SearchPalette CurrentPalette() => new(
        HighlightBrush(HighlightBackgroundKey, Color.Parse("#FFD54F")),
        HighlightBrush(HighlightForegroundKey, Color.Parse("#1F2328")),
        HighlightBrush(CurrentBackgroundKey, Color.Parse("#C2410C")),
        HighlightBrush(CurrentForegroundKey, Colors.White));

    private IBrush HighlightBrush(string resourceKey, Color fallback) =>
        this.TryFindResource(resourceKey, ActualThemeVariant, out var found) && found is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files == null) return;

        // N arquivos soltos = N abas
        var dropped = files
            .Select(file => file.TryGetLocalPath())
            .OfType<string>()
            .Where(IsMarkdownFile);

        await _viewModel.LoadFilesAsync(dropped);
    }

    private static bool IsMarkdownFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".markdown" or ".mdown" or ".mkd";
    }
}
