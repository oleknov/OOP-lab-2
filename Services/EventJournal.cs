using System.Text;

namespace StudentPerformanceXml.Services;

public sealed class EventJournal
{
    private static EventJournal? _instance;
    private static readonly object InstanceLock = new();

    private readonly string _journalFilePath;
    private readonly object _writeLock = new();

    private EventJournal()
    {
        _journalFilePath = Path.Combine(AppPaths.OutputDirectory, "journal.log");
    }

    public static EventJournal GetInstance()
    {
        lock (InstanceLock)
        {
            return _instance ??= new EventJournal();
        }
    }

    public string JournalFilePath => _journalFilePath;

    public bool RecordEvent(string message, EventPriority priority)
    {
        string line = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} {ToText(priority)}. {message}";
        try
        {
            lock (_writeLock)
            {
                File.AppendAllText(_journalFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ToText(EventPriority priority) => priority switch
    {
        EventPriority.Filtering => "Фільтрація",
        EventPriority.Transformation => "Трансформація",
        EventPriority.Saving => "Збереження",
        _ => priority.ToString()
    };
}
