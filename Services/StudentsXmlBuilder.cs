using System.Xml.Linq;
using StudentPerformanceXml.Models;

namespace StudentPerformanceXml.Services;

public static class StudentsXmlBuilder
{
    public static string Build(IEnumerable<Student> students, string rootName)
    {
        var root = new XElement(rootName,
            students.Select(student => new XElement(StudentXmlNames.StudentElement,
                student.GetAttributeNames().Select(name => new XAttribute(name, student.GetAttribute(name)!)),
                student.GetMarks().Select(mark => new XElement(StudentXmlNames.SubjectElement,
                    new XAttribute(StudentXmlNames.SubjectNameAttribute, mark.GetSubjectName()),
                    new XAttribute(StudentXmlNames.MarkAttribute, mark.GetMark()))))));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return doc.Declaration + Environment.NewLine + doc;
    }
}
