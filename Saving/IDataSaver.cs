using StudentPerformanceXml.Models;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Saving;

public interface IDataSaver
{
    Task<bool> SaveDataAsync(string path, List<Student> students, IFileStorage storage);
}
