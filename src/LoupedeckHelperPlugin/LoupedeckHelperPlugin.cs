namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;
    using System.Reflection;

    using Loupedeck.LoupedeckHelperPlugin.Helpers;
    using Loupedeck.LoupedeckHelperPlugin.State;
    using Loupedeck.SharedState;

    public sealed class LoupedeckHelperPlugin : Plugin
    {
        private readonly MultiWheelFnState _multiWheelFnState = new();
        private SharedStateIpcServer _ipcServer;

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        internal static event Action PluginReady;

        internal static MultiWheelFnState MultiWheelFnState { get; private set; }

        public LoupedeckHelperPlugin()
        {
            PluginLog.Init(this.Log);
            PluginLog.Info($"[LoupedeckHelperPlugin] Starting git {GetGitVersion()} ({GetBuildConfiguration()})");
            PluginResources.Init(this.Assembly);
            MultiWheelFnState = this._multiWheelFnState;
        }

        public override void Load()
        {
            try
            {
                this._ipcServer = new SharedStateIpcServer(this._multiWheelFnState);
                this._ipcServer.Start();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "[LoupedeckHelperPlugin] Shared-state IPC startup failed; loading action with local state only");
                SharedStateDiscovery.Delete();
                this._ipcServer?.Dispose();
                this._ipcServer = null;
            }

            PluginReady?.Invoke();
        }

        public override void Unload()
        {
            this._ipcServer?.Dispose();
            SharedStateDiscovery.Delete();
            base.Unload();
        }

        private static String GetGitVersion()
        {
            var informationalVersion = typeof(LoupedeckHelperPlugin).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "";

            var plusIndex = informationalVersion.LastIndexOf('+');
            var gitVersion = plusIndex >= 0 && plusIndex < informationalVersion.Length - 1
                ? informationalVersion[(plusIndex + 1)..]
                : informationalVersion;

            return gitVersion.Length > 7 ? gitVersion[..7] : gitVersion;
        }

        private static String GetBuildConfiguration() =>
            typeof(LoupedeckHelperPlugin).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()
                ?.Configuration ?? "unknown";
    }
}
