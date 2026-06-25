namespace Loupedeck.LoupedeckHelperPlugin.Helpers
{
    using System;
    using System.IO;

    internal static class DiagnosticLog
    {
        private const String DirectoryName = "LoupedeckSharedState";
        private const String FileName = "helper-plugin-debug.log";

        public static void Info(String message) => Write("INFO", message, null);

        public static void Error(String message, Exception exception) => Write("ERROR", message, exception);

        private static void Write(String level, String message, Exception exception)
        {
            var line = $"{DateTimeOffset.Now:O} | {level} | {message}";
            if (exception != null)
            {
                line += $" | {exception.GetType().FullName}: {exception.Message} | {exception.StackTrace}";
            }

            foreach (var path in GetCandidatePaths())
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.AppendAllText(path, line + Environment.NewLine);
                    return;
                }
                catch
                {
                }
            }
        }

        private static String[] GetCandidatePaths()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return
            [
                Path.Combine(home, "Library", "Application Support", DirectoryName, FileName),
                Path.Combine(Path.GetTempPath(), FileName)
            ];
        }
    }
}
