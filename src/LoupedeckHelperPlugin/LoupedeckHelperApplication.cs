namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    public sealed class LoupedeckHelperApplication : ClientApplication
    {
        protected override String GetProcessName() => "";

        protected override String GetBundleName() => "";

        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}
