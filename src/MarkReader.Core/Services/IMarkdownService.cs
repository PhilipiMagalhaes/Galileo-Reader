namespace MarkReader.Core.Services;

public interface IMarkdownService
{
    string LoadMarkdown(string filePath);
    string ConvertToHtml(string markdown);
}
