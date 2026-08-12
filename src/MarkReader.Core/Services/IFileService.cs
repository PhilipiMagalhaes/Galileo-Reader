namespace MarkReader.Core.Services;

public interface IFileService
{
    Task<string?> OpenFileDialogAsync();
    Task<string> ReadFileAsync(string path);
    string GetFileName(string path);
    long GetFileSize(string path);
}
