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

        if (doc.DocumentType is not null)
        {
            error = "Файли з DTD (<!DOCTYPE>) не підтримуються.";
            return false;
        }

        XElement? root = doc.Root;
        if (root is null)
        {
            error = "Файл не містить кореневого елемента.";
            return false;
        }

        if (root.Name.Namespace != XNamespace.None)
        {
            error = "Елементи файлу не повинні мати простору імен (xmlns).";
            return false;
        }

        List<XElement> students = root.Elements().ToList();
        if (students.Count == 0)
        {
            error = $"Кореневий елемент <{root.Name.LocalName}> не містить жодного елемента <{StudentXmlNames.StudentElement}>.";
            return false;
        }

        for (int i = 0; i < students.Count; i++)
        {
            if (!ValidateStudent(students[i], i + 1, out error))
                return false;
        }

        error = "";
        return true;
    }

    private static bool ValidateStudent(XElement student, int number, out string error)
    {
        if (student.Name != StudentXmlNames.StudentElement)
        {
            error = $"Елемент №{number} у корені — <{student.Name.LocalName}>, а очікується <{StudentXmlNames.StudentElement}>.";
            return false;
        }

        List<XAttribute> attributes = student.Attributes().ToList();
        if (attributes.Count == 0)
        {
            error = $"Студент №{number} не має жодного атрибута.";
            return false;
        }

        if (attributes.Any(a => a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None))
        {
            error = $"Студент №{number} має атрибути з простором імен, вони не підтримуються.";
            return false;
        }

        string studentName = (string?)student.Attribute(StudentXmlNames.FullNameAttribute) ?? $"№{number}";

        foreach (XElement subject in student.Elements())
        {
            if (subject.Name != StudentXmlNames.SubjectElement)
            {
                error = $"Студент {studentName} містить елемент <{subject.Name.LocalName}>, а очікується лише <{StudentXmlNames.SubjectElement}>.";
                return false;
            }

            if (subject.HasElements)
            {
                error = $"Дисципліна в студента {studentName} містить вкладені елементи, структура має бути дворівневою.";
                return false;
            }

            string? subjectName = (string?)subject.Attribute(StudentXmlNames.SubjectNameAttribute);
            if (string.IsNullOrWhiteSpace(subjectName))
            {
                error = $"Студент {studentName} має дисципліну без назви (атрибут {StudentXmlNames.SubjectNameAttribute}).";
                return false;
            }

            if (!int.TryParse((string?)subject.Attribute(StudentXmlNames.MarkAttribute), out int mark))
            {
                error = $"Дисципліна «{subjectName}» у студента {studentName} має некоректну оцінку.";
                return false;
            }

            if (mark < StudentXmlNames.MinMarkValue || mark > StudentXmlNames.MaxMarkValue)
            {
                error = $"Дисципліна «{subjectName}» у студента {studentName}: оцінка {mark} поза межами " +
                        $"{StudentXmlNames.MinMarkValue}–{StudentXmlNames.MaxMarkValue}.";
                return false;
            }
        }

        error = "";
        return true;
    }
}
