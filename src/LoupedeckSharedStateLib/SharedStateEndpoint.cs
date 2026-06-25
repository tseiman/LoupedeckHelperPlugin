namespace Loupedeck.SharedState
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    public sealed class SharedStateEndpoint
    {
        public SharedStateEndpoint(String rawValue)
        {
            this.RawValue = rawValue ?? "";
        }

        public String RawValue { get; }

        public Boolean IsUnix => this.RawValue.StartsWith("unix:", StringComparison.OrdinalIgnoreCase);

        public Boolean IsNamedPipe => this.RawValue.StartsWith("pipe:", StringComparison.OrdinalIgnoreCase);

        public String Address => this.IsUnix || this.IsNamedPipe
            ? this.RawValue.Substring(this.RawValue.IndexOf(':') + 1)
            : this.RawValue;

        public static SharedStateEndpoint CreateDefault()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new SharedStateEndpoint($"pipe:{SharedStateConstants.DefaultWindowsPipeName}");
            }

            return new SharedStateEndpoint($"unix:{Path.Combine(SharedStateDiscovery.GetDiscoveryDirectory(), SharedStateConstants.DefaultUnixSocketFileName)}");
        }
    }
}
