using System.Text;

namespace StudentPerformanceXml.Models;

public class Student
{
    private readonly Dictionary<string, string> _attributes = new();
    private readonly List<SubjectMark> _marks = new();

    public void SetAttribute(string name, string value) => _attributes[name] = value;

    public void AddMark(SubjectMark mark) => _marks.Add(mark);

    public string? GetAttribute(string name) =>
        _attributes.TryGetValue(name, out string? value) ? value : null;

    public List<string> GetAttributeNames() => _attributes.Keys.ToList();

    public List<SubjectMark> GetMarks() => _marks;

    public override string ToString()
    {
        var sb = new StringBuilder();
        string title = GetAttribute(StudentXmlNames.FullNameAttribute)
                       ?? _attributes.Values.FirstOrDefault()
                       ?? "(без імені)";
        sb.AppendLine(title);

        foreach (var (name, value) in _attributes)
        {
            if (name == StudentXmlNames.FullNameAttribute) continue;
            sb.AppendLine($"    {name}: {value}");
        }

        if (_marks.Count > 0)
        {
            sb.AppendLine("    Оцінки:");
            foreach (SubjectMark mark in _marks)
                sb.AppendLine($"      • {mark}");
        }

        return sb.ToString();
    }
}
