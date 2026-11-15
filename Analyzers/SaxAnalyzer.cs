using System.Xml;
using StudentPerformanceXml.Models;

namespace StudentPerformanceXml.Analyzers;

public class SaxAnalyzer : IXmlAnalyzer
{
    public string MethodName => "SAX API";

    public List<Student> AnalyzeData(string xmlContent, SearchParameters parameters)
    {
        var result = new List<Student>();
        Student? current = null;

        using var reader = XmlReader.Create(new StringReader(xmlContent));
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == StudentXmlNames.StudentElement)
            {
                current = new Student();
                bool isEmpty = reader.IsEmptyElement;

                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        current.SetAttribute(reader.Name, reader.Value);
                    } while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }

                if (isEmpty)
                {
                    FinishStudent(current, parameters, result);
                    current = null;
                }
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.Name == StudentXmlNames.SubjectElement
                     && current is not null)
            {
                string name = reader.GetAttribute(StudentXmlNames.SubjectNameAttribute) ?? "";
                int mark = StudentXmlNames.ParseMark(reader.GetAttribute(StudentXmlNames.MarkAttribute));

                if (parameters.SubjectMatches(name, mark))
                    current.AddMark(new SubjectMark(name, mark));
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == StudentXmlNames.StudentElement
                     && current is not null)
            {
                FinishStudent(current, parameters, result);
                current = null;
            }
        }

        return result;
    }

    private static void FinishStudent(Student student, SearchParameters parameters, List<Student> result)
    {
        foreach (string attribute in parameters.GetSelectedAttributes())
        {
            if (student.GetAttribute(attribute) != parameters.GetKeyword(attribute))
                return;
        }

        if (parameters.HasSubjectFilter && student.GetMarks().Count == 0)
            return;

        result.Add(student);
    }
}
