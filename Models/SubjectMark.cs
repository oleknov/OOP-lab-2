namespace StudentPerformanceXml.Models;

public class SubjectMark
{
    private readonly string _subjectName;
    private readonly int _mark;

    public SubjectMark(string subjectName, int mark)
    {
        _subjectName = subjectName;
        _mark = mark;
    }

    public string GetSubjectName() => _subjectName;

    public int GetMark() => _mark;

    public override string ToString() => $"{_subjectName}: {_mark}";
}
