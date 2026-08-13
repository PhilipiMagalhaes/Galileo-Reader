using MarkReader.Core.Services;
using MarkReader.Core.ViewModels;
using Xunit;

namespace MarkReader.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly PastaTemporaria _pasta = new("markreader-abas");
    private readonly SearchViewFake _busca = new();
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        _vm = new MainViewModel(new MarkdownServiceFake(), new SettingsServiceFake());
        _vm.AttachSearchView(_busca);
    }

    public void Dispose()
    {
        _pasta.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Abrir_dois_arquivos_cria_duas_abas_e_seleciona_a_ultima()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        Assert.Equal(2, _vm.OpenDocuments.Count);
        Assert.Equal("b.md", _vm.SelectedDocument?.FileName);
        Assert.True(_vm.HasActiveDocument);
    }

    [Fact]
    public async Task Reabrir_o_mesmo_arquivo_ativa_a_aba_existente_sem_duplicar()
    {
        var a = Arquivo("a.md", "# A");
        await _vm.LoadFileAsync(a);
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        await _vm.LoadFileAsync(a);

        Assert.Equal(2, _vm.OpenDocuments.Count);
        Assert.Equal("a.md", _vm.SelectedDocument?.FileName);
    }

    [Fact]
    public async Task Apenas_a_aba_selecionada_fica_ativa()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        Assert.Single(_vm.OpenDocuments, documento => documento.IsActive);
        Assert.True(_vm.SelectedDocument!.IsActive);
    }

    [Fact]
    public async Task Fechar_aba_nao_selecionada_mantem_a_selecao()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        var primeira = _vm.OpenDocuments[0];
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));
        var selecionada = _vm.SelectedDocument;

        _vm.CloseDocument(primeira);

        Assert.Same(selecionada, _vm.SelectedDocument);
        Assert.Single(_vm.OpenDocuments);
    }

    [Fact]
    public async Task Fechar_a_aba_selecionada_cai_na_vizinha_da_direita()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));
        await _vm.LoadFileAsync(Arquivo("c.md", "# C"));
        _vm.SelectedDocument = _vm.OpenDocuments[1];

        _vm.CloseCurrentDocument();

        Assert.Equal("c.md", _vm.SelectedDocument?.FileName);
    }

    [Fact]
    public async Task Fechar_a_ultima_aba_da_direita_cai_na_da_esquerda()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        _vm.CloseCurrentDocument();

        Assert.Equal("a.md", _vm.SelectedDocument?.FileName);
    }

    [Fact]
    public async Task Fechar_todas_as_abas_volta_ao_estado_vazio()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));

        _vm.CloseCurrentDocument();

        Assert.Empty(_vm.OpenDocuments);
        Assert.Null(_vm.SelectedDocument);
        Assert.False(_vm.HasActiveDocument);
    }

    [Fact]
    public async Task Alternar_abas_circula_nos_dois_sentidos()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        _vm.SelectNextDocument();
        Assert.Equal("a.md", _vm.SelectedDocument?.FileName);

        _vm.SelectPreviousDocument();
        Assert.Equal("b.md", _vm.SelectedDocument?.FileName);
    }

    [Fact]
    public async Task Trocar_de_aba_zera_a_busca_do_documento_anterior()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A com termo"));
        _vm.IsSearchVisible = true;
        _busca.ResultadosADevolver = 3;
        _vm.SearchQuery = "termo";
        _vm.ExecuteSearch();
        Assert.Equal(3, _vm.SearchResultCount);

        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        Assert.Equal(0, _vm.SearchResultCount);
        Assert.Equal(-1, _vm.CurrentSearchIndex);
        Assert.Equal(string.Empty, _vm.SearchQuery);
        Assert.False(_vm.IsSearchVisible);
        Assert.True(_busca.LimpezasPedidas > 0);
    }

    [Fact]
    public async Task Fechar_aba_nao_deixa_o_status_falando_da_aba_fechada()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));
        Assert.Contains("b.md", _vm.StatusMessage);

        _vm.CloseCurrentDocument();

        Assert.DoesNotContain("b.md", _vm.StatusMessage);
        Assert.Contains("a.md", _vm.StatusMessage);
    }

    [Fact]
    public async Task Fechar_a_ultima_aba_volta_a_mensagem_de_estado_vazio()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));

        _vm.CloseCurrentDocument();

        Assert.Equal("Abra um arquivo Markdown para começar", _vm.StatusMessage);
    }

    [Fact]
    public async Task Alternar_aba_faz_o_status_falar_da_aba_da_frente()
    {
        await _vm.LoadFileAsync(Arquivo("a.md", "# A"));
        await _vm.LoadFileAsync(Arquivo("b.md", "# B"));

        _vm.SelectNextDocument();

        Assert.Contains("a.md", _vm.StatusMessage);
    }

    [Fact]
    public async Task Arquivo_inexistente_nao_cria_aba_e_reporta_no_status()
    {
        await _vm.LoadFileAsync(_pasta.CaminhoDe("nao-existe.md"));

        Assert.Empty(_vm.OpenDocuments);
        Assert.StartsWith("Erro ao abrir arquivo", _vm.StatusMessage);
    }

    [Fact]
    public async Task Abrir_uma_lista_de_arquivos_cria_uma_aba_por_arquivo_na_ordem()
    {
        await _vm.LoadFilesAsync(new[]
        {
            Arquivo("a.md", "# A"),
            Arquivo("b.md", "# B"),
            Arquivo("c.md", "# C")
        });

        Assert.Equal(new[] { "a.md", "b.md", "c.md" }, _vm.OpenDocuments.Select(documento => documento.FileName));
        Assert.Equal("c.md", _vm.SelectedDocument?.FileName);
    }

    [Fact]
    public async Task Um_caminho_ruim_na_lista_nao_impede_os_demais()
    {
        await _vm.LoadFilesAsync(new[]
        {
            Arquivo("a.md", "# A"),
            _pasta.CaminhoDe("nao-existe.md"),
            Arquivo("c.md", "# C")
        });

        Assert.Equal(new[] { "a.md", "c.md" }, _vm.OpenDocuments.Select(documento => documento.FileName));
    }

    [Fact]
    public async Task Argumento_em_branco_nao_derruba_nem_cria_aba()
    {
        // Path.GetFullPath lança para vazio e para só-espaços; vindo de um lambda de
        // dispatcher, exceção não observada mataria o app na inicialização.
        await _vm.LoadFilesAsync(new[] { string.Empty, "   ", Arquivo("a.md", "# A") });

        Assert.Single(_vm.OpenDocuments);
        Assert.Equal("a.md", _vm.SelectedDocument?.FileName);
    }

    [Fact]
    public async Task Lista_vazia_nao_muda_nada()
    {
        await _vm.LoadFilesAsync(Array.Empty<string>());

        Assert.Empty(_vm.OpenDocuments);
        Assert.False(_vm.HasActiveDocument);
    }

    [Theory]
    [InlineData("--dark")]
    [InlineData("-v")]
    [InlineData("/gate")]
    [InlineData("")]
    [InlineData("   ")]
    public void Argumento_que_nao_e_caminho_de_arquivo_e_descartado(string argumento)
    {
        Assert.Empty(MainViewModel.FilePathArguments(new[] { argumento }));
    }

    [Fact]
    public void Caminho_inexistente_passa_para_virar_erro_visivel()
    {
        var inexistente = _pasta.CaminhoDe("nao-existe.md");

        Assert.Equal(new[] { inexistente }, MainViewModel.FilePathArguments(new[] { inexistente }));
    }

    [Fact]
    public void Buscar_sem_documento_aberto_nao_abre_a_barra()
    {
        _vm.ToggleSearch();

        Assert.False(_vm.IsSearchVisible);
    }

    // ── apoio ────────────────────────────────────────────────────────

    private string Arquivo(string nome, string conteudo) => _pasta.Escrever(nome, conteudo);

    private sealed class SearchViewFake : IDocumentSearchView
    {
        public int ResultadosADevolver { get; set; }

        public int LimpezasPedidas { get; private set; }

        public int Highlight(string term) => ResultadosADevolver;

        public void GoToResult(int index) { }

        public void ClearHighlights() => LimpezasPedidas++;
    }
}
