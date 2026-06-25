namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;

    public sealed class MultiWheelFnState
    {
        private readonly Object _syncRoot = new();
        private Boolean _isEnabled;

        public event Action Changed;

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

        public void Disable()
        {
            this.Set(false);
        }

        public void Set(Boolean value)
        {
            lock (this._syncRoot)
            {
                if (this._isEnabled == value)
                {
                    return;
                }

                this._isEnabled = value;
            }

            this.Changed?.Invoke();
        }
    }
}
