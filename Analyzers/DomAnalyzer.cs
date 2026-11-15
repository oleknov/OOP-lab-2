using System.Xml;
using StudentPerformanceXml.Models;

namespace StudentPerformanceXml.Analyzers;

public class DomAnalyzer : IXmlAnalyzer
{
    public string MethodName => "DOM API";

    public string LastXPath { get; private set; } = "";

    public List<Student> AnalyzeData(string xmlContent, SearchParameters parameters)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);

        string subjectPredicate = BuildSubjectPredicate(parameters);
        string studentPredicate = BuildStudentPredicate(parameters, subjectPredicate);

        LastXPath = "//" + StudentXmlNames.StudentElement + Wrap(studentPredicate);
        string subjectPath = StudentXmlNames.SubjectElement + Wrap(subjectPredicate);

        var result = new List<Student>();
        XmlNodeList? studentNodes = doc.SelectNodes(LastXPath);
        if (studentNodes is null) return result;

        foreach (XmlNode studentNode in studentNodes)
        {
            var student = new Student();
            if (studentNode.Attributes is not null)
            {
                foreach (XmlAttribute attribute in studentNode.Attributes)
                    student.SetAttribute(attribute.Name, attribute.Value);
            }

            XmlNodeList? subjectNodes = studentNode.SelectNodes(subjectPath);
            if (subjectNodes is not null)
            {
                foreach (XmlNode subjectNode in subjectNodes)
                {
                    string name = subjectNode.Attributes?[StudentXmlNames.SubjectNameAttribute]?.Value ?? "";
                    int mark = StudentXmlNames.ParseMark(subjectNode.Attributes?[StudentXmlNames.MarkAttribute]?.Value);
                    student.AddMark(new SubjectMark(name, mark));
                }
            }

            result.Add(student);
        }

        return result;
    }

    private static string BuildStudentPredicate(SearchParameters parameters, string subjectPredicate)
    {
        var conditions = parameters.GetSelectedAttributes()
            .Select(attr => $"@{attr}={ToXPathLiteral(parameters.GetKeyword(attr))}")
            .ToList();

        if (parameters.HasSubjectFilter)
            conditions.Add(StudentXmlNames.SubjectElement + Wrap(subjectPredicate));

        return string.Join(" and ", conditions);
    }

    private static string BuildSubjectPredicate(SearchParameters parameters)
    {
        var conditions = new List<string>();
        if (parameters.SubjectName is not null)
            conditions.Add($"@{StudentXmlNames.SubjectNameAttribute}={ToXPathLiteral(parameters.SubjectName)}");
        if (parameters.MinMark is not null)
            conditions.Add($"@{StudentXmlNames.MarkAttribute} >= {parameters.MinMark}");
        if (parameters.MaxMark is not null)
            conditions.Add($"@{StudentXmlNames.MarkAttribute} <= {parameters.MaxMark}");
        return string.Join(" and ", conditions);
    }

    private static string Wrap(string predicate) => predicate.Length == 0 ? "" : $"[{predicate}]";

    private static string ToXPathLiteral(string value)
    {
        if (!value.Contains('\'')) return $"'{value}'";
        if (!value.Contains('"')) return $"\"{value}\"";
        string[] parts = value.Split('\'');
        return "concat('" + string.Join("', \"'\", '", parts) + "')";
    }
}
