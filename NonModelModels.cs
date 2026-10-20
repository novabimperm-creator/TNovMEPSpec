using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TNovMEPSpec
{
    public static class NonModelFamilyNames
    {
        //генерация по правилам (IsolNonModel, второй слой ROLS)
        public const string Generated = "pmN.Условное семейство Экземпляр";
        //ручные элементы из окна «Немоделируемые» и каталога
        public const string Manual = "pmN.Условное семейство (ОбМод)";
        //файлы семейств для загрузки, если их нет в проекте (относительно TNovPaths.BimLibrary)
        public const string GeneratedRfa = @"ВК_ОВ_Семейства\pmN.Условное семейство Экземпляр (ОбМод).rfa";
        public const string ManualRfa = @"ВК_ОВ_Семейства\pmN.Условное семейство (ОбМод).rfa";

        public static bool IsGenerated(string familyName) => familyName != null && familyName.Contains(Generated);
        public static bool IsManual(string familyName) => familyName != null && familyName.Contains(Manual);
    }

    /// <summary>Строка окна «Немоделируемые» — экземпляр условного семейства в модели.</summary>
    public class NonModelElementRow : INotifyPropertyChanged
    {
        private string _group, _naim, _mark, _code, _manufacturer, _unit, _quantity;
        public string ADSK_Группирование { get => _group; set { _group = value; OnPropertyChanged(); } }
        public string ADSK_Наименование { get => _naim; set { _naim = value; OnPropertyChanged(); } }
        public string ADSK_Марка { get => _mark; set { _mark = value; OnPropertyChanged(); } }
        public string ADSK_КодИзделия { get => _code; set { _code = value; OnPropertyChanged(); } }
        public string ADSK_ЗаводИзготовитель { get => _manufacturer; set { _manufacturer = value; OnPropertyChanged(); } }
        public string ADSK_ЕдиницаИзмерения { get => _unit; set { _unit = value; OnPropertyChanged(); } }
        public string ADSK_Количество { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }
        public bool GeneratedByRule { get; set; }
        public string RuleId { get; set; }
        public string RuleDescription { get; set; }
        public long ElementId { get; set; }
        public string TypeName { get; set; }
        public string SourceText => GeneratedByRule ? "генерируется по правилам" : "вручную";

        public NonModelElementRow Clone() => (NonModelElementRow)MemberwiseClone();

        public bool SameValues(NonModelElementRow o)
        {
            return o != null && ADSK_Группирование == o.ADSK_Группирование && ADSK_Наименование == o.ADSK_Наименование
                && ADSK_Марка == o.ADSK_Марка && ADSK_КодИзделия == o.ADSK_КодИзделия && ADSK_ЗаводИзготовитель == o.ADSK_ЗаводИзготовитель
                && ADSK_ЕдиницаИзмерения == o.ADSK_ЕдиницаИзмерения && ADSK_Количество == o.ADSK_Количество;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>Строка каталога немоделируемых (MEPCatalog.json): без группирования и количества — их задают при добавлении.</summary>
    public class NonModelCatalogRow
    {
        public string ADSK_Наименование { get; set; }
        public string ADSK_Марка { get; set; }
        public string ADSK_КодИзделия { get; set; }
        public string ADSK_ЗаводИзготовитель { get; set; }
        public string ADSK_ЕдиницаИзмерения { get; set; }

        //строки каталога различаются по наименованию
        public string CatalogKey() => (ADSK_Наименование ?? string.Empty).Trim();
    }
}
