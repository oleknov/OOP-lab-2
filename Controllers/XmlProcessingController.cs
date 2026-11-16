using StudentPerformanceXml.Analyzers;
using StudentPerformanceXml.Models;
using StudentPerformanceXml.Saving;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Controllers;

public sealed class XmlProcessingController
{
    private readonly IFileStorage _localStorage = new LocalDiskStorage();
    private readonly GoogleDriveStorage _driveStorage = new();
    private readonly XmlAttributeParser _attributeParser = new();
    private readonly XslTransformer _transformer = new();

    private string? _currentFileName;
    private string? _currentXmlContent;
    private string _rootName = "StudentPerformance";
    private List<Student>? _lastResults;

    public bool HasFile => _currentXmlContent is not null;

    public Task<IReadOnlyList<DriveEntry>> ListDriveFilesAsync(params string[] extensions) =>
        _driveStorage.ListFilesAsync(extensions);

    public async Task<LoadedXml> OpenXmlAsync(string path, string fileName, bool fromDrive)
    {
        byte[] bytes = await GetStorage(fromDrive).ReadBytesAsync(path);
        LoadedXml loaded = await Task.Run(() => ParseXml(bytes));

        if (loaded.Error is null)
        {
            _currentFileName = fileName;
            _currentXmlContent = loaded.Content;
            _rootName = loaded.RootName;
            _lastResults = null;
        }

        return loaded;
    }

    public async Task<string> ReadXslAsync(string path, bool fromDrive)
    {
        byte[] bytes = await GetStorage(fromDrive).ReadBytesAsync(path);
        return XmlFileDecoder.Decode(bytes);
    }

    public async Task<List<Student>> SearchAsync(IXmlAnalyzer analyzer, SearchParameters parameters)
    {
        string xmlContent = _currentXmlContent
            ?? throw new InvalidOperationException("Спочатку відкрийте XML-файл.");

        List<Student> results = await Task.Run(() => analyzer.AnalyzeData(xmlContent, parameters));
        _lastResults = results;

        EventJournal.GetInstance().RecordEvent(
            $"Параметри: {parameters}. Метод: {analyzer.MethodName}. Знайдено: {results.Count}",
            EventPriority.Filtering);

        return results;
    }

    public bool InvalidateResults()
    {
        if (_lastResults is null) return false;

        _lastResults = null;
        return true;
    }

    public void ClearResults() => _lastResults = null;

    public async Task<string> TransformToHtmlAsync(string xslContent)
    {
        if (_currentXmlContent is null || _currentFileName is null)
            throw new InvalidOperationException("Спочатку відкрийте XML-файл.");

        string sourceXml = _lastResults is null
            ? _currentXmlContent
            : StudentsXmlBuilder.Build(_lastResults, _rootName);

        string html = await Task.Run(() => _transformer.Transform(sourceXml, xslContent));

        string fileName = $"{Path.GetFileNameWithoutExtension(_currentFileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
        string outputPath = Path.Combine(AppPaths.OutputDirectory, fileName);

        if (!await _localStorage.WriteAsync(outputPath, html))
            throw new IOException($"Не вдалося записати файл {outputPath}");

        EventJournal.GetInstance().RecordEvent($"Збережено у файл {fileName}", EventPriority.Transformation);

        return outputPath;
    }

    public string? GetSaveWarning()
    {
        if (_currentFileName is null || _lastResults is null)
            return "Спочатку виконайте пошук.";

        if (_lastResults.Count == 0)
            return "Немає даних для збереження: пошук нічого не знайшов.";

        return null;
    }

    public string CreateFilteredFileName()
    {
        string baseName = Path.GetFileNameWithoutExtension(_currentFileName ?? "students");
        return $"{baseName}_filtered_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    public Task<string> SaveXmlAsync(string targetPath, bool toDrive) =>
        SaveFilteredAsync(new XmlSaveManager(GetStorage(toDrive)), targetPath);

    public Task<string> SaveHtmlAsync(string targetPath, bool toDrive, string xslContent)
    {
        var manager = new HtmlSaveManager(GetStorage(toDrive));
        manager.SetXsl(xslContent);
        return SaveFilteredAsync(manager, targetPath);
    }

    private async Task<string> SaveFilteredAsync(SaveManager manager, string targetPath)
    {
        if (_lastResults is null)
            throw new InvalidOperationException("Спочатку виконайте пошук.");

        manager.OpenFile(targetPath);
        manager.SetData(_lastResults, _rootName);

        bool saved = await Task.Run(() => manager.SaveAsync());
        string savedPath = manager.OpenedFilePath ?? targetPath;

        if (!saved)
            throw new IOException($"Не вдалося записати файл {savedPath}");

        return savedPath;
    }

    private IFileStorage GetStorage(bool drive) => drive ? _driveStorage : _localStorage;

    private LoadedXml ParseXml(byte[] bytes)
    {
        string content = XmlFileDecoder.Decode(bytes);

        if (!_attributeParser.ValidateDocumentStructure(content, out string error))
            return new LoadedXml(content, error, "", new(), new());

        return new LoadedXml(
            content,
            null,
            _attributeParser.GetRootName(content),
            _attributeParser.ExtractAvailableAttributes(content),
            _attributeParser.ExtractSubjectNames(content));
    }
}
