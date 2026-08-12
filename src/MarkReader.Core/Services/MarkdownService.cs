using Markdig;

namespace MarkReader.Core.Services;

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string LoadMarkdown(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Arquivo não encontrado: {filePath}");
        return File.ReadAllText(filePath);
    }

    public string ConvertToHtml(string markdown)
    {
        return Markdig.Markdown.ToHtml(markdown, _pipeline);
    }
}
