using System.Text;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using StudentPerformanceXml.Analyzers;
using StudentPerformanceXml.Controllers;
using StudentPerformanceXml.Models;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml;

public partial class MainPage : ContentPage
{
    private readonly XmlProcessingController _controller = new();
    private readonly DialogService _dialogs;

    private readonly Dictionary<string, (CheckBox Check, Picker Picker)> _attributeFilters = new();

    private bool _isBusy;

    public MainPage()
    {
        InitializeComponent();
        _dialogs = new DialogService(this);

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

    private async void OnSaveXmlClicked(object sender, EventArgs e) => await RunBusyAsync(() => SaveFilteredAsync(asHtml: false));

    private async void OnSaveHtmlClicked(object sender, EventArgs e) => await RunBusyAsync(() => SaveFilteredAsync(asHtml: true));

    private void OnClearClicked(object sender, EventArgs e) => ClearOutputAndParameters();

    private async void OnExitClicked(object sender, EventArgs e)
    {
        if (await _dialogs.ConfirmExitAsync())
            Application.Current?.Quit();
    }

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
            await _dialogs.ShowErrorAsync(ex.Message);
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
        FileResult? file = await _dialogs.PickXmlFileAsync();
        if (file is null) return;

        LoadedXml loaded = await _controller.OpenXmlAsync(file.FullPath, file.FileName, fromDrive: false);
        await ShowLoadedXmlAsync(loaded, file.FileName);
    }

    private async Task OpenXmlFromDriveAsync()
    {
        IReadOnlyList<DriveEntry> files = await _controller.ListDriveFilesAsync(".xml");
        DriveEntry? entry = await _dialogs.ChooseDriveFileAsync("XML-файли на Google Drive", files, ".xml");
        if (entry is null) return;

        LoadedXml loaded = await _controller.OpenXmlAsync(entry.Id, entry.Name, fromDrive: true);
        await ShowLoadedXmlAsync(loaded, $"{entry.Name} (Google Drive)");
    }

    private async Task ShowLoadedXmlAsync(LoadedXml loaded, string displayName)
    {
        if (loaded.Error is not null)
        {
            await _dialogs.ShowMessageAsync("Некоректний файл", loaded.Error);
            return;
        }

        LoadAvailableAttributes(loaded.Attributes, loaded.Subjects);
        ClearOutputAndParameters();
        CurrentFileLabel.Text = displayName;
        ResultsLabel.Text = "Файл завантажено. Оберіть параметри та натисніть «Пошук».";
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
        if (!_controller.InvalidateResults()) return;

        ResultsLabel.Text = "Параметри пошуку змінено. Натисніть «Пошук», щоб оновити результати." +
                            Environment.NewLine + Environment.NewLine + ResultsLabel.Text;
    }

    private async Task SearchAsync()
    {
        if (!_controller.HasFile)
        {
            await _dialogs.ShowMessageAsync("Увага", "Спочатку відкрийте XML-файл.");
            return;
        }

        SearchParameters parameters;
        try
        {
            parameters = BuildSearchParameters();
        }
        catch (FormatException ex)
        {
            await _dialogs.ShowMessageAsync("Некоректні параметри", ex.Message);
            return;
        }

        IXmlAnalyzer analyzer = CreateSelectedAnalyzer();
        List<Student> results = await _controller.SearchAsync(analyzer, parameters);

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
        if (!_controller.HasFile)
        {
            await _dialogs.ShowMessageAsync("Увага", "Спочатку відкрийте XML-файл.");
            return;
        }

        try
        {
            string? xslContent = await PickXslAsync();
            if (xslContent is null) return;

            string outputPath = await _controller.TransformToHtmlAsync(xslContent);

            await Launcher.Default.OpenAsync(new OpenFileRequest("HTML", new ReadOnlyFile(outputPath)));
        }
        catch (XsltException ex)
        {
            await _dialogs.ShowErrorAsync($"Помилка в XSL-файлі: {ex.Message}");
        }
        catch (XmlException ex)
        {
            await _dialogs.ShowErrorAsync($"Некоректний XML/XSL: {ex.Message}");
        }
    }

    private async Task SaveFilteredAsync(bool asHtml)
    {
        string? warning = _controller.GetSaveWarning();
        if (warning is not null)
        {
            await _dialogs.ShowMessageAsync("Увага", warning);
            return;
        }

        bool? toDrive = await _dialogs.ChooseStorageAsync("Куди зберегти фрагмент?");
        if (toDrive is null) return;

        try
        {
            string? xslContent = null;
            if (asHtml)
            {
                xslContent = await PickXslAsync();
                if (xslContent is null) return;
            }

            string targetPath = _controller.CreateFilteredFileName();
            if (!toDrive.Value)
            {
                string? folder = await _dialogs.ChooseLocalFolderAsync();
                if (folder is null) return;
                targetPath = Path.Combine(folder, targetPath);
            }

            string savedPath = xslContent is null
                ? await _controller.SaveXmlAsync(targetPath, toDrive.Value)
                : await _controller.SaveHtmlAsync(targetPath, toDrive.Value, xslContent);

            string message = toDrive.Value
                ? $"Фрагмент збережено на Google Drive у файл:\n{Path.GetFileName(savedPath)}"
                : $"Фрагмент збережено у файл:\n{savedPath}";
            await _dialogs.ShowMessageAsync("Збережено", message);
        }
        catch (XsltException ex)
        {
            await _dialogs.ShowErrorAsync($"Помилка в XSL-файлі: {ex.Message}");
        }
        catch (XmlException ex)
        {
            await _dialogs.ShowErrorAsync($"Некоректний XSL: {ex.Message}");
        }
    }

    private async Task<string?> PickXslAsync()
    {
        bool? fromDrive = await _dialogs.ChooseStorageAsync("Звідки взяти XSL-файл?");
        if (fromDrive is null) return null;

        if (fromDrive.Value)
        {
            IReadOnlyList<DriveEntry> files = await _controller.ListDriveFilesAsync(".xsl", ".xslt");
            DriveEntry? entry = await _dialogs.ChooseDriveFileAsync("XSL-файли на Google Drive", files, ".xsl", ".xslt");
            return entry is null ? null : await _controller.ReadXslAsync(entry.Id, fromDrive: true);
        }

        FileResult? file = await _dialogs.PickXslFileAsync();
        return file is null ? null : await _controller.ReadXslAsync(file.FullPath, fromDrive: false);
    }

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
        _controller.ClearResults();
    }
}
