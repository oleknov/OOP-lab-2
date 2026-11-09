namespace StudentPerformanceXml.Models;

public static class StudentXmlNames
{
    public const string StudentElement = "Student";
    public const string SubjectElement = "Subject";
    public const string SubjectNameAttribute = "Name";
    public const string MarkAttribute = "Mark";
    public const string FullNameAttribute = "FullName";

    public static int ParseMark(string? value) =>
        int.TryParse(value, out int mark) ? mark : 0;
}
