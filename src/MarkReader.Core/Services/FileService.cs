namespace MarkReader.Core.Services;

public class FileService : IFileService
{
    public Task<string?> OpenFileDialogAsync()
    {
        // Dialog is handled at the UI layer (Avalonia StorageProvider)
        // This is a pass-through for testability
        return Task.FromResult<string?>(null);
    }

    public async Task<string> ReadFileAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path) ?? path;
    }

    public long GetFileSize(string path)
    {
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }
}
