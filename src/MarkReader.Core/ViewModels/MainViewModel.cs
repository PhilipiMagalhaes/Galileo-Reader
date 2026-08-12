using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkReader.Core.Services;

namespace MarkReader.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMarkdownService _markdownService;
    private readonly ISettingsService _settingsService;

    private IDocumentSearchView? _searchView;

    /// <summary>
    /// Destacar percorre a árvore visual inteira: medido em ~400 ms num documento de 202 KB
    /// (2400 blocos). Buscar a cada tecla travaria a digitação — daí a espera por pausa.
    /// </summary>
    private const int SearchDebounceMilliseconds = 180;

    private CancellationTokenSource? _pendingSearch;

    [ObservableProperty]
    private string _markdownContent = string.Empty;

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchCountText))]
    private int _searchResultCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchCountText))]
    private int _currentSearchIndex = -1;

    [ObservableProperty]
    private bool _isSearchVisible = false;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string _statusMessage = "Abra um arquivo Markdown para começar";

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fileSizeText = string.Empty;

    // Event to open file dialog (implemented at UI layer)
    public event Func<Task<string?>>? OpenFileDialogRequested;

    public string SearchCountText =>
        SearchResultCount == 0 ? "Sem resultados" :
        $"{CurrentSearchIndex + 1} de {SearchResultCount}";

    public MainViewModel(
        IMarkdownService markdownService,
        ISettingsService settingsService)
    {
        _markdownService = markdownService;
        _settingsService = settingsService;

        _isDarkTheme = _settingsService.IsDarkTheme;
    }

    /// <summary>
    /// Liga a VM ao documento renderizado. A busca acontece sobre o que está na tela,
    /// então quem sabe contar e destacar é a camada de UI.
    /// </summary>
    public void AttachSearchView(IDocumentSearchView searchView) => _searchView = searchView;

    private IDocumentSearchView SearchView =>
        _searchView ?? throw new InvalidOperationException(
            $"{nameof(AttachSearchView)} não foi chamado: a busca não tem documento para operar.");

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var path = OpenFileDialogRequested != null
            ? await OpenFileDialogRequested.Invoke()
            : null;

        if (string.IsNullOrEmpty(path)) return;

        await LoadFileAsync(path);
    }

    /// <summary>
    /// Caminho único de abertura — diálogo e arrastar-e-soltar passam por aqui.
    /// Trocar o documento por fora deixaria a busca contando ocorrências de um texto
    /// que não está mais na tela.
    /// </summary>
    public async Task LoadFileAsync(string path)
    {
        try
        {
            var content = await Task.Run(() => _markdownService.LoadMarkdown(path));

            IsSearchVisible = false;
            ResetSearch();

            MarkdownContent = content;
            CurrentFilePath = path;
            FileName = Path.GetFileName(path);
            FileSizeText = FormatFileSize(new FileInfo(path).Length);
            StatusMessage = $"Arquivo carregado: {FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao abrir arquivo: {ex.Message}";
        }
    }

    private static string FormatFileSize(long size) =>
        size < 1024 ? $"{size} B"
        : size < 1024 * 1024 ? $"{size / 1024.0:F1} KB"
        : $"{size / (1024.0 * 1024):F1} MB";

    [RelayCommand]
    public void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible)
            ResetSearch();
    }

    [RelayCommand]
    public void CloseSearch()
    {
        IsSearchVisible = false;
        ResetSearch();
    }

    [RelayCommand]
    public void ExecuteSearch()
    {
        CancelPendingSearch();

        if (string.IsNullOrEmpty(MarkdownContent) || string.IsNullOrWhiteSpace(SearchQuery))
        {
            ResetSearch();
            return;
        }

        SearchResultCount = SearchView.Highlight(SearchQuery);
        CurrentSearchIndex = SearchResultCount > 0 ? 0 : -1;

        if (CurrentSearchIndex >= 0)
            SearchView.GoToResult(CurrentSearchIndex);
    }

    [RelayCommand]
    public void NextResult() => MoveResult(+1);

    [RelayCommand]
    public void PreviousResult() => MoveResult(-1);

    private void MoveResult(int step)
    {
        // Enter logo após digitar não pode esperar a pausa do debounce
        if (_pendingSearch != null)
            ExecuteSearch();

        if (SearchResultCount == 0) return;

        CurrentSearchIndex = (CurrentSearchIndex + step + SearchResultCount) % SearchResultCount;
        SearchView.GoToResult(CurrentSearchIndex);
    }

    /// <summary>Único dono da limpeza: zerar a consulta reentra aqui via OnSearchQueryChanged.</summary>
    private void ResetSearch()
    {
        CancelPendingSearch();
        SearchView.ClearHighlights();
        SearchResultCount = 0;
        CurrentSearchIndex = -1;
        SearchQuery = string.Empty;
    }

    /// <summary>Espera a digitação parar antes de destacar; cada tecla nova adia a anterior.</summary>
    private async void ScheduleSearch()
    {
        CancelPendingSearch();

        var pending = new CancellationTokenSource();
        _pendingSearch = pending;

        try
        {
            await Task.Delay(SearchDebounceMilliseconds, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ExecuteSearch();
    }

    private void CancelPendingSearch()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        _pendingSearch = null;
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _settingsService.IsDarkTheme = IsDarkTheme;
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            ResetSearch();
        else
            ScheduleSearch();
    }
}
