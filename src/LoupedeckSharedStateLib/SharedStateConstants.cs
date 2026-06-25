namespace Loupedeck.SharedState
{
    using System;

    public static class SharedStateConstants
    {
        public const Int32 DiscoveryVersion = 1;
        public const String ProviderName = "LoupedeckSharedStatePlugin";
        public const String MultiWheelKeepActiveKey = "loupedeck.shared.multiwheel.keep-active";
        public const String DefaultUnixSocketFileName = "loupedeck-shared-state.sock";
        public const String DefaultWindowsPipeName = "loupedeck-shared-state";
    }
}
