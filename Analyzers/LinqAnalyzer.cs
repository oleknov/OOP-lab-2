using System.Xml.Linq;
using StudentPerformanceXml.Models;

namespace StudentPerformanceXml.Analyzers;

// LINQ to XML: документ завантажується в XDocument, запит нарощується умовами Where залежно від вибраних параметрів.
public class LinqAnalyzer : IXmlAnalyzer
{
    public string MethodName => "LINQ to XML";

    public List<Student> AnalyzeData(string xmlContent, SearchParameters parameters)
    {
        XDocument doc = XDocument.Parse(xmlContent);

        IEnumerable<XElement> query = doc.Descendants(StudentXmlNames.StudentElement);

        foreach (string attribute in parameters.GetSelectedAttributes())
        {
            string keyword = parameters.GetKeyword(attribute);
            query = query.Where(s => (string?)s.Attribute(attribute) == keyword);
        }

        var result =
            from studentElement in query
            let marks = (from subject in studentElement.Elements(StudentXmlNames.SubjectElement)
                         let name = (string?)subject.Attribute(StudentXmlNames.SubjectNameAttribute) ?? ""
                         let mark = StudentXmlNames.ParseMark((string?)subject.Attribute(StudentXmlNames.MarkAttribute))
                         where parameters.SubjectMatches(name, mark)
                         select new SubjectMark(name, mark)).ToList()
            where !parameters.HasSubjectFilter || marks.Count > 0
            select CreateStudent(studentElement, marks);

        return result.ToList();
    }

    private static Student CreateStudent(XElement element, List<SubjectMark> marks)
    {
        var student = new Student();
        foreach (XAttribute attribute in element.Attributes())
            student.SetAttribute(attribute.Name.LocalName, attribute.Value);
        foreach (SubjectMark mark in marks)
            student.AddMark(mark);
        return student;
    }
}
