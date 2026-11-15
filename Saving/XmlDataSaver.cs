using StudentPerformanceXml.Models;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Saving;

public class XmlDataSaver : IDataSaver
{
    private readonly string _rootName;

    public XmlDataSaver(string rootName)
    {
        _rootName = rootName;
    }

    public Task<bool> SaveDataAsync(string path, List<Student> students, IFileStorage storage)
    {
        string xml = StudentsXmlBuilder.Build(students, _rootName);
        return storage.WriteAsync(path, xml);
    }
}
