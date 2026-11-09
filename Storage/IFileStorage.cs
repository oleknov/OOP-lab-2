namespace StudentPerformanceXml.Storage;

public interface IFileStorage
{
    Task<string> ReadAsync(string path);

    Task<bool> WriteAsync(string path, string content);
}
