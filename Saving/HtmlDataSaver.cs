using StudentPerformanceXml.Models;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Saving;

public class HtmlDataSaver : IDataSaver
{
    private readonly string _rootName;
    private readonly string _xslContent;
    private readonly XslTransformer _transformer = new();

    public HtmlDataSaver(string rootName, string xslContent)
    {
        _rootName = rootName;
        _xslContent = xslContent;
    }

    public Task<bool> SaveDataAsync(string path, List<Student> students, IFileStorage storage)
    {
        string xml = StudentsXmlBuilder.Build(students, _rootName);
        string html = _transformer.Transform(xml, _xslContent);
        return storage.WriteAsync(path, html);
    }
}
