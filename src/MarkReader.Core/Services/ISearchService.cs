namespace MarkReader.Core.Services;

public record SearchResult(int LineNumber, int StartIndex, int Length, string ContextSnippet);

public interface ISearchService
{
    IReadOnlyList<SearchResult> Search(string content, string query);
}
