namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;
    using System.Reflection;

    using Loupedeck.LoupedeckHelperPlugin.Helpers;
    using Loupedeck.LoupedeckHelperPlugin.State;
#if ENABLE_SHARED_STATE_IPC
    using Loupedeck.SharedState;
#endif

    public sealed class LoupedeckHelperPlugin : Plugin
    {
#if ENABLE_SHARED_STATE_IPC
        private readonly MultiWheelFnState _multiWheelFnState = new();
        private SharedStateIpcServer _ipcServer;
#endif

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        static LoupedeckHelperPlugin()
        {
            DiagnosticLog.Info("[LoupedeckHelperPlugin] static constructor");
        }

        public LoupedeckHelperPlugin()
        {
            DiagnosticLog.Info("[LoupedeckHelperPlugin] constructor enter");
            PluginLog.Init(this.Log);
            PluginLog.Info($"[LoupedeckHelperPlugin] Starting git {GetGitVersion()} ({GetBuildConfiguration()})");
            DiagnosticLog.Info($"[LoupedeckHelperPlugin] constructor after PluginLog.Init git={GetGitVersion()} config={GetBuildConfiguration()}");
            PluginResources.Init(this.Assembly);
            DiagnosticLog.Info($"[LoupedeckHelperPlugin] constructor after PluginResources.Init assembly={this.Assembly.FullName}");
            DiagnosticLog.Info("[LoupedeckHelperPlugin] constructor completed");
        }

        public override void Load()
        {
            DiagnosticLog.Info("[LoupedeckHelperPlugin] Load enter");
            PluginLog.Info("[LoupedeckHelperPlugin] Load enter");
#if ENABLE_SHARED_STATE_IPC
            try
            {
                DiagnosticLog.Info("[LoupedeckHelperPlugin] creating SharedStateIpcServer");
                this._ipcServer = new SharedStateIpcServer(this._multiWheelFnState);
                DiagnosticLog.Info("[LoupedeckHelperPlugin] starting SharedStateIpcServer");
                this._ipcServer.Start();
                DiagnosticLog.Info("[LoupedeckHelperPlugin] SharedStateIpcServer started");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("[LoupedeckHelperPlugin] Shared-state IPC startup failed", ex);
                PluginLog.Error(ex, "[LoupedeckHelperPlugin] Shared-state IPC startup failed; loading action with local state only");
                SharedStateDiscovery.Delete();
                this._ipcServer?.Dispose();
                this._ipcServer = null;
            }
#else
            DiagnosticLog.Info("[LoupedeckHelperPlugin] shared-state IPC disabled at build time");
            PluginLog.Info("[LoupedeckHelperPlugin] Shared-state IPC disabled at build time");
#endif

            DiagnosticLog.Info("[LoupedeckHelperPlugin] Load completed");
            PluginLog.Info("[LoupedeckHelperPlugin] Load completed");
        }

        public override void Unload()
        {
            DiagnosticLog.Info("[LoupedeckHelperPlugin] Unload enter");
#if ENABLE_SHARED_STATE_IPC
            this._ipcServer?.Dispose();
            SharedStateDiscovery.Delete();
#endif
            base.Unload();
            DiagnosticLog.Info("[LoupedeckHelperPlugin] Unload completed");
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
