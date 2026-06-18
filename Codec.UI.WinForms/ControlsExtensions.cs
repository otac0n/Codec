namespace Codec.UI.WinForms
{
    using System;
    using System.ComponentModel;

    public static class ControlsExtensions
    {
        extension(Control @this)
        {
            private bool DisposedOrDisposing => @this.IsDisposed || @this.Disposing;

            public void InvokeIfNotDisposed(Action action)
            {
                if (!@this.DisposedOrDisposing)
                {
                    @this.InvokeIfRequired(() =>
                    {
                        if (!@this.DisposedOrDisposing)
                        {
                            action();
                        }
                    });
                }
            }
        }

        extension(ISynchronizeInvoke @this)
        {
            /// <summary>
            /// Extension method allowing conditional invoke usage.
            /// </summary>
            /// <param name="this">The object with which to synchronize.</param>
            /// <param name="action">The action to perform.</param>
            public void InvokeIfRequired(MethodInvoker action)
            {
                if (@this.InvokeRequired)
                {
                    try
                    {
                        @this.Invoke(action, []);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
                else
                {
                    action();
                }
            }
        }
    }
}
