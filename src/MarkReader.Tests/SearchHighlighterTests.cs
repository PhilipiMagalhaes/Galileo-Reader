using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Markdown.Avalonia;
using MarkReader.Desktop;
using Xunit;

namespace MarkReader.Tests;

public class SearchHighlighterTests
{
    private const string Documento = """
        # Busca no documento

        O termo **busca** aparece aqui em negrito, e a busca aparece de novo no mesmo
        paragrafo. Um `codigo com busca dentro` tambem conta.

        - item com busca na lista
        - item sem o termo

        | coluna | valor |
        |---|---|
        | busca | 42 |

        > citacao com busca dentro

        [link de busca](https://exemplo.com/busca)
        """;

    /// <summary>Total de ocorrências de "busca" no texto renderizado do documento acima.</summary>
    private const int OcorrenciasEsperadas = 8;

    private static readonly IBrush Fundo = new SolidColorBrush(Color.Parse("#FFD54F"));
    private static readonly IBrush Frente = new SolidColorBrush(Colors.Black);
    private static readonly IBrush FundoCorrente = new SolidColorBrush(Color.Parse("#C2410C"));
    private static readonly IBrush FrenteCorrente = new SolidColorBrush(Colors.White);

    private static readonly SearchPalette Paleta = new(Fundo, Frente, FundoCorrente, FrenteCorrente);

    /// <summary>Paleta de um "outro tema", para exercitar o repintar da troca de tema.</summary>
    private static readonly SearchPalette OutraPaleta = new(
        new SolidColorBrush(Color.Parse("#7A5800")),
        new SolidColorBrush(Colors.White),
        new SolidColorBrush(Color.Parse("#F0B457")),
        new SolidColorBrush(Color.Parse("#1A1400")));

    [AvaloniaFact]
    public void Documento_renderizado_usa_CTextBlock_e_nao_TextBlock()
    {
        var viewer = Renderizar(Documento);

        var blocosDeCor = viewer.GetVisualDescendants().OfType<CTextBlock>().Count();
        var blocosDoAvalonia = viewer.GetVisualDescendants().OfType<TextBlock>().Count();

        // Guarda de regressão do bug original: procurar TextBlock não encontra o texto.
        Assert.True(blocosDeCor > blocosDoAvalonia,
            $"esperado o texto em CTextBlock, veio {blocosDeCor} CTextBlock e {blocosDoAvalonia} TextBlock");
    }

    [AvaloniaFact]
    public void Highlight_marca_todas_as_ocorrencias_e_a_contagem_bate_com_as_marcas()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        int total = highlighter.Highlight("busca", Paleta);

        Assert.Equal(OcorrenciasEsperadas, total);
        Assert.Equal(total, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Highlight_ignora_diferenca_de_maiusculas()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        Assert.Equal(OcorrenciasEsperadas, highlighter.Highlight("BUSCA", Paleta));
    }

    [AvaloniaFact]
    public void Highlight_de_termo_inexistente_nao_marca_nada()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        Assert.Equal(0, highlighter.Highlight("zzzz", Paleta));
        Assert.Equal(0, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Clear_devolve_o_texto_original_sem_residuo()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        var antes = TextoDoDocumento(viewer);

        highlighter.Highlight("busca", Paleta);
        highlighter.Clear();

        Assert.Equal(antes, TextoDoDocumento(viewer));
        Assert.Equal(0, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Highlight_repetido_nao_multiplica_as_marcas()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        int primeira = highlighter.Highlight("busca", Paleta);
        int segunda = highlighter.Highlight("busca", Paleta);

        Assert.Equal(primeira, segunda);
        Assert.Equal(segunda, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Highlight_preserva_a_formatacao_ao_redor_da_marca()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        highlighter.Highlight("busca", Paleta);

        // O "busca" em negrito continua dentro do span de negrito depois da quebra do run.
        var negritos = viewer.GetVisualDescendants().OfType<CTextBlock>()
            .SelectMany(Inlines)
            .OfType<CBold>()
            .SelectMany(bold => Inlines(bold.Content))
            .OfType<CRun>()
            .ToList();

        Assert.Contains(negritos, run =>
            run.Text.Equals("busca", StringComparison.OrdinalIgnoreCase) &&
            ReferenceEquals(run.Background, Fundo));
    }

    [AvaloniaFact]
    public void GoToResult_marca_uma_unica_ocorrencia_como_corrente()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);

        highlighter.GoToResult(0);

        Assert.Equal(1, ContarPintados(viewer, FundoCorrente));
        Assert.Equal(OcorrenciasEsperadas - 1, ContarPintados(viewer, Fundo));
    }

    [AvaloniaFact]
    public void GoToResult_devolve_a_ocorrencia_anterior_ao_destaque_comum()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);

        highlighter.GoToResult(0);
        var primeira = Marcados(viewer, FundoCorrente).Single();

        highlighter.GoToResult(1);
        var segunda = Marcados(viewer, FundoCorrente).Single();

        Assert.NotSame(primeira, segunda);
        Assert.Same(Fundo, primeira.Background);
        Assert.Equal(1, ContarPintados(viewer, FundoCorrente));
    }

    [AvaloniaFact]
    public void GoToResult_percorre_ocorrencias_distintas_dentro_do_mesmo_paragrafo()
    {
        // O parágrafo de abertura tem 3 ocorrências; navegar entre elas tem de mover a marca.
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);

        var visitadas = new List<CRun>();
        for (int i = 0; i < 3; i++)
        {
            highlighter.GoToResult(i);
            visitadas.Add(Marcados(viewer, FundoCorrente).Single());
        }

        Assert.Equal(3, visitadas.Distinct().Count());
    }

    [AvaloniaFact]
    public void GoToResult_com_indice_invalido_nao_muda_nada()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);
        highlighter.GoToResult(0);

        highlighter.GoToResult(99);
        highlighter.GoToResult(-1);

        Assert.Equal(1, ContarPintados(viewer, FundoCorrente));
    }

    [AvaloniaFact]
    public void Clear_apaga_tambem_a_marca_da_ocorrencia_corrente()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        var antes = TextoDoDocumento(viewer);

        highlighter.Highlight("busca", Paleta);
        highlighter.GoToResult(2);
        highlighter.Clear();

        Assert.Equal(antes, TextoDoDocumento(viewer));
        Assert.Equal(0, ContarPintados(viewer, FundoCorrente));
        Assert.Equal(0, ContarPintados(viewer, Fundo));
    }

    [AvaloniaFact]
    public void GoToResult_devolve_o_texto_da_ocorrencia_anterior_ao_pincel_comum()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);

        highlighter.GoToResult(0);
        var primeira = Marcados(viewer, FundoCorrente).Single();
        highlighter.GoToResult(1);

        Assert.Same(Fundo, primeira.Background);
        Assert.Same(Frente, primeira.Foreground);
    }

    [AvaloniaFact]
    public void Nova_busca_nao_deixa_ocorrencia_corrente_de_uma_busca_anterior()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);
        highlighter.GoToResult(3);

        highlighter.Highlight("item", Paleta);

        Assert.Equal(0, ContarPintados(viewer, FundoCorrente));
    }

    [AvaloniaFact]
    public void Repintar_com_paleta_nova_move_marca_comum_e_corrente_para_o_tema_novo()
    {
        // É a sequência exata do RepaintHighlights quando o usuário troca de tema.
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        highlighter.Highlight("busca", Paleta);
        highlighter.GoToResult(2);

        int total = highlighter.Highlight("busca", OutraPaleta);
        highlighter.GoToResult(2);

        Assert.Equal(OcorrenciasEsperadas, total);
        Assert.Equal(0, ContarPintados(viewer, Fundo));
        Assert.Equal(0, ContarPintados(viewer, FundoCorrente));
        Assert.Equal(1, ContarPintados(viewer, OutraPaleta.CurrentBackground));
        Assert.Equal(OcorrenciasEsperadas - 1, ContarPintados(viewer, OutraPaleta.Background));
    }

    // ── apoio ────────────────────────────────────────────────────────

    private static MarkdownScrollViewer Renderizar(string markdown)
    {
        var viewer = new MarkdownScrollViewer { Markdown = markdown };
        var window = new Window { Content = viewer, Width = 900, Height = 700 };

        window.Show();
        window.Measure(new Size(900, 700));
        window.Arrange(new Rect(0, 0, 900, 700));
        window.UpdateLayout();

        return viewer;
    }

    private static int ContarMarcados(Visual raiz) => ContarPintados(raiz, Fundo);

    private static int ContarPintados(Visual raiz, IBrush pincel) => Marcados(raiz, pincel).Count;

    private static List<CRun> Marcados(Visual raiz, IBrush pincel) =>
        raiz.GetVisualDescendants().OfType<CTextBlock>()
            .SelectMany(Inlines)
            .OfType<CRun>()
            .Where(run => ReferenceEquals(run.Background, pincel))
            .ToList();

    private static string TextoDoDocumento(Visual raiz) =>
        string.Join("|", raiz.GetVisualDescendants().OfType<CTextBlock>().Select(block => block.Text));

    private static IEnumerable<CInline> Inlines(CTextBlock block) => Inlines(block.Content);

    private static IEnumerable<CInline> Inlines(IEnumerable<CInline>? inlines)
    {
        foreach (var inline in inlines ?? Enumerable.Empty<CInline>())
        {
            yield return inline;

            if (inline is CSpan span)
                foreach (var filho in Inlines(span.Content)) yield return filho;
        }
    }
}
