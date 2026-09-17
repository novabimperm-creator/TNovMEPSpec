using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TNovCommon;

namespace TNovMEPSpec
{
    public class MEPSpecSSTypePreviewViewModel
    {
        public string HeaderText { get; }
        public ObservableCollection<SSTypePreviewRowItem> ConduitRows { get; }
        public ObservableCollection<SSTypePreviewRowItem> CableTrayRows { get; }
        public ICommand ContinueCommand { get; }
        public ICommand CancelCommand { get; }

        public event EventHandler ContinueRequest;
        public event EventHandler CloseRequest;

        readonly Action _onContinue;
        bool _finished;

        public MEPSpecSSTypePreviewViewModel(IList<SSTypePreviewRow> rows, Action onContinue)
        {
            _onContinue = onContinue;

            ConduitRows = new ObservableCollection<SSTypePreviewRowItem>(
                (rows ?? new List<SSTypePreviewRow>())
                    .Where(r => r != null && r.Category == "Короб")
                    .OrderBy(r => r.Grouping ?? "", StringComparer.CurrentCulture)
                    .Select(r => new SSTypePreviewRowItem(r, SelectElements)));

            CableTrayRows = new ObservableCollection<SSTypePreviewRowItem>(
                (rows ?? new List<SSTypePreviewRow>())
                    .Where(r => r != null && r.Category == "Лоток")
                    .OrderBy(r => r.Grouping ?? "", StringComparer.CurrentCulture)
                    .Select(r => new SSTypePreviewRowItem(r, SelectElements)));

            HeaderText =
                $"Типов коробов: {ConduitRows.Count}, типов лотков: {CableTrayRows.Count}. " +
                "Проверьте параметры и при необходимости выделите элементы в модели. «Далее» запускает обработку.";

            ContinueCommand = new RelayCommand(_ => Continue(), _ => !_finished);
            CancelCommand = new RelayCommand(_ => Cancel(), _ => !_finished);
        }

        void SelectElements(SSTypePreviewRowItem row)
        {
            if (row == null || row.ElementIds == null || row.ElementIds.Count == 0)
                return;
            MEPSpecSSPreflightRevitBridge.SelectElements(row.ElementIds);
        }

        void Continue()
        {
            if (_finished) return;
            _finished = true;
            _onContinue?.Invoke();
            ContinueRequest?.Invoke(this, EventArgs.Empty);
        }

        public void OnWindowClosed()
        {
            if (_finished) return;
            _finished = true;
            Logger.Log("Запуск транзакций СС отменен пользователем на окне типов.", 3);
        }

        void Cancel()
        {
            if (_finished) return;
            _finished = true;
            Logger.Log("Запуск транзакций СС отменен пользователем на окне типов.", 3);
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }

    public class SSTypePreviewRowItem
    {
        public string Category { get; }
        public string Grouping { get; }
        public string Title { get; }
        public int Count { get; }
        public ObservableCollection<SSTypeParamItem> Parameters { get; }
        public ICollection<ElementId> ElementIds { get; }
        public ICommand SelectCommand { get; }

        public SSTypePreviewRowItem(SSTypePreviewRow row, Action<SSTypePreviewRowItem> selectAction)
        {
            Category = row.Category ?? "";
            Grouping = string.IsNullOrEmpty(row.Grouping) ? "(без группирования)" : row.Grouping;
            Count = row.Count;
            Title = Grouping + "  ·  " + Count.ToString() + " шт.";
            Parameters = new ObservableCollection<SSTypeParamItem>(row.Parameters ?? new List<SSTypeParamItem>());
            ElementIds = row.ElementIds ?? new List<ElementId>();
            SelectCommand = new RelayCommand(_ => selectAction(this), _ => true);
        }
    }
}
