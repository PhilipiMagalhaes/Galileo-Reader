using MarkReader.Core.Services;

namespace MarkReader.Tests;

internal sealed class MarkdownServiceFake : IMarkdownService
{
    public string LoadMarkdown(string filePath) => File.ReadAllText(filePath);

    public string ConvertToHtml(string markdown) => markdown;
}

internal sealed class SettingsServiceFake : ISettingsService
{
    public bool IsDarkTheme { get; set; }

    public void Save() { }

    public void Load() { }
}

/// <summary>Diretório descartável para os arquivos de cada teste.</summary>
internal sealed class PastaTemporaria : IDisposable
{
    private readonly string _caminho;

    public PastaTemporaria(string prefixo)
    {
        _caminho = Path.Combine(Path.GetTempPath(), $"{prefixo}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_caminho);
    }

    public string Escrever(string nome, string conteudo)
    {
        var caminho = Path.Combine(_caminho, nome);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    public string CaminhoDe(string nome) => Path.Combine(_caminho, nome);

    public void Dispose()
    {
        if (Directory.Exists(_caminho)) Directory.Delete(_caminho, recursive: true);
    }
}
