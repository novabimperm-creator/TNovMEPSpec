using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;

namespace TNovMEPSpec
{
    public static class MEPSpecSSTypePreviewHost
    {
        static MEPSpecSSTypePreviewWPF _window;

        public static void Show(UIApplication uiapp, IList<SSTypePreviewRow> rows, Action onContinue)
        {
            MEPSpecSSPreflightRevitBridge.Initialize();

            if (_window != null)
            {
                if (_window.WindowState == WindowState.Minimized)
                    _window.WindowState = WindowState.Normal;
                _window.Activate();
                return;
            }

            var vm = new MEPSpecSSTypePreviewViewModel(rows, onContinue);
            _window = new MEPSpecSSTypePreviewWPF(vm);
            _window.Closed += (s, e) => { _window = null; };
            new WindowInteropHelper(_window) { Owner = uiapp.MainWindowHandle };
            _window.Show();
        }
    }
}
