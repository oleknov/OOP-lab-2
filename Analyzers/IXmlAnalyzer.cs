using StudentPerformanceXml.Models;

namespace StudentPerformanceXml.Analyzers;

public interface IXmlAnalyzer
{
    string MethodName { get; }

    List<Student> AnalyzeData(string xmlContent, SearchParameters parameters);
}
