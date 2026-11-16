using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using StudentPerformanceXml.Storage;

namespace StudentPerformanceXml.Services;

public sealed class DialogService
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

    private readonly Page _page;

    public DialogService(Page page)
    {
        _page = page;
    }

    public Task<FileResult?> PickXmlFileAsync() =>
        FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Оберіть XML-файл", FileTypes = XmlFileType });

    public Task<FileResult?> PickXslFileAsync() =>
        FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Оберіть XSL-файл", FileTypes = XslFileType });

    public async Task<string?> ChooseLocalFolderAsync()
    {
        FolderPickerResult result = await FolderPicker.Default.PickAsync(AppPaths.OutputDirectory, CancellationToken.None);
        return result.IsSuccessful ? result.Folder?.Path : null;
    }

    public async Task<bool?> ChooseStorageAsync(string title)
    {
        string? choice = await _page.DisplayActionSheet(title, CancelOption, null, LocalDiskOption, DriveOption);
        return choice switch
        {
            LocalDiskOption => false,
            DriveOption => true,
            _ => null
        };
    }

    public async Task<DriveEntry?> ChooseDriveFileAsync(string title, IReadOnlyList<DriveEntry> files, params string[] extensions)
    {
        if (files.Count == 0)
        {
            await ShowMessageAsync("Google Drive", $"Файлів {string.Join(", ", extensions)} на Google Drive не знайдено.");
            return null;
        }

        string[] labels = files
            .Select((f, i) => files.Count(other => other.Name == f.Name) > 1 ? $"{f.Name} [{i + 1}]" : f.Name)
            .ToArray();

        string? choice = await _page.DisplayActionSheet(title, CancelOption, null, labels);
        int index = choice is null ? -1 : Array.IndexOf(labels, choice);
        return index < 0 ? null : files[index];
    }

    public Task ShowMessageAsync(string title, string message) => _page.DisplayAlert(title, message, "OK");

    public Task ShowErrorAsync(string message) => ShowMessageAsync("Помилка", message);

    public Task<bool> ConfirmExitAsync() =>
        _page.DisplayAlert("Вихід", "Чи дійсно ви хочете завершити роботу з програмою?", "Так", "Ні");
}
