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

        int total = highlighter.Highlight("busca", Fundo, Frente);

        Assert.Equal(OcorrenciasEsperadas, total);
        Assert.Equal(total, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Highlight_ignora_diferenca_de_maiusculas()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        Assert.Equal(OcorrenciasEsperadas, highlighter.Highlight("BUSCA", Fundo, Frente));
    }

    [AvaloniaFact]
    public void Highlight_de_termo_inexistente_nao_marca_nada()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        Assert.Equal(0, highlighter.Highlight("zzzz", Fundo, Frente));
        Assert.Equal(0, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Clear_devolve_o_texto_original_sem_residuo()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);
        var antes = TextoDoDocumento(viewer);

        highlighter.Highlight("busca", Fundo, Frente);
        highlighter.Clear();

        Assert.Equal(antes, TextoDoDocumento(viewer));
        Assert.Equal(0, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Highlight_repetido_nao_multiplica_as_marcas()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        int primeira = highlighter.Highlight("busca", Fundo, Frente);
        int segunda = highlighter.Highlight("busca", Fundo, Frente);

        Assert.Equal(primeira, segunda);
        Assert.Equal(segunda, ContarMarcados(viewer));
    }

    [AvaloniaFact]
    public void Highlight_preserva_a_formatacao_ao_redor_da_marca()
    {
        var viewer = Renderizar(Documento);
        var highlighter = new SearchHighlighter(() => viewer);

        highlighter.Highlight("busca", Fundo, Frente);

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

    private static int ContarMarcados(Visual raiz) =>
        raiz.GetVisualDescendants().OfType<CTextBlock>()
            .SelectMany(Inlines)
            .OfType<CRun>()
            .Count(run => ReferenceEquals(run.Background, Fundo));

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
