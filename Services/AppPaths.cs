namespace StudentPerformanceXml.Services;

public static class AppPaths
{
    public static string OutputDirectory
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "StudentPerformanceXml");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
