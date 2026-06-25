namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;
    using System.Reflection;

    public sealed class LoupedeckHelperPlugin : Plugin
    {
        public static MultiWheelFnState MultiWheelFnState { get; } = new();

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        public LoupedeckHelperPlugin()
        {
            this.Log.Info($"[LoupedeckHelperPlugin] Starting git {GetGitVersion()} ({GetBuildConfiguration()})");
        }

        public override void Load()
        {
            this.Log.Info("[LoupedeckHelperPlugin] Load");
        }

        public override void Unload()
        {
            this.Log.Info("[LoupedeckHelperPlugin] Unload");
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
