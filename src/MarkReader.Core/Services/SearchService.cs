namespace MarkReader.Core.Services;

public class SearchService : ISearchService
{
    public IReadOnlyList<SearchResult> Search(string content, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(content))
            return Array.Empty<SearchResult>();

        var results = new List<SearchResult>();
        var lines = content.Split('\n');
        var comparison = StringComparison.OrdinalIgnoreCase;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            int searchStart = 0;

            while (true)
            {
                int idx = line.IndexOf(query, searchStart, comparison);
                if (idx < 0) break;

                // Build a trimmed context snippet (max 80 chars)
                var snippet = line.Trim();
                if (snippet.Length > 80)
                {
                    int start = Math.Max(0, idx - 20);
                    snippet = (start > 0 ? "…" : "") +
                              line.Substring(start, Math.Min(80, line.Length - start)).Trim() +
                              (start + 80 < line.Length ? "…" : "");
                }

                results.Add(new SearchResult(lineIndex + 1, idx, query.Length, snippet));
                searchStart = idx + query.Length;
            }
        }

        return results;
    }
}
