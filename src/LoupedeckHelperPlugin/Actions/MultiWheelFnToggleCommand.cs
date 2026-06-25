namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    using Loupedeck.LoupedeckHelperPlugin.Helpers;
    using Loupedeck.LoupedeckHelperPlugin.State;

    public sealed class MultiWheelFnToggleCommand : PluginMultistateDynamicCommand
    {
        private MultiWheelFnState _fnState;

        public MultiWheelFnToggleCommand()
            : base(groupName: "Shared State", displayName: "MultiWheel Fn", description: "Keeps compatible MultiWheel functions active after their action completes")
        {
            this.IsWidget = true;
            LoupedeckHelperPlugin.PluginReady += this.OnPluginReady;

            this.AddState("OFF", "MultiWheel Fn\nOFF", "MultiWheel Fn OFF");
            this.AddState("ON", "MultiWheel Fn\nON", "MultiWheel Fn ON");
        }

        private void OnPluginReady()
        {
            this._fnState = LoupedeckHelperPlugin.MultiWheelFnState;
            this._fnState.Changed += _ => this.SyncStateFromService();
            this.SyncStateFromService();
        }

        protected override void RunCommand(String actionParameter)
        {
            this._fnState?.Toggle();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandImage(actionParameter, imageSize);

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, this._fnState?.IsEnabled == true ? BitmapColor.Red : BitmapColor.Black);
            ButtonVisuals.DrawText(bitmapBuilder, this.GetCurrentState().DisplayName, BitmapColor.White);
            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.GetCurrentState().DisplayName;

        protected override String GetCommandDisplayName(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandDisplayName(actionParameter, imageSize);

        private void SyncStateFromService()
        {
            this.SetCurrentState(this._fnState?.IsEnabled == true ? 1 : 0);
            this.ActionImageChanged();
        }
    }
}
