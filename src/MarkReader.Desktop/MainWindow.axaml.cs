using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using MarkReader.Core.Services;
using MarkReader.Core.ViewModels;

namespace MarkReader.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Required for Avalonia XAML loader
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = null!; // Dummy for designer
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Wire up file dialog request
        _viewModel.OpenFileDialogRequested += OpenFileDialogAsync;

        // Wire up scroll-to-result
        _viewModel.ScrollToSearchResult += OnScrollToSearchResult;

        // React to theme toggle and search changes
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
            {
                App.SetTheme(_viewModel.IsDarkTheme);
                UpdateThemeIcon();
            }
            else if (e.PropertyName == nameof(MainViewModel.SearchQuery) || 
                     e.PropertyName == nameof(MainViewModel.IsSearchVisible))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ClearHighlights();
                    if (_viewModel.IsSearchVisible && !string.IsNullOrWhiteSpace(_viewModel.SearchQuery))
                    {
                        ApplyHighlights(_viewModel.SearchQuery);
                    }
                }, DispatcherPriority.Background);
            }
        };

        // Keyboard shortcuts
        KeyDown += OnKeyDown;

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

    private void OnScrollToSearchResult(object? sender, SearchResult result)
    {
        // Basic scroll: scroll the viewer proportionally based on line number
        // Full highlight implementation would require custom rendering
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<ScrollViewer>("ContentScrollViewer") is { } sv &&
                sv.Extent.Height > 0)
            {
                // Estimate scroll position from line number (rough approximation)
                var totalLines = _viewModel.MarkdownContent.Split('\n').Length;
                if (totalLines > 0)
                {
                    var fraction = (double)(result.LineNumber - 1) / totalLines;
                    sv.ScrollToEnd();
                    var targetOffset = fraction * sv.Extent.Height;
                    sv.Offset = sv.Offset.WithY(targetOffset);
                }
            }
        }, DispatcherPriority.Render);
    }

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

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path != null && IsMarkdownFile(path))
            {
                var content = await File.ReadAllTextAsync(path);
                _viewModel.MarkdownContent = content;
                _viewModel.CurrentFilePath = path;
                _viewModel.FileName = Path.GetFileName(path);
                var size = new FileInfo(path).Length;
                _viewModel.FileSizeText = size < 1024 ? $"{size} B"
                    : size < 1024 * 1024 ? $"{size / 1024.0:F1} KB"
                    : $"{size / (1024.0 * 1024):F1} MB";
                _viewModel.StatusMessage = $"Arquivo carregado: {_viewModel.FileName}";
                break;
            }
        }
    }

    private static bool IsMarkdownFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".markdown" or ".mdown" or ".mkd";
    }

    private void ClearHighlights()
    {
        var viewer = this.FindControl<Control>("MarkdownScrollViewer");
        if (viewer == null) return;

        var textBlocks = viewer.GetVisualDescendants().OfType<TextBlock>();
        foreach (var run in textBlocks.SelectMany(GetRuns))
        {
            if (run.Background == Brushes.Yellow)
            {
                run.ClearValue(Run.BackgroundProperty);
                run.ClearValue(Run.ForegroundProperty);
            }
        }
    }

    private IEnumerable<Run> GetRuns(TextBlock tb)
    {
        if (tb.Inlines != null)
            return GetRunsFromInlines(tb.Inlines);
        return Array.Empty<Run>();
    }

    private IEnumerable<Run> GetRunsFromInlines(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run) yield return run;
            else if (inline is Span span)
            {
                foreach (var r in GetRunsFromInlines(span.Inlines)) yield return r;
            }
        }
    }

    private void ApplyHighlights(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return;
        
        var viewer = this.FindControl<Control>("MarkdownScrollViewer");
        if (viewer == null) return;

        var textBlocks = viewer.GetVisualDescendants().OfType<TextBlock>().ToList();
        foreach (var tb in textBlocks)
        {
            if (tb.Inlines != null && tb.Inlines.Count > 0)
            {
                ProcessInlinesForHighlight(tb.Inlines, term);
            }
            else if (!string.IsNullOrEmpty(tb.Text))
            {
                var text = tb.Text;
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    tb.Text = null;
                    tb.Inlines = new InlineCollection();
                    tb.Inlines.Add(new Run(text));
                    ProcessInlinesForHighlight(tb.Inlines, term);
                }
            }
        }
    }

    private void ProcessInlinesForHighlight(InlineCollection inlines, string term)
    {
        for (int i = 0; i < inlines.Count; i++)
        {
            var inline = inlines[i];
            if (inline is Run run && !string.IsNullOrEmpty(run.Text))
            {
                if (run.Background == Brushes.Yellow) continue;

                var text = run.Text;
                int idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    inlines.RemoveAt(i);
                    
                    int currentIndex = 0;
                    while (idx >= 0)
                    {
                        if (idx > currentIndex)
                        {
                            var r = new Run(text.Substring(currentIndex, idx - currentIndex));
                            inlines.Insert(i++, r);
                        }
                        
                        var matchRun = new Run(text.Substring(idx, term.Length))
                        {
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Black
                        };
                        inlines.Insert(i++, matchRun);
                        
                        currentIndex = idx + term.Length;
                        idx = text.IndexOf(term, currentIndex, StringComparison.OrdinalIgnoreCase);
                    }
                    if (currentIndex < text.Length)
                    {
                        var r = new Run(text.Substring(currentIndex));
                        inlines.Insert(i, r);
                    }
                    else
                    {
                        i--; // Ajuste de índice pois não houve sobra no final
                    }
                }
            }
            else if (inline is Span span)
            {
                ProcessInlinesForHighlight(span.Inlines, term);
            }
        }
    }
}