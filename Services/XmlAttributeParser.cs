using System.Xml;
using System.Xml.Linq;
using StudentPerformanceXml.Models;

namespace StudentPerformanceXml.Services;

public class XmlAttributeParser
{
    public Dictionary<string, List<string>> ExtractAvailableAttributes(string xmlContent)
    {
        XDocument doc = XDocument.Parse(xmlContent);

        return doc.Descendants(StudentXmlNames.StudentElement)
            .SelectMany(s => s.Attributes())
            .GroupBy(a => a.Name.LocalName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => a.Value).Distinct().OrderBy(v => v).ToList());
    }

    public List<string> ExtractSubjectNames(string xmlContent)
    {
        XDocument doc = XDocument.Parse(xmlContent);

        return doc.Descendants(StudentXmlNames.SubjectElement)
            .Select(s => (string?)s.Attribute(StudentXmlNames.SubjectNameAttribute))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    public string GetRootName(string xmlContent) =>
        XDocument.Parse(xmlContent).Root?.Name.LocalName ?? "Students";

    public bool ValidateDocumentStructure(string xmlContent, out string error)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch (XmlException ex)
        {
            error = $"Файл не є коректним XML: {ex.Message}";
            return false;
        }

        if (!doc.Descendants(StudentXmlNames.StudentElement).Any())
        {
            error = $"У файлі немає жодного елемента <{StudentXmlNames.StudentElement}>.";
            return false;
        }

        foreach (XElement subject in doc.Descendants(StudentXmlNames.SubjectElement))
        {
            if (subject.Attribute(StudentXmlNames.SubjectNameAttribute) is null)
            {
                error = $"Елемент <{StudentXmlNames.SubjectElement}> без атрибута {StudentXmlNames.SubjectNameAttribute}.";
                return false;
            }
            if (!int.TryParse((string?)subject.Attribute(StudentXmlNames.MarkAttribute), out _))
            {
                error = $"Дисципліна «{(string?)subject.Attribute(StudentXmlNames.SubjectNameAttribute)}» має некоректну оцінку.";
                return false;
            }
        }

        error = "";
        return true;
    }
}
