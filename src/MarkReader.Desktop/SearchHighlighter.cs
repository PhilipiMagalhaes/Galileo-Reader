using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;

namespace MarkReader.Desktop;

/// <summary>Cores do destaque de busca, resolvidas pelo tema ativo.</summary>
/// <param name="Background">Fundo das ocorrências.</param>
/// <param name="Foreground">Texto das ocorrências.</param>
/// <param name="CurrentBackground">Fundo da ocorrência corrente.</param>
/// <param name="CurrentForeground">Texto da ocorrência corrente.</param>
internal sealed record SearchPalette(
    IBrush Background,
    IBrush Foreground,
    IBrush CurrentBackground,
    IBrush CurrentForeground);

/// <summary>
/// Destaca ocorrências no documento renderizado pelo Markdown.Avalonia.
/// O texto ali é desenhado por <see cref="CTextBlock"/> / <see cref="CRun"/> do
/// ColorTextBlock.Avalonia — não por TextBlock/Run do Avalonia. Procurar TextBlock
/// era a razão de a busca "encontrar" sem nunca pintar nada.
/// </summary>
internal sealed class SearchHighlighter
{
    private readonly Func<Control?> _documentRoot;
    private readonly List<BlockSnapshot> _snapshots = new();
    private readonly List<Match> _matches = new();

    private SearchPalette? _palette;
    private int _currentIndex = -1;

    public SearchHighlighter(Func<Control?> documentRoot) => _documentRoot = documentRoot;

    /// <summary>Propriedades de estilo copiadas para os pedaços de um run quebrado.</summary>
    private static readonly AvaloniaProperty[] InlineStyleProperties =
    {
        CInline.BackgroundProperty,
        CInline.ForegroundProperty,
        CInline.FontFamilyProperty,
        CInline.FontSizeProperty,
        CInline.FontStyleProperty,
        CInline.FontWeightProperty,
        CInline.FontStretchProperty,
        CInline.IsUnderlineProperty,
        CInline.IsStrikethroughProperty,
        CInline.TextVerticalAlignmentProperty
    };

    /// <summary>
    /// A paleta inteira entra aqui — inclusive as cores da ocorrência corrente — para que
    /// destacar e navegar nunca misturem cores de temas diferentes.
    /// </summary>
    public int Highlight(string term, SearchPalette palette)
    {
        Clear();
        if (string.IsNullOrEmpty(term)) return 0;

        _palette = palette;

        if (_documentRoot() is not { } root) return 0;

        foreach (var block in root.GetVisualDescendants().OfType<CTextBlock>())
        {
            if (block.Content is not { Count: > 0 } content) continue;
            if (!ContainsTerm(block.Text, term)) continue;

            var snapshot = new BlockSnapshot(block, content.ToArray());
            int before = _matches.Count;

            HighlightInlines(content, term, palette, block, snapshot);

            // Termo que cruza fronteira de formatação (buscar "bold" em "**bo**ld") casa no
            // texto do bloco mas em nenhum run: sem match, não há o que restaurar depois.
            if (_matches.Count > before)
                _snapshots.Add(snapshot);
        }

        return _matches.Count;
    }

    /// <summary>
    /// Marca a ocorrência de índice <paramref name="index"/> como a corrente — devolvendo a
    /// anterior ao destaque comum — e rola até o bloco que a contém.
    /// </summary>
    public void GoToResult(int index)
    {
        if (index < 0 || index >= _matches.Count || _palette is not { } palette) return;

        RepaintCurrentAsNormal();

        _currentIndex = index;
        _matches[index].Paint(palette.CurrentBackground, palette.CurrentForeground);
        _matches[index].Block.BringIntoView();
    }

    private void RepaintCurrentAsNormal()
    {
        if (_currentIndex < 0 || _currentIndex >= _matches.Count || _palette is not { } palette) return;

        _matches[_currentIndex].Paint(palette.Background, palette.Foreground);
    }

    public void Clear()
    {
        // Cada span tem entrada própria com os filhos originais, então a ordem é indiferente.
        foreach (var snapshot in _snapshots)
        {
            foreach (var (span, children) in snapshot.Spans)
                span.Content = children;

            snapshot.Block.Content = new AvaloniaList<CInline>(snapshot.RootContent);
        }

        _snapshots.Clear();
        _matches.Clear();
        _currentIndex = -1;
        _palette = null;
    }

    private void HighlightInlines(
        IList<CInline> inlines,
        string term,
        SearchPalette palette,
        CTextBlock block,
        BlockSnapshot snapshot)
    {
        for (int i = 0; i < inlines.Count; i++)
        {
            switch (inlines[i])
            {
                case CSpan span:
                    var children = span.Content?.ToList() ?? new List<CInline>();
                    snapshot.Spans.Add((span, children.ToArray()));
                    HighlightInlines(children, term, palette, block, snapshot);
                    span.Content = children;
                    break;

                case CRun run when ContainsTerm(run.Text, term):
                    var pieces = SplitOnTerm(run, term, palette, block);
                    inlines.RemoveAt(i);
                    foreach (var piece in pieces)
                        inlines.Insert(i++, piece);
                    i--;
                    break;
            }
        }
    }

    /// <summary>
    /// Quebra o run nas fronteiras do termo. O run original nunca é mutado — os pedaços são
    /// runs novos, e por isso o snapshot restaura o conteúdo exato ao limpar a busca.
    /// </summary>
    private List<CInline> SplitOnTerm(CRun run, string term, SearchPalette palette, CTextBlock block)
    {
        var text = run.Text;
        var pieces = new List<CInline>();
        int cursor = 0;

        for (int found = IndexOfTerm(text, term, 0); found >= 0; found = IndexOfTerm(text, term, cursor))
        {
            if (found > cursor)
                pieces.Add(CopyStyle(run, new CRun { Text = text[cursor..found] }));

            var match = CopyStyle(run, new CRun { Text = text.Substring(found, term.Length) });
            match.Background = palette.Background;
            match.Foreground = palette.Foreground;
            pieces.Add(match);
            _matches.Add(new Match(block, match));

            cursor = found + term.Length;
        }

        if (cursor < text.Length)
            pieces.Add(CopyStyle(run, new CRun { Text = text[cursor..] }));

        return pieces;
    }

    /// <summary>Copia só o que estava explicitamente definido, para não congelar herança de tema.</summary>
    private static CRun CopyStyle(CRun source, CRun target)
    {
        foreach (var property in InlineStyleProperties)
        {
            if (source.IsSet(property))
                target.SetValue(property, source.GetValue(property));
        }

        return target;
    }

    private static bool ContainsTerm(string? text, string term) =>
        !string.IsNullOrEmpty(text) && text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static int IndexOfTerm(string text, string term, int start) =>
        start > text.Length ? -1 : text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);

    private sealed record Match(CTextBlock Block, CRun Run)
    {
        public void Paint(IBrush background, IBrush foreground)
        {
            Run.Background = background;
            Run.Foreground = foreground;
        }
    }

    private sealed record BlockSnapshot(CTextBlock Block, CInline[] RootContent)
    {
        public List<(CSpan Span, CInline[] Children)> Spans { get; } = new();
    }
}
