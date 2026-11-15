using StudentPerformanceXml.Models;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Saving;

public abstract class SaveManager
{
    private readonly IFileStorage _storage;
    private string? _openedFilePath;
    private List<Student> _data = new();

    protected SaveManager(IFileStorage storage)
    {
        _storage = storage;
    }

    public abstract string FileExtension { get; }

    public string? OpenedFilePath => _openedFilePath;

    protected string RootName { get; private set; } = "Students";

    public void OpenFile(string path)
    {
        _openedFilePath = Path.ChangeExtension(path, FileExtension);
    }

    public void SetData(List<Student> students, string rootName)
    {
        _data = students;
        RootName = rootName;
    }

    public async Task<bool> SaveAsync()
    {
        if (_openedFilePath is null)
            throw new InvalidOperationException("Файл для збереження не відкрито.");

        IDataSaver saver = CreateSaver();
        bool saved = await saver.SaveDataAsync(_openedFilePath, _data, _storage);

        if (saved)
        {
            EventJournal.GetInstance().RecordEvent(
                $"Збережено відфільтрований фрагмент у файл {Path.GetFileName(_openedFilePath)}",
                EventPriority.Saving);
        }

        return saved;
    }

    protected abstract IDataSaver CreateSaver();
}
