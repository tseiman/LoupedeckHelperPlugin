namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    internal static class SharedStateDiscovery
    {
        public const String Key = "loupedeck.shared.multiwheel.keep-active";
        public const String Provider = "LoupedeckSharedStatePlugin";
        private const String DirectoryName = "LoupedeckSharedState";
        private const String DiscoveryFileName = "shared-state.json";
        private const String SocketFileName = "loupedeck-shared-state.sock";
        private const String PipeName = "loupedeck-shared-state";

        public static String Endpoint => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"pipe:{PipeName}"
            : $"unix:{Path.Combine(GetDirectory(), SocketFileName)}";

        public static String UnixSocketPath => Path.Combine(GetDirectory(), SocketFileName);

        public static String Pipe => PipeName;

        public static String FilePath => Path.Combine(GetDirectory(), DiscoveryFileName);

        public static void Write()
        {
            Directory.CreateDirectory(GetDirectory());
            File.WriteAllText(FilePath,
                "{\n"
                + "  \"version\": 1,\n"
                + $"  \"provider\": \"{Provider}\",\n"
                + "  \"capabilities\": [\n"
                + $"    \"{Key}\"\n"
                + "  ],\n"
                + $"  \"endpoint\": \"{EscapeJson(Endpoint)}\"\n"
                + "}\n");
        }

        public static void Delete()
        {
            TryDelete(FilePath);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TryDelete(UnixSocketPath);
            }
        }

        public static String GetDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", DirectoryName);
            }

            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !String.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(xdgDataHome, DirectoryName)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", DirectoryName);
        }

        private static void TryDelete(String path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        private static String EscapeJson(String value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
