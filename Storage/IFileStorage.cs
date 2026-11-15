namespace StudentPerformanceXml.Storage;

public interface IFileStorage
{
    Task<byte[]> ReadBytesAsync(string path);

    Task<bool> WriteAsync(string path, string content);
}
