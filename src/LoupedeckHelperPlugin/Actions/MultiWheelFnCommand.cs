namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    public sealed class MultiWheelFnCommand : PluginDynamicCommand
    {
        public MultiWheelFnCommand()
            : base(groupName: "Shared State", displayName: "MultiWheel Fn", description: "Keeps compatible MultiWheel functions active after their action completes")
        {
            this.IsWidget = true;
            LoupedeckHelperPlugin.MultiWheelFnState.Changed += this.OnStateChanged;
        }

        protected override void RunCommand(String actionParameter)
        {
            LoupedeckHelperPlugin.MultiWheelFnState.Toggle();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            bitmapBuilder.FillRectangle(0, 0, imageSize.GetWidth(), imageSize.GetHeight(), LoupedeckHelperPlugin.MultiWheelFnState.IsEnabled ? BitmapColor.Red : BitmapColor.Black);
            bitmapBuilder.DrawText(this.GetCommandDisplayName(actionParameter, imageSize), BitmapColor.White);
            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            LoupedeckHelperPlugin.MultiWheelFnState.IsEnabled ? "MultiWheel Fn\nON" : "MultiWheel Fn\nOFF";

        private void OnStateChanged()
        {
            this.ActionImageChanged();
        }
    }
}
