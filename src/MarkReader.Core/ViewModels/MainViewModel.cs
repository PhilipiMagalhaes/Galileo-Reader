using System.Collections.ObjectModel;
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

    public ObservableCollection<DocumentViewModel> OpenDocuments { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDocument))]
    private DocumentViewModel? _selectedDocument;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchCountText))]
    private int _searchResultCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchCountText))]
    private int _currentSearchIndex = -1;

    [ObservableProperty]
    private bool _isSearchVisible;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string _statusMessage = "Abra um arquivo Markdown para começar";

    // Event to open file dialog (implemented at UI layer)
    public event Func<Task<string?>>? OpenFileDialogRequested;

    public bool HasActiveDocument => SelectedDocument != null;

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
    /// Arquivo já aberto ativa a aba existente em vez de duplicá-la.
    /// </summary>
    public async Task LoadFileAsync(string path)
    {
        // Tudo dentro do try: normalizar o caminho já lança para string vazia ou só espaços,
        // e um caminho ruim tem de virar mensagem de status, nunca exceção — quem chama
        // pode ser um lambda de dispatcher, onde exceção não observada mata o app.
        try
        {
            if (OpenDocuments.FirstOrDefault(document => document.HasPath(path)) is { } alreadyOpen)
            {
                SelectedDocument = alreadyOpen;
                StatusMessage = $"Já aberto: {alreadyOpen.FileName}";
                return;
            }

            var content = await Task.Run(() => _markdownService.LoadMarkdown(path));
            var document = new DocumentViewModel(path, content, new FileInfo(path).Length);

            OpenDocuments.Add(document);
            SelectedDocument = document;
            StatusMessage = $"Arquivo carregado: {document.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao abrir arquivo: {ex.Message}";
        }
    }

    /// <summary>
    /// Abre uma sequência de arquivos em abas, em ordem — usado pelos argumentos de linha de
    /// comando e por arrastar vários arquivos de uma vez. A última aberta fica à frente;
    /// caminho que falha vira mensagem de status e não interrompe os demais.
    /// </summary>
    public async Task LoadFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            await LoadFileAsync(path);
    }

    /// <summary>
    /// Dos argumentos de linha de comando, os que são candidatos a caminho de arquivo.
    /// Descarta vazios e o que parece opção (<c>-x</c>, <c>/x</c>) — se um dia houver opções
    /// de verdade, é aqui que elas se separam. Caminho inexistente <b>passa</b> de propósito:
    /// vira erro visível na barra de status em vez de sumir calado.
    /// </summary>
    public static IEnumerable<string> FilePathArguments(IEnumerable<string> args) =>
        args.Where(argument => !string.IsNullOrWhiteSpace(argument))
            .Where(argument => !argument.StartsWith('-') && !argument.StartsWith('/'));

    [RelayCommand]
    public void CloseDocument(DocumentViewModel? document)
    {
        if (document == null || !OpenDocuments.Contains(document)) return;

        if (SelectedDocument == document)
            SelectedDocument = NeighbourOf(document);

        OpenDocuments.Remove(document);
    }

    /// <summary>
    /// A vizinha da direita; na última aba, a da esquerda. Escolhida <b>antes</b> da remoção:
    /// tirar da coleção um item selecionado faz o ListBox reeleger a seleção sozinho, e o
    /// estado passaria por um valor intermediário que pisca a tira de abas e o empty state.
    /// </summary>
    private DocumentViewModel? NeighbourOf(DocumentViewModel document)
    {
        if (OpenDocuments.Count <= 1) return null;

        int index = OpenDocuments.IndexOf(document);
        return OpenDocuments[index == OpenDocuments.Count - 1 ? index - 1 : index + 1];
    }

    [RelayCommand]
    public void CloseCurrentDocument() => CloseDocument(SelectedDocument);

    [RelayCommand]
    public void SelectNextDocument() => MoveSelection(+1);

    [RelayCommand]
    public void SelectPreviousDocument() => MoveSelection(-1);

    private void MoveSelection(int step)
    {
        if (OpenDocuments.Count < 2 || SelectedDocument == null) return;

        int index = OpenDocuments.IndexOf(SelectedDocument);
        SelectedDocument = OpenDocuments[(index + step + OpenDocuments.Count) % OpenDocuments.Count];
    }

    partial void OnSelectedDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsActive = false;
        if (newValue != null) newValue.IsActive = true;

        // O status fala sempre da aba da frente; quem abre sobrescreve com a própria mensagem.
        StatusMessage = newValue == null
            ? "Abra um arquivo Markdown para começar"
            : $"Lendo: {newValue.FileName}";

        // A busca é do documento da frente: trocar de aba não pode levar junto a contagem
        // nem os destaques do documento anterior.
        IsSearchVisible = false;
        ResetSearch();
    }

    [RelayCommand]
    public void ToggleSearch()
    {
        if (!HasActiveDocument) return;

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

        if (!HasActiveDocument || string.IsNullOrWhiteSpace(SearchQuery))
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
        // Enter logo após digitar não espera a pausa do debounce — e a busca que acabou de
        // rodar já posiciona na 1ª ocorrência, então aplicar o passo por cima pularia ela.
        if (_pendingSearch != null)
        {
            ExecuteSearch();
            return;
        }

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
