namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;
    using System.Reflection;

    public sealed class LoupedeckHelperPlugin : Plugin
    {
        public static MultiWheelFnState MultiWheelFnState { get; } = new();

        private SharedStateIpcServer _ipcServer;

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        public LoupedeckHelperPlugin()
        {
            this.Log.Info($"[LoupedeckHelperPlugin] Starting git {GetGitVersion()} ({GetBuildConfiguration()})");
        }

        public override void Load()
        {
            this.Log.Info("[LoupedeckHelperPlugin] Load");
            try
            {
                this._ipcServer = new SharedStateIpcServer(
                    MultiWheelFnState,
                    message => this.Log.Info(message),
                    (exception, message) => this.Log.Error(exception, message));
                this._ipcServer.Start();
            }
            catch (Exception ex)
            {
                this.Log.Error(ex, "[LoupedeckHelperPlugin] Shared-state IPC startup failed");
                this._ipcServer?.Dispose();
                this._ipcServer = null;
            }
        }

        public override void Unload()
        {
            this.Log.Info("[LoupedeckHelperPlugin] Unload");
            this._ipcServer?.Dispose();
            this._ipcServer = null;
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
