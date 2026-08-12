using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkReader.Core.Services;

namespace MarkReader.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMarkdownService _markdownService;
    private readonly ISearchService _searchService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _markdownContent = string.Empty;

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<SearchResult> _searchResults = Array.Empty<SearchResult>();

    [ObservableProperty]
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

    // Event to notify the UI to scroll to a search result
    public event EventHandler<SearchResult>? ScrollToSearchResult;

    // Event to open file dialog (implemented at UI layer)
    public event Func<Task<string?>>? OpenFileDialogRequested;

    public string SearchCountText =>
        SearchResults.Count == 0 ? "Sem resultados" :
        $"{CurrentSearchIndex + 1} de {SearchResults.Count}";

    public MainViewModel(
        IMarkdownService markdownService,
        ISearchService searchService,
        ISettingsService settingsService)
    {
        _markdownService = markdownService;
        _searchService = searchService;
        _settingsService = settingsService;

        _isDarkTheme = _settingsService.IsDarkTheme;
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var path = OpenFileDialogRequested != null
            ? await OpenFileDialogRequested.Invoke()
            : null;

        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var content = await Task.Run(() => _markdownService.LoadMarkdown(path));
            MarkdownContent = content;
            CurrentFilePath = path;
            FileName = Path.GetFileName(path);

            var size = new FileInfo(path).Length;
            FileSizeText = size < 1024 ? $"{size} B"
                : size < 1024 * 1024 ? $"{size / 1024.0:F1} KB"
                : $"{size / (1024.0 * 1024):F1} MB";

            StatusMessage = $"Arquivo carregado: {FileName}";

            // Clear search on new file
            SearchQuery = string.Empty;
            SearchResults = Array.Empty<SearchResult>();
            CurrentSearchIndex = -1;
            IsSearchVisible = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao abrir arquivo: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible)
        {
            SearchQuery = string.Empty;
            SearchResults = Array.Empty<SearchResult>();
            CurrentSearchIndex = -1;
            OnPropertyChanged(nameof(SearchCountText));
        }
    }

    [RelayCommand]
    public void CloseSearch()
    {
        IsSearchVisible = false;
        SearchQuery = string.Empty;
        SearchResults = Array.Empty<SearchResult>();
        CurrentSearchIndex = -1;
        OnPropertyChanged(nameof(SearchCountText));
    }

    [RelayCommand]
    public void ExecuteSearch()
    {
        if (string.IsNullOrEmpty(MarkdownContent) || string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults = Array.Empty<SearchResult>();
            CurrentSearchIndex = -1;
            OnPropertyChanged(nameof(SearchCountText));
            return;
        }

        SearchResults = _searchService.Search(MarkdownContent, SearchQuery);
        CurrentSearchIndex = SearchResults.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(SearchCountText));

        if (CurrentSearchIndex >= 0)
            ScrollToSearchResult?.Invoke(this, SearchResults[CurrentSearchIndex]);
    }

    [RelayCommand]
    public void NextResult()
    {
        if (SearchResults.Count == 0) return;
        CurrentSearchIndex = (CurrentSearchIndex + 1) % SearchResults.Count;
        OnPropertyChanged(nameof(SearchCountText));
        ScrollToSearchResult?.Invoke(this, SearchResults[CurrentSearchIndex]);
    }

    [RelayCommand]
    public void PreviousResult()
    {
        if (SearchResults.Count == 0) return;
        CurrentSearchIndex = (CurrentSearchIndex - 1 + SearchResults.Count) % SearchResults.Count;
        OnPropertyChanged(nameof(SearchCountText));
        ScrollToSearchResult?.Invoke(this, SearchResults[CurrentSearchIndex]);
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
        {
            SearchResults = Array.Empty<SearchResult>();
            CurrentSearchIndex = -1;
            OnPropertyChanged(nameof(SearchCountText));
        }
        else
        {
            ExecuteSearch();
        }
    }
}
