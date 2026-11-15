using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Saving;

public class HtmlSaveManager : SaveManager
{
    private string? _xslContent;

    public HtmlSaveManager(IFileStorage storage) : base(storage)
    {
    }

    public override string FileExtension => ".html";

    public void SetXsl(string xslContent)
    {
        _xslContent = xslContent;
    }

    protected override IDataSaver CreateSaver()
    {
        if (_xslContent is null)
            throw new InvalidOperationException("Для збереження в HTML потрібно вибрати XSL-файл.");

        return new HtmlDataSaver(RootName, _xslContent);
    }
}
