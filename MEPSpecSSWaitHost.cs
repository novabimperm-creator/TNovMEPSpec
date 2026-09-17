using Autodesk.Revit.UI;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TNovMEPSpec
{
    public sealed class MEPSpecSSWaitHost : IDisposable
    {
        MEPSpecSSWaitWPF _window;

        public static MEPSpecSSWaitHost Show(UIApplication uiapp)
        {
            var host = new MEPSpecSSWaitHost();
            IntPtr owner = uiapp != null ? uiapp.MainWindowHandle : IntPtr.Zero;
            var ready = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                var window = new MEPSpecSSWaitWPF();
                host._window = window;
                if (owner != IntPtr.Zero)
                    new WindowInteropHelper(window) { Owner = owner };
                window.Closed += (s, e) =>
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                };
                window.Show();
                ready.Set();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
            return host;
        }

        public void Close()
        {
            MEPSpecSSWaitWPF window = _window;
            if (window == null) return;
            try
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (window.IsLoaded)
                        window.Close();
                });
            }
            catch
            {
            }
            _window = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
