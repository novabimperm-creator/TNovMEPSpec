using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;

namespace TNovMEPSpec
{
    public static class MEPSpecSSPreflightHost
    {
        static MEPSpecSSPreflightWPF _window;

        public static void Show(UIApplication uiapp, IList<MEPSpecIssueRow> rows, string title = null, string header = null)
        {
            MEPSpecSSPreflightRevitBridge.Initialize();

            if (_window != null)
            {
                try { _window.Close(); }
                catch { }
                _window = null;
            }

            var vm = new MEPSpecSSPreflightViewModel(rows, title, header);
            _window = new MEPSpecSSPreflightWPF(vm);
            _window.Closed += (s, e) => { _window = null; };
            new WindowInteropHelper(_window) { Owner = uiapp.MainWindowHandle };
            _window.Show();
        }
    }
}
