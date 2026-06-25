namespace Loupedeck.LoupedeckHelperPlugin.State
{
    using System;

    internal sealed class MultiWheelFnState
    {
        private readonly Object _syncRoot = new();
        private Boolean _isEnabled;

        public event Action<Boolean> Changed;

        public Boolean IsEnabled
        {
            get
            {
                lock (this._syncRoot)
                {
                    return this._isEnabled;
                }
            }
        }

        public Boolean Set(Boolean value)
        {
            lock (this._syncRoot)
            {
                if (this._isEnabled == value)
                {
                    return this._isEnabled;
                }

                this._isEnabled = value;
            }

            this.Changed?.Invoke(value);
            return value;
        }

        public Boolean Toggle()
        {
            var value = !this.IsEnabled;
            return this.Set(value);
        }

        public Boolean Disable() => this.Set(false);
    }
}
