namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    using Loupedeck.LoupedeckHelperPlugin.Helpers;
    using Loupedeck.LoupedeckHelperPlugin.State;

    public sealed class MultiWheelFnToggleCommand : PluginMultistateDynamicCommand
    {
        private MultiWheelFnState _fnState;

        static MultiWheelFnToggleCommand()
        {
            DiagnosticLog.Info("[MultiWheelFnToggleCommand] static constructor");
        }

        public MultiWheelFnToggleCommand()
            : base(groupName: "Shared State", displayName: "MultiWheel Fn", description: "Keeps compatible MultiWheel functions active after their action completes")
        {
            DiagnosticLog.Info("[MultiWheelFnToggleCommand] constructor enter");
            PluginLog.Info("[MultiWheelFnToggleCommand] constructor enter");
            try
            {
                this.IsWidget = true;
                DiagnosticLog.Info("[MultiWheelFnToggleCommand] IsWidget set");
                LoupedeckHelperPlugin.PluginReady += this.OnPluginReady;
                DiagnosticLog.Info("[MultiWheelFnToggleCommand] PluginReady subscribed");

                this.AddState("OFF", "MultiWheel Fn\nOFF", "MultiWheel Fn OFF");
                DiagnosticLog.Info("[MultiWheelFnToggleCommand] OFF state added");
                this.AddState("ON", "MultiWheel Fn\nON", "MultiWheel Fn ON");
                DiagnosticLog.Info("[MultiWheelFnToggleCommand] ON state added");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("[MultiWheelFnToggleCommand] constructor failed", ex);
                PluginLog.Error(ex, "[MultiWheelFnToggleCommand] constructor failed");
                throw;
            }

            DiagnosticLog.Info("[MultiWheelFnToggleCommand] constructor completed");
        }

        private void OnPluginReady()
        {
            DiagnosticLog.Info("[MultiWheelFnToggleCommand] OnPluginReady enter");
            try
            {
                this._fnState = LoupedeckHelperPlugin.MultiWheelFnState;
                DiagnosticLog.Info($"[MultiWheelFnToggleCommand] fnState set null={this._fnState == null}");
                this._fnState.Changed += _ => this.SyncStateFromService();
                this.SyncStateFromService();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("[MultiWheelFnToggleCommand] OnPluginReady failed", ex);
                PluginLog.Error(ex, "[MultiWheelFnToggleCommand] OnPluginReady failed");
                throw;
            }

            DiagnosticLog.Info("[MultiWheelFnToggleCommand] OnPluginReady completed");
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
