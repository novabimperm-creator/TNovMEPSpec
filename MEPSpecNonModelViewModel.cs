using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using TNovCommon;

namespace TNovMEPSpec
{
    /// <summary>Окно «Немоделируемые»: элементы условных семейств в модели с редактированием параметров.</summary>
    public class MEPSpecNonModelViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private Dictionary<long, NonModelElementRow> _original = new Dictionary<long, NonModelElementRow>();

        public ObservableCollection<NonModelElementRow> Rows { get; } = new ObservableCollection<NonModelElementRow>();
        /// <summary>В модель вносились изменения (транзакции зафиксированы).</summary>
        public bool ModelChanged { get; private set; }

        private string _status = "";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        //строки поиска под заголовками столбцов
        public ICollectionView View { get; }
        private string _fGroup = "", _fNaim = "", _fMark = "", _fCode = "", _fManufacturer = "", _fUnit = "", _fQuantity = "", _fSource = "";
        public string FilterGroup { get => _fGroup; set { _fGroup = value ?? ""; OnFilterChanged(); } }
        public string FilterNaim { get => _fNaim; set { _fNaim = value ?? ""; OnFilterChanged(); } }
        public string FilterMark { get => _fMark; set { _fMark = value ?? ""; OnFilterChanged(); } }
        public string FilterCode { get => _fCode; set { _fCode = value ?? ""; OnFilterChanged(); } }
        public string FilterManufacturer { get => _fManufacturer; set { _fManufacturer = value ?? ""; OnFilterChanged(); } }
        public string FilterUnit { get => _fUnit; set { _fUnit = value ?? ""; OnFilterChanged(); } }
        public string FilterQuantity { get => _fQuantity; set { _fQuantity = value ?? ""; OnFilterChanged(); } }
        public string FilterSource { get => _fSource; set { _fSource = value ?? ""; OnFilterChanged(); } }

        public MEPSpecNonModelViewModel(Document doc)
        {
            _doc = doc;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = o => o is NonModelElementRow r
                && Has(r.ADSK_Группирование, _fGroup) && Has(r.ADSK_Наименование, _fNaim) && Has(r.ADSK_Марка, _fMark)
                && Has(r.ADSK_КодИзделия, _fCode) && Has(r.ADSK_ЗаводИзготовитель, _fManufacturer) && Has(r.ADSK_ЕдиницаИзмерения, _fUnit)
                && Has(r.ADSK_Количество, _fQuantity) && Has(r.SourceText, _fSource);
        }

        static bool Has(string value, string filter) =>
            filter.Length == 0 || (value ?? "").IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        void OnFilterChanged([CallerMemberName] string name = null)
        {
            OnPropertyChanged(name);
            //во время редактирования ячейки Refresh недопустим
            if (View is IEditableCollectionView ev && (ev.IsEditingItem || ev.IsAddingNew)) return;
            View.Refresh();
        }

        /// <summary>Проверка семейств: загрузка с сервера и размещение одного экземпляра в 0,0,0.</summary>
        public void EnsureFamilies()
        {
            var problems = new List<string>();
            bool changed1 = false, changed2 = false;
            using (Transaction t = new Transaction(_doc, "TNov - условные семейства немоделируемых"))
            {
                t.Start();
                try
                {
                    AddProblem(problems, NonModelElementService.EnsurePlaced(_doc, NonModelFamilyNames.Generated, NonModelFamilyNames.GeneratedRfa, out changed1));
                    AddProblem(problems, NonModelElementService.EnsurePlaced(_doc, NonModelFamilyNames.Manual, NonModelFamilyNames.ManualRfa, out changed2));
                }
                catch (Exception ex) { problems.Add(ex.Message); }
                if (changed1 || changed2) { t.Commit(); ModelChanged = true; }
                else t.RollBack();
            }
            if (problems.Count > 0) { Status = string.Join("\n", problems); foreach (var p in problems) Logger.Log(p, 4); }
        }

        static void AddProblem(List<string> problems, string p) { if (p != null) problems.Add(p); }

        public void Reload()
        {
            Rows.Clear();
            _original.Clear();
            foreach (var r in NonModelElementService.Collect(_doc))
            {
                Rows.Add(r);
                _original[r.ElementId] = r.Clone();
            }
        }

        public bool HasEdits => Rows.Any(r => !r.SameValues(_original.TryGetValue(r.ElementId, out var o) ? o : null));

        /// <summary>Строки с некорректным количеством.</summary>
        public List<NonModelElementRow> InvalidRows()
        {
            return Rows.Where(r => !NonModelElementService.TryParseNumber(r.ADSK_Количество, out _)).ToList();
        }

        /// <summary>Записывает изменённые строки в модель. Возвращает их количество.</summary>
        public int SaveEdits()
        {
            List<NonModelElementRow> changed = Rows.Where(r => !r.SameValues(_original.TryGetValue(r.ElementId, out var o) ? o : null)).ToList();
            if (changed.Count == 0) return 0;
            using (Transaction t = new Transaction(_doc, "TNov - правка немоделируемых"))
            {
                t.Start();
                foreach (var r in changed) NonModelElementService.Apply(_doc, r);
                NonModelElementService.SyncManualTypeNames(_doc);
                t.Commit();
            }
            ModelChanged = true;
            Logger.Log("Немоделируемые: изменено элементов " + changed.Count, 1);
            Reload();
            return changed.Count;
        }

        public void MarkModelChanged() { ModelChanged = true; }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>Окно «Каталог немоделируемых»: строки MEPCatalog.json, добавление в модель.</summary>
    public class MEPSpecNonModelCatalogViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private readonly NonModelCatalogService _service;

        public ObservableCollection<NonModelCatalogRow> Rows { get; }
        public ICollectionView View { get; }
        public bool ModelChanged { get; private set; }
        public bool CatalogEdited { get; set; }
        public string CatalogPath => _service.Path;

        private string _filter = "";
        public string Filter { get => _filter; set { _filter = value ?? ""; OnPropertyChanged(); View.Refresh(); } }

        public MEPSpecNonModelCatalogViewModel(Document doc, NonModelCatalogService service)
        {
            _doc = doc;
            _service = service;
            Rows = new ObservableCollection<NonModelCatalogRow>(service.Load());
            Rows.CollectionChanged += (s, e) => CatalogEdited = true;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = o =>
            {
                if (_filter.Length == 0) return true;
                var r = o as NonModelCatalogRow;
                if (r == null) return true;
                string text = string.Join(" ", r.ADSK_Наименование, r.ADSK_Марка, r.ADSK_КодИзделия, r.ADSK_ЗаводИзготовитель);
                return text.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
            };
        }

        /// <summary>«Группирование 1, Группирование 2» → отдельные значения без повторов.</summary>
        public static List<string> SplitGroupings(string text)
        {
            return (text ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();
        }

        /// <summary>По элементу на каждое группирование: один тип, одинаковое количество. Возвращает число созданных.</summary>
        public int AddToModel(NonModelCatalogRow row, IList<string> groupings, string quantity)
        {
            if (row == null) return 0;
            if (string.IsNullOrWhiteSpace(row.ADSK_Наименование)) throw new InvalidOperationException("Не заполнено ADSK_Наименование.");
            if (groupings.Count == 0) groupings = new List<string> { "" };
            using (Transaction t = new Transaction(_doc, "TNov - немоделируемые из каталога"))
            {
                t.Start();
                foreach (string g in groupings) NonModelElementService.AddFromCatalog(_doc, row, g, quantity);
                NonModelElementService.SyncManualTypeNames(_doc);
                t.Commit();
            }
            ModelChanged = true;
            Logger.Log("Немоделируемые: из каталога добавлено " + groupings.Count + " шт. «" + row.CatalogKey() + "» (" + string.Join(", ", groupings) + ")", 1);
            return groupings.Count;
        }

        public void SaveCatalog()
        {
            _service.Save(Rows);
            CatalogEdited = false;
            Logger.Log("Каталог немоделируемых сохранён: " + _service.Path, 1);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
