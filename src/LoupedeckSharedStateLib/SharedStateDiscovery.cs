namespace Loupedeck.SharedState
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.Json;

    public static class SharedStateDiscovery
    {
        private const String DirectoryName = "LoupedeckSharedState";
        private const String FileName = "shared-state.json";

        public static String GetDiscoveryDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, DirectoryName);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", DirectoryName);
            }

            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!String.IsNullOrWhiteSpace(xdgDataHome))
            {
                return Path.Combine(xdgDataHome, DirectoryName);
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", DirectoryName);
        }

        public static String GetDiscoveryFilePath() => Path.Combine(GetDiscoveryDirectory(), FileName);

        public static void Write(SharedStateEndpoint endpoint)
        {
            Directory.CreateDirectory(GetDiscoveryDirectory());
            var info = new SharedStateDiscoveryInfo
            {
                Version = SharedStateConstants.DiscoveryVersion,
                Provider = SharedStateConstants.ProviderName,
                Capabilities = [SharedStateConstants.MultiWheelKeepActiveKey],
                Endpoint = endpoint.RawValue
            };

            File.WriteAllText(GetDiscoveryFilePath(), JsonSerializer.Serialize(info, JsonOptions()));
        }

        public static void Delete()
        {
            try
            {
                File.Delete(GetDiscoveryFilePath());
            }
            catch
            {
            }
        }

        public static Boolean TryRead(out SharedStateEndpoint endpoint)
        {
            endpoint = null;

            try
            {
                var path = GetDiscoveryFilePath();
                if (!File.Exists(path))
                {
                    return false;
                }

                var info = JsonSerializer.Deserialize<SharedStateDiscoveryInfo>(File.ReadAllText(path), JsonOptions());
                if (info?.Version != SharedStateConstants.DiscoveryVersion
                    || !String.Equals(info.Provider, SharedStateConstants.ProviderName, StringComparison.Ordinal)
                    || info.Capabilities?.Contains(SharedStateConstants.MultiWheelKeepActiveKey) != true
                    || String.IsNullOrWhiteSpace(info.Endpoint))
                {
                    return false;
                }

                endpoint = new SharedStateEndpoint(info.Endpoint);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };
    }
}
