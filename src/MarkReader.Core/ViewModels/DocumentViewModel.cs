using CommunityToolkit.Mvvm.ComponentModel;

namespace MarkReader.Core.ViewModels;

/// <summary>
/// Um arquivo aberto — uma aba. O conteúdo é imutável: reabrir o mesmo caminho ativa a aba
/// existente em vez de recarregar.
/// </summary>
public partial class DocumentViewModel : ObservableObject
{
    /// <summary>
    /// Cada aba mantém seu próprio visualizador vivo, escondido em vez de destruído —
    /// trocar de aba não deve re-parsear o markdown nem perder onde a leitura parou.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    public string FilePath { get; }

    public string FileName { get; }

    public string MarkdownContent { get; }

    public string FileSizeText { get; }

    public DocumentViewModel(string filePath, string markdownContent, long sizeInBytes)
    {
        // Normalizado: "pasta\a.md" e "pasta\.\a.md" são o mesmo arquivo, não duas abas.
        FilePath = Path.GetFullPath(filePath);
        FileName = Path.GetFileName(filePath);
        MarkdownContent = markdownContent;
        FileSizeText = FormatFileSize(sizeInBytes);
    }

    public bool HasPath(string path) =>
        string.Equals(FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);

    private static string FormatFileSize(long size) =>
        size < 1024 ? $"{size} B"
        : size < 1024 * 1024 ? $"{size / 1024.0:F1} KB"
        : $"{size / (1024.0 * 1024):F1} MB";
}
