using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Saving;

public class XmlSaveManager : SaveManager
{
    public XmlSaveManager(IFileStorage storage) : base(storage)
    {
    }

    public override string FileExtension => ".xml";

    protected override IDataSaver CreateSaver() => new XmlDataSaver(RootName);
}
