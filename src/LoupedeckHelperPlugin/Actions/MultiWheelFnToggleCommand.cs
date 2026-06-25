namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    using Loupedeck.LoupedeckHelperPlugin.Helpers;

    public sealed class MultiWheelFnToggleCommand : PluginDynamicCommand
    {
        static MultiWheelFnToggleCommand()
        {
            DiagnosticLog.Info("[MultiWheelFnToggleCommand] static constructor");
        }

        public MultiWheelFnToggleCommand()
            : base(groupName: "Shared State", displayName: "MultiWheel Fn", description: "Keeps compatible MultiWheel functions active after their action completes")
        {
            DiagnosticLog.Info("[MultiWheelFnToggleCommand] constructor enter");
            PluginLog.Info("[MultiWheelFnToggleCommand] constructor enter");
            this.IsWidget = true;
            DiagnosticLog.Info("[MultiWheelFnToggleCommand] constructor completed");
        }

        protected override void RunCommand(String actionParameter)
        {
            PluginLog.Info("[MultiWheelFnToggleCommand] RunCommand");
        }
    }
}
