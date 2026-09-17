using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TNovMEPSpec
{
    public class MEPSpecSSPreflightViewModel : INotifyPropertyChanged
    {
        public string TitleText { get; }
        public string HeaderText { get; }
        public ObservableCollection<SSCablePreflightRowItem> Rows { get; }
        public ICommand CloseCommand { get; }

        public event EventHandler CloseRequest;

        public MEPSpecSSPreflightViewModel(IList<MEPSpecIssueRow> rows, string title = null, string header = null)
        {
            TitleText = string.IsNullOrWhiteSpace(title) ? "ПРОВЕРКА ПАРАМЕТРОВ КАБЕЛЯ" : title;

            int elementCount = rows == null ? 0 : rows.Sum(r => r.Count);
            int groupCount = rows == null ? 0 : rows.Count;
            HeaderText = string.IsNullOrWhiteSpace(header)
                ? $"Найдено проблемных элементов: {elementCount} (групп: {groupCount}). " +
                  "Исправьте RBZ_Пучок (должен быть заблокирован формулой и заполнен) и запустите снова."
                : header;

            Rows = new ObservableCollection<SSCablePreflightRowItem>(
                (rows ?? new List<MEPSpecIssueRow>()).Select(r => new SSCablePreflightRowItem(r, SelectElements)));

            CloseCommand = new SSPreflightRelayCommand(_ => RaiseCloseRequest());
        }

        void SelectElements(SSCablePreflightRowItem row)
        {
            if (row == null || row.ElementIds == null || row.ElementIds.Count == 0)
                return;
            MEPSpecSSPreflightRevitBridge.SelectElements(row.ElementIds);
        }

        void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SSCablePreflightRowItem
    {
        public string Grouping { get; }
        public string Category { get; }
        public string ProblemParams { get; }
        public int Count { get; }
        public string ElementIdsText { get; }
        public ICollection<ElementId> ElementIds { get; }
        public ICommand SelectCommand { get; }

        public SSCablePreflightRowItem(MEPSpecIssueRow row, Action<SSCablePreflightRowItem> selectAction)
        {
            Grouping = string.IsNullOrEmpty(row.Grouping) ? "(без группирования)" : row.Grouping;
            Category = row.Category ?? "";
            ProblemParams = row.ProblemParams ?? "";
            Count = row.Count;
            ElementIdsText = row.ElementIdsText ?? "";
            ElementIds = row.ElementIds ?? new List<ElementId>();
            SelectCommand = new SSPreflightRelayCommand(_ => selectAction(this));
        }
    }

    public class SSPreflightRelayCommand : ICommand
    {
        readonly Action<object> _execute;

        public SSPreflightRelayCommand(Action<object> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
