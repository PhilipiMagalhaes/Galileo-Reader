namespace MarkReader.Core.Services;

/// <summary>
/// Busca sobre o documento <b>renderizado</b>, implementada pela camada de UI.
/// A contagem exibida vem daqui justamente para bater com o que está destacado na tela —
/// buscar no markdown cru contaria ocorrências em URLs, cercas de código e marcadores.
/// </summary>
public interface IDocumentSearchView
{
    /// <summary>Destaca todas as ocorrências e devolve quantas foram encontradas.</summary>
    int Highlight(string term);

    /// <summary>Traz a ocorrência de índice <paramref name="index"/> para a área visível.</summary>
    void GoToResult(int index);

    /// <summary>Remove os destaques e devolve o documento ao estado original.</summary>
    void ClearHighlights();
}
