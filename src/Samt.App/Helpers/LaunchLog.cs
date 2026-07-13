namespace Samt_App.Helpers;

internal static class LaunchLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SAMT",
        "launch.log");

    public static void Write(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.Now:O}  {message}{Environment.NewLine}");
        }
        catch
        {
            // Never crash on logging.
        }
    }

    public static string PathText => LogPath;
}
