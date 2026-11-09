using System.Text;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using StudentPerformanceXml.Analyzers;
using StudentPerformanceXml.Models;
using StudentPerformanceXml.Services;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml;

public partial class MainPage : ContentPage
{
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

    private readonly IFileStorage _storage = new LocalDiskStorage();
    private readonly XmlAttributeParser _attributeParser = new();
    private readonly XslTransformer _transformer = new();

    private readonly Dictionary<string, (CheckBox Check, Picker Picker)> _attributeFilters = new();

    private string? _currentXmlPath;
    private string? _currentXmlContent;
    private string _rootName = "StudentPerformance";
    private List<Student>? _lastResults;

    public MainPage()
    {
        InitializeComponent();
        SubjectPicker.SelectedIndexChanged += (_, _) =>
        {
            if (SubjectPicker.SelectedIndex >= 0) SubjectCheckBox.IsChecked = true;
        };
    }


    private async void OnOpenXmlClicked(object sender, EventArgs e)
    {
        try
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Оберіть XML-файл",
                FileTypes = XmlFileType
            });
            if (file is null) return;

            string content = await _storage.ReadAsync(file.FullPath);
            if (!_attributeParser.ValidateDocumentStructure(content, out string error))
            {
                await DisplayAlert("Некоректний файл", error, "OK");
                return;
            }

            _currentXmlPath = file.FullPath;
            _currentXmlContent = content;
            _rootName = _attributeParser.GetRootName(content);

            LoadAvailableAttributes(content);
            ClearOutputAndParameters();
            CurrentFileLabel.Text = file.FileName;
            ResultsLabel.Text = "Файл завантажено. Оберіть параметри та натисніть «Пошук».";
        }
        catch (Exception ex)
        {
            await ShowError(ex.Message);
        }
    }
    private void LoadAvailableAttributes(string xmlContent)
    {
        AttributeFiltersLayout.Children.Clear();
        _attributeFilters.Clear();

        foreach (var (attribute, values) in _attributeParser.ExtractAvailableAttributes(xmlContent))
        {
            var check = new CheckBox { VerticalOptions = LayoutOptions.Center };
            var label = new Label { Text = attribute, WidthRequest = 100, VerticalOptions = LayoutOptions.Center };
            var picker = new Picker { ItemsSource = values, WidthRequest = 260, Title = attribute };
            picker.SelectedIndexChanged += (_, _) =>
            {
                if (picker.SelectedIndex >= 0) check.IsChecked = true;
            };

            var row = new HorizontalStackLayout { Spacing = 6 };
            row.Children.Add(check);
            row.Children.Add(label);
            row.Children.Add(picker);
            AttributeFiltersLayout.Children.Add(row);

            _attributeFilters[attribute] = (check, picker);
        }

        SubjectPicker.ItemsSource = _attributeParser.ExtractSubjectNames(xmlContent);
    }

    private async void OnSearchClicked(object sender, EventArgs e)
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

        try
        {
            IXmlAnalyzer analyzer = CreateSelectedAnalyzer();  
            _lastResults = analyzer.AnalyzeData(_currentXmlContent, parameters);
            DisplayResults(_lastResults, analyzer, parameters);
        }
        catch (Exception ex)
        {
            await ShowError(ex.Message);
        }
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
        return new LinqAnalyzer();
    }

    private void DisplayResults(List<Student> students, IXmlAnalyzer analyzer, SearchParameters parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Метод: {analyzer.MethodName}");
        sb.AppendLine($"Параметри: {parameters}");
        sb.AppendLine($"Знайдено студентів: {students.Count}");
        sb.AppendLine();

        foreach (Student student in students)
            sb.AppendLine(student.ToString());

        ResultsLabel.Text = students.Count == 0
            ? sb + "Нічого не знайдено."
            : sb.ToString();
    }

    private async void OnTransformClicked(object sender, EventArgs e)
    {
        if (_currentXmlContent is null || _currentXmlPath is null)
        {
            await DisplayAlert("Увага", "Спочатку відкрийте XML-файл.", "OK");
            return;
        }

        try
        {
            FileResult? xslFile = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Оберіть XSL-файл",
                FileTypes = XslFileType
            });
            if (xslFile is null) return;

            string xslContent = await _storage.ReadAsync(xslFile.FullPath);

            string sourceXml = _lastResults is null
                ? _currentXmlContent
                : StudentsXmlBuilder.Build(_lastResults, _rootName);

            string html = _transformer.Transform(sourceXml, xslContent);

            string fileName = $"{Path.GetFileNameWithoutExtension(_currentXmlPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string outputPath = Path.Combine(AppPaths.OutputDirectory, fileName);

            if (!await _storage.WriteAsync(outputPath, html))
            {
                await ShowError($"Не вдалося записати файл {outputPath}");
                return;
            }

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
        catch (Exception ex)
        {
            await ShowError(ex.Message);
        }
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
}
