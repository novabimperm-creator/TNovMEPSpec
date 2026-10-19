using System;
using System.Collections.Generic;

namespace TNovMEPSpec
{
    public static class NonModelFamilyNames
    {
        public const string Generated = "pmN.Условное семейство Экземпляр";
        public const string Manual = "pmN.Условное семейство (ОбМод)";
    }

    public class NonModelElementRow
    {
        public string ADSK_Группирование { get; set; }
        public string ADSK_Наименование { get; set; }
        public string ADSK_Марка { get; set; }
        public string ADSK_КодИзделия { get; set; }
        public string ADSK_ЗаводИзготовитель { get; set; }
        public string ADSK_ЕдиницаИзмерения { get; set; }
        public string ADSK_Количество { get; set; }
        public bool GeneratedByRule { get; set; }
        public string RuleId { get; set; }
        public string RuleDescription { get; set; }
        public int ElementId { get; set; }
        public bool AlreadyAdded { get; set; }

        public string CatalogKey()
        {
            return (ADSK_Группирование ?? string.Empty).Trim() + "+" + (ADSK_Наименование ?? string.Empty).Trim();
        }
    }

    public class NonModelCatalogRow
    {
        public string ADSK_Группирование { get; set; }
        public string ADSK_Наименование { get; set; }
        public string ADSK_Марка { get; set; }
        public string ADSK_КодИзделия { get; set; }
        public string ADSK_ЗаводИзготовитель { get; set; }
        public string ADSK_ЕдиницаИзмерения { get; set; }

        public string CatalogKey()
        {
            return (ADSK_Группирование ?? string.Empty).Trim() + "+" + (ADSK_Наименование ?? string.Empty).Trim();
        }
    }
}