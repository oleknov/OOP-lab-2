using System.Text;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using StudentPerformanceXml.Analyzers;
using StudentPerformanceXml.Models;
using StudentPerformanceXml.Saving;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml;

public partial class MainPage : ContentPage
{
    private const string LocalDiskOption = "Локальний диск";
    private const string DriveOption = "Google Drive";
    private const string CancelOption = "Скасувати";

    private static readonly FilePickerFileType XmlFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, new[] { ".xml" } },
        { DevicePlatform.MacCatalyst, new[] { "public.xml" } },
        { DevicePlatform.Android, new[] { "text/xml", "application/xml" } },
    });

    private static readonly FilePickerFileType XslFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, new[] { ".xsl", ".xslt" } },
        { DevicePlatform.MacCatalyst, new[] { "public.xml" } },
        { DevicePlatform.Android, new[] { "text/xml", "application/xml", "application/xslt+xml" } },
    });

    private readonly IFileStorage _localStorage = new LocalDiskStorage();
    private readonly GoogleDriveStorage _driveStorage = new();
    private readonly XmlAttributeParser _attributeParser = new();
    private readonly XslTransformer _transformer = new();

    private readonly Dictionary<string, (CheckBox Check, Picker Picker)> _attributeFilters = new();

    private string? _currentFileName;
    private string? _currentXmlContent;
    private string _rootName = "StudentPerformance";
    private List<Student>? _lastResults;
    private bool _isBusy;

    public MainPage()
    {
        InitializeComponent();

        SubjectPicker.SelectedIndexChanged += (_, _) =>
        {
            if (SubjectPicker.SelectedIndex >= 0) SubjectCheckBox.IsChecked = true;
            InvalidateResults();
        };
        SubjectCheckBox.CheckedChanged += (_, _) => InvalidateResults();
        MinMarkEntry.TextChanged += (_, _) => InvalidateResults();
        MaxMarkEntry.TextChanged += (_, _) => InvalidateResults();
    }

    private async void OnOpenXmlClicked(object sender, EventArgs e) => await RunBusyAsync(OpenXmlAsync);

    private async void OnOpenDriveClicked(object sender, EventArgs e) => await RunBusyAsync(OpenXmlFromDriveAsync);

    private async void OnSearchClicked(object sender, EventArgs e) => await RunBusyAsync(SearchAsync);

    private async void OnTransformClicked(object sender, EventArgs e) => await RunBusyAsync(TransformAsync);

    private async void OnSaveXmlClicked(object sender, EventArgs e) => await RunBusyAsync(SaveXmlAsync);

    private async void OnSaveHtmlClicked(object sender, EventArgs e) => await RunBusyAsync(SaveHtmlAsync);

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_isBusy) return;

        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;

        foreach (Button button in new[] { OpenXmlButton, OpenDriveButton, ClearButton, SearchButton, TransformButton, SaveXmlButton, SaveHtmlButton })
            button.IsEnabled = !busy;

        ParametersPanel.InputTransparent = busy;
        BusyIndicator.IsRunning = busy;
        BusyIndicator.IsVisible = busy;
    }

    private async Task OpenXmlAsync()
    {
        FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Оберіть XML-файл",
            FileTypes = XmlFileType
        });
        if (file is null) return;

        byte[] bytes = await _localStorage.ReadBytesAsync(file.FullPath);
        await LoadXmlFileAsync(bytes, file.FileName, file.FileName);
    }

    private async Task OpenXmlFromDriveAsync()
    {
        DriveEntry? entry = await ChooseDriveFileAsync("XML-файли на Google Drive", ".xml");
        if (entry is null) return;

        byte[] bytes = await _driveStorage.ReadBytesAsync(entry.Id);
        await LoadXmlFileAsync(bytes, entry.Name, $"{entry.Name} (Google Drive)");
    }

    private async Task LoadXmlFileAsync(byte[] bytes, string fileName, string displayName)
    {
        LoadedXml loaded = await Task.Run(() => LoadXml(bytes));

        if (loaded.Error is not null)
        {
            await DisplayAlert("Некоректний файл", loaded.Error, "OK");
            return;
        }

        _currentFileName = fileName;
        _currentXmlContent = loaded.Content;
        _rootName = loaded.RootName;

        LoadAvailableAttributes(loaded.Attributes, loaded.Subjects);
        ClearOutputAndParameters();
        CurrentFileLabel.Text = displayName;
        ResultsLabel.Text = "Файл завантажено. Оберіть параметри та натисніть «Пошук».";
    }

    private LoadedXml LoadXml(byte[] bytes)
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

    private void LoadAvailableAttributes(Dictionary<string, List<string>> attributes, List<string> subjects)
    {
        AttributeFiltersLayout.Children.Clear();
        _attributeFilters.Clear();

        foreach (var (attribute, values) in attributes)
        {
            var check = new CheckBox { VerticalOptions = LayoutOptions.Center };
            var label = new Label { Text = attribute, WidthRequest = 100, VerticalOptions = LayoutOptions.Center };
            var picker = new Picker { ItemsSource = values, WidthRequest = 260, Title = attribute };

            picker.SelectedIndexChanged += (_, _) =>
            {
                if (picker.SelectedIndex >= 0) check.IsChecked = true;
                InvalidateResults();
            };
            check.CheckedChanged += (_, _) => InvalidateResults();

            var row = new HorizontalStackLayout { Spacing = 6 };
            row.Children.Add(check);
            row.Children.Add(label);
            row.Children.Add(picker);
            AttributeFiltersLayout.Children.Add(row);

            _attributeFilters[attribute] = (check, picker);
        }

        SubjectPicker.ItemsSource = subjects;
    }

    private void InvalidateResults()
    {
        if (_lastResults is null) return;

        _lastResults = null;
        ResultsLabel.Text = "Параметри пошуку змінено. Натисніть «Пошук», щоб оновити результати." +
                            Environment.NewLine + Environment.NewLine + ResultsLabel.Text;
    }

    private async Task SearchAsync()
    {
        if (_currentXmlContent is null)
        {
            await DisplayAlert("Увага", "Спочатку відкрийте XML-файл.", "OK");
            return;
        }

        SearchParameters parameters;
        try
        {
            parameters = BuildSearchParameters();
        }
        catch (FormatException ex)
        {
            await DisplayAlert("Некоректні параметри", ex.Message, "OK");
            return;
        }

        IXmlAnalyzer analyzer = CreateSelectedAnalyzer();
        string xmlContent = _currentXmlContent;
        List<Student> results = await Task.Run(() => analyzer.AnalyzeData(xmlContent, parameters));

        _lastResults = results;

        EventJournal.GetInstance().RecordEvent(
            $"Параметри: {parameters}. Метод: {analyzer.MethodName}. Знайдено: {results.Count}",
            EventPriority.Filtering);

        DisplayResults(results, analyzer, parameters);
    }

    private SearchParameters BuildSearchParameters()
    {
        var parameters = new SearchParameters();

        foreach (var (attribute, (check, picker)) in _attributeFilters)
        {
            if (check.IsChecked && picker.SelectedItem is string value)
                parameters.AddCriterion(attribute, value);
        }

        if (SubjectCheckBox.IsChecked && SubjectPicker.SelectedItem is string subject)
            parameters.SubjectName = subject;

        parameters.MinMark = ParseOptionalMark(MinMarkEntry.Text, "Оцінка від");
        parameters.MaxMark = ParseOptionalMark(MaxMarkEntry.Text, "Оцінка до");

        if (parameters.MinMark > parameters.MaxMark)
            throw new FormatException("Нижня межа оцінки більша за верхню.");

        return parameters;
    }

    private static int? ParseOptionalMark(string? text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text.Trim(), out int mark)) return mark;
        throw new FormatException($"Поле «{fieldName}» має містити ціле число.");
    }

    private IXmlAnalyzer CreateSelectedAnalyzer()
    {
        if (SaxRadio.IsChecked) return new SaxAnalyzer();
        if (DomRadio.IsChecked) return new DomAnalyzer();
        return new LinqAnalyzer();
    }

    private void DisplayResults(List<Student> students, IXmlAnalyzer analyzer, SearchParameters parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Метод: {analyzer.MethodName}");
        if (analyzer is DomAnalyzer dom)
            sb.AppendLine($"XPath: {dom.LastXPath}");
        sb.AppendLine($"Параметри: {parameters}");
        sb.AppendLine($"Знайдено студентів: {students.Count}");
        sb.AppendLine();

        foreach (Student student in students)
            sb.AppendLine(student.ToString());

        ResultsLabel.Text = students.Count == 0
            ? sb + "Нічого не знайдено."
            : sb.ToString();
    }

    private async Task TransformAsync()
    {
        if (_currentXmlContent is null || _currentFileName is null)
        {
            await DisplayAlert("Увага", "Спочатку відкрийте XML-файл.", "OK");
            return;
        }

        try
        {
            string? xslContent = await PickXslAsync();
            if (xslContent is null) return;

            string sourceXml = _lastResults is null
                ? _currentXmlContent
                : StudentsXmlBuilder.Build(_lastResults, _rootName);

            string html = await Task.Run(() => _transformer.Transform(sourceXml, xslContent));

            string fileName = $"{Path.GetFileNameWithoutExtension(_currentFileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string outputPath = Path.Combine(AppPaths.OutputDirectory, fileName);

            if (!await _localStorage.WriteAsync(outputPath, html))
            {
                await ShowError($"Не вдалося записати файл {outputPath}");
                return;
            }

            EventJournal.GetInstance().RecordEvent($"Збережено у файл {fileName}", EventPriority.Transformation);

            await Launcher.Default.OpenAsync(new OpenFileRequest("HTML", new ReadOnlyFile(outputPath)));
        }
        catch (XsltException ex)
        {
            await ShowError($"Помилка в XSL-файлі: {ex.Message}");
        }
        catch (XmlException ex)
        {
            await ShowError($"Некоректний XML/XSL: {ex.Message}");
        }
    }

    private async Task SaveXmlAsync()
    {
        if (!await EnsureResultsToSaveAsync()) return;

        IFileStorage? storage = await ChooseTargetStorageAsync();
        if (storage is null) return;

        await SaveFilteredAsync(new XmlSaveManager(storage), storage);
    }

    private async Task SaveHtmlAsync()
    {
        if (!await EnsureResultsToSaveAsync()) return;

        IFileStorage? storage = await ChooseTargetStorageAsync();
        if (storage is null) return;

        string? xslContent = await PickXslAsync();
        if (xslContent is null) return;

        var manager = new HtmlSaveManager(storage);
        manager.SetXsl(xslContent);
        await SaveFilteredAsync(manager, storage);
    }

    private async Task<bool> EnsureResultsToSaveAsync()
    {
        if (_currentFileName is null || _lastResults is null)
        {
            await DisplayAlert("Увага", "Спочатку виконайте пошук.", "OK");
            return false;
        }

        if (_lastResults.Count == 0)
        {
            await DisplayAlert("Увага", "Немає даних для збереження: пошук нічого не знайшов.", "OK");
            return false;
        }

        return true;
    }

    private async Task SaveFilteredAsync(SaveManager manager, IFileStorage storage)
    {
        if (_currentFileName is null || _lastResults is null) return;

        bool toDrive = storage is GoogleDriveStorage;

        try
        {
            string fileName = $"{Path.GetFileNameWithoutExtension(_currentFileName)}_filtered_{DateTime.Now:yyyyMMdd_HHmmss}";
            manager.OpenFile(toDrive ? fileName : Path.Combine(AppPaths.OutputDirectory, fileName));
            manager.SetData(_lastResults, _rootName);

            bool saved = await Task.Run(() => manager.SaveAsync());

            if (!saved)
            {
                await ShowError($"Не вдалося записати файл {manager.OpenedFilePath}");
                return;
            }

            string message = toDrive
                ? $"Фрагмент збережено на Google Drive у файл:\n{Path.GetFileName(manager.OpenedFilePath)}"
                : $"Фрагмент збережено у файл:\n{manager.OpenedFilePath}";
            await DisplayAlert("Збережено", message, "OK");
        }
        catch (XsltException ex)
        {
            await ShowError($"Помилка в XSL-файлі: {ex.Message}");
        }
        catch (XmlException ex)
        {
            await ShowError($"Некоректний XSL: {ex.Message}");
        }
    }

    private async Task<string?> PickXslAsync()
    {
        string? source = await ChooseStorageOptionAsync("Звідки взяти XSL-файл?");

        if (source == LocalDiskOption)
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Оберіть XSL-файл",
                FileTypes = XslFileType
            });
            return file is null ? null : XmlFileDecoder.Decode(await _localStorage.ReadBytesAsync(file.FullPath));
        }

        if (source == DriveOption)
        {
            DriveEntry? entry = await ChooseDriveFileAsync("XSL-файли на Google Drive", ".xsl", ".xslt");
            return entry is null ? null : XmlFileDecoder.Decode(await _driveStorage.ReadBytesAsync(entry.Id));
        }

        return null;
    }

    private async Task<IFileStorage?> ChooseTargetStorageAsync()
    {
        string? choice = await ChooseStorageOptionAsync("Куди зберегти фрагмент?");
        return choice switch
        {
            LocalDiskOption => _localStorage,
            DriveOption => _driveStorage,
            _ => null
        };
    }

    private async Task<string?> ChooseStorageOptionAsync(string title)
    {
        string? choice = await DisplayActionSheet(title, CancelOption, null, LocalDiskOption, DriveOption);
        return choice is LocalDiskOption or DriveOption ? choice : null;
    }

    private async Task<DriveEntry?> ChooseDriveFileAsync(string title, params string[] extensions)
    {
        IReadOnlyList<DriveEntry> files = await _driveStorage.ListFilesAsync(extensions);
        if (files.Count == 0)
        {
            await DisplayAlert("Google Drive", $"Файлів {string.Join(", ", extensions)} на Google Drive не знайдено.", "OK");
            return null;
        }

        string[] labels = files
            .Select((f, i) => files.Count(other => other.Name == f.Name) > 1 ? $"{f.Name} [{i + 1}]" : f.Name)
            .ToArray();

        string? choice = await DisplayActionSheet(title, CancelOption, null, labels);
        int index = choice is null ? -1 : Array.IndexOf(labels, choice);
        return index < 0 ? null : files[index];
    }

    private void OnClearClicked(object sender, EventArgs e) => ClearOutputAndParameters();

    private void ClearOutputAndParameters()
    {
        foreach (var (check, picker) in _attributeFilters.Values)
        {
            picker.SelectedIndex = -1;
            check.IsChecked = false;
        }

        SubjectPicker.SelectedIndex = -1;
        SubjectCheckBox.IsChecked = false;
        MinMarkEntry.Text = string.Empty;
        MaxMarkEntry.Text = string.Empty;
        LinqRadio.IsChecked = true;

        ResultsLabel.Text = string.Empty;
        _lastResults = null;
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        bool confirmed = await DisplayAlert(
            "Вихід",
            "Чи дійсно ви хочете завершити роботу з програмою?",
            "Так", "Ні");

        if (confirmed)
            Application.Current?.Quit();
    }

    private Task ShowError(string message) => DisplayAlert("Помилка", message, "OK");

    private sealed record LoadedXml(
        string Content,
        string? Error,
        string RootName,
        Dictionary<string, List<string>> Attributes,
        List<string> Subjects);
}
