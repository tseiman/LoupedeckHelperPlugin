namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    public sealed class MultiWheelFnCommand : PluginDynamicCommand
    {
        public MultiWheelFnCommand()
            : base(groupName: "Shared State", displayName: "MultiWheel Fn", description: "Keeps compatible MultiWheel functions active while pressed")
        {
            this.IsWidget = true;
            LoupedeckHelperPlugin.MultiWheelFnState.Changed += this.OnStateChanged;
        }

        protected override void RunCommand(String actionParameter)
        {
            LoupedeckHelperPlugin.MultiWheelFnState.Disable();
        }

        protected override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            switch (buttonEvent.EventType)
            {
                case DeviceButtonEventType.Press:
                case DeviceButtonEventType.LongPress:
                case DeviceButtonEventType.RepeatPress:
                    LoupedeckHelperPlugin.MultiWheelFnState.Set(true);
                    return true;

                case DeviceButtonEventType.Release:
                    LoupedeckHelperPlugin.MultiWheelFnState.Set(false);
                    return true;

                default:
                    return base.ProcessButtonEvent2(actionParameter, buttonEvent);
            }
        }

        [Obsolete("Compatibility path for older button event routing.")]
        protected override Boolean ProcessButtonEvent(String actionParameter, DeviceButtonEvent buttonEvent)
        {
            LoupedeckHelperPlugin.MultiWheelFnState.Set(buttonEvent.IsPressed);
            return true;
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
