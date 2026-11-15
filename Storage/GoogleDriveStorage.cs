using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace StudentPerformanceXml.Storage;

public sealed record DriveEntry(string Id, string Name);

public sealed class GoogleDriveStorage : IFileStorage
{
    private const int MaxListPages = 10;
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(2);

    private DriveService? _service;

    public async Task<IReadOnlyList<DriveEntry>> ListFilesAsync(params string[] extensions)
    {
        DriveService drive = await GetServiceAsync();
        var result = new List<DriveEntry>();
        string? pageToken = null;
        int pages = 0;

        do
        {
            FilesResource.ListRequest request = drive.Files.List();
            request.Q = "trashed = false and mimeType != 'application/vnd.google-apps.folder'";
            request.OrderBy = "modifiedTime desc";
            request.PageSize = 100;
            request.Fields = "nextPageToken, files(id, name)";
            request.PageToken = pageToken;

            var response = await request.ExecuteAsync();
            result.AddRange(response.Files
                .Where(f => extensions.Any(ext => f.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .Select(f => new DriveEntry(f.Id, f.Name)));

            pageToken = response.NextPageToken;
            pages++;
        } while (pageToken is not null && pages < MaxListPages);

        return result;
    }

    public async Task<byte[]> ReadBytesAsync(string fileId)
    {
        DriveService drive = await GetServiceAsync();
        using var stream = new MemoryStream();

        IDownloadProgress progress = await drive.Files.Get(fileId).DownloadAsync(stream);
        if (progress.Status == DownloadStatus.Failed) throw progress.Exception;

        return stream.ToArray();
    }

    public async Task<bool> WriteAsync(string path, string content)
    {
        DriveService drive = await GetServiceAsync();
        string name = Path.GetFileName(path);
        string mimeType = GetMimeType(name);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        FilesResource.CreateMediaUpload request =
            drive.Files.Create(new DriveFile { Name = name, MimeType = mimeType }, stream, mimeType);
        request.Fields = "id";

        IUploadProgress progress = await request.UploadAsync();
        if (progress.Status == UploadStatus.Failed) throw progress.Exception;

        return progress.Status == UploadStatus.Completed;
    }

    private async Task<DriveService> GetServiceAsync()
    {
        if (_service is not null) return _service;

        Stream secretsStream;
        try
        {
            secretsStream = await FileSystem.OpenAppPackageFileAsync("credentials.json");
        }
        catch (FileNotFoundException ex)
        {
            throw new InvalidOperationException(
                "Не знайдено credentials.json. Додайте OAuth credentials до Resources/Raw/credentials.json.", ex);
        }

        await using var ownedSecretsStream = secretsStream;
        ClientSecrets secrets = GoogleClientSecrets.FromStream(ownedSecretsStream).Secrets;

        using var timeout = new CancellationTokenSource(AuthorizationTimeout);
        UserCredential credential;
        try
        {
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                [DriveService.Scope.DriveReadonly, DriveService.Scope.DriveFile],
                "user",
                timeout.Token,
                new FileDataStore(Path.Combine(FileSystem.AppDataDirectory, "google-token"), true));
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException("Вхід у Google не завершено. Спробуйте ще раз і підтвердьте доступ у браузері.");
        }

        return _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "StudentPerformanceXml"
        });
    }

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xml" => "text/xml",
            ".html" or ".htm" => "text/html",
            ".xsl" or ".xslt" => "application/xslt+xml",
            _ => "application/octet-stream"
        };
}
