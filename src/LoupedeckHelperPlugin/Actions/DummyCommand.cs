namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    public sealed class DummyCommand : PluginDynamicCommand
    {
        public DummyCommand()
            : base(groupName: "Helper", displayName: "Dummy", description: "Dummy action for loader diagnostics")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
        }
    }
}
