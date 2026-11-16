namespace StudentPerformanceXml.Controllers;

public sealed record LoadedXml(
    string Content,
    string? Error,
    string RootName,
    Dictionary<string, List<string>> Attributes,
    List<string> Subjects);
