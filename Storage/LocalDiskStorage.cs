using System.Text;

namespace StudentPerformanceXml.Storage;

public class LocalDiskStorage : IFileStorage
{
    public Task<byte[]> ReadBytesAsync(string path) => File.ReadAllBytesAsync(path);

    public async Task<bool> WriteAsync(string path, string content)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
