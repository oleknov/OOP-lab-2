namespace StudentPerformanceXml.Models;

public class SearchParameters
{
    private readonly Dictionary<string, string> _criteria = new();

    public string? SubjectName { get; set; }
    public int? MinMark { get; set; }
    public int? MaxMark { get; set; }

    public void AddCriterion(string attribute, string value) => _criteria[attribute] = value;

    public List<string> GetSelectedAttributes() => _criteria.Keys.ToList();

    public string GetKeyword(string attribute) => _criteria[attribute];

    public bool HasSubjectFilter => SubjectName is not null || MinMark is not null || MaxMark is not null;

    public bool MarkMatches(int mark) =>
        (MinMark is null || mark >= MinMark) && (MaxMark is null || mark <= MaxMark);

    public bool SubjectMatches(string subjectName, int mark) =>
        (SubjectName is null || subjectName == SubjectName) && MarkMatches(mark);

    public override string ToString()
    {
        var parts = _criteria.Select(c => $"«{c.Key}» – {c.Value}").ToList();
        if (SubjectName is not null) parts.Add($"«Дисципліна» – {SubjectName}");
        if (MinMark is not null || MaxMark is not null)
        {
            string range = (MinMark, MaxMark) switch
            {
                (not null, not null) => $"{MinMark}–{MaxMark}",
                (not null, null) => $"від {MinMark}",
                _ => $"до {MaxMark}"
            };
            parts.Add($"«Оцінка» – {range}");
        }
        return parts.Count == 0 ? "без параметрів" : string.Join(", ", parts);
    }
}
