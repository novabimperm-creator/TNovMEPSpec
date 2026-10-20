using System.Collections.Generic;

namespace TNovMEPSpec
{
    public sealed class NonModelRuleInfo
    {
        public string Id { get; private set; }
        public string Description { get; private set; }
        public string ExcelFile { get; private set; }
        public NonModelRuleInfo(string id, string description, string excelFile) { Id = id; Description = description; ExcelFile = excelFile; }

        public string FullText()
        {
            string text = Description;
            if (!string.IsNullOrEmpty(ExcelFile)) text += "\n\nТаблица: " + ExcelFile;
            return text;
        }
    }

    public static class NonModelRuleRegistry
    {
        private static readonly IList<NonModelRuleInfo> _rules = new List<NonModelRuleInfo>
        {
            new NonModelRuleInfo("ROLS",
                "Изоляция ROLS (воздуховоды и трубы): клей, разбавитель, ленты и покровные материалы. " +
                "Количество = количество изоляции × коэффициент из таблицы; для коэффициента 999 — по формулам ROLS " +
                "по наружному диаметру, толщине и длине изоляции (ед. «м» — ленты, «л» — клей и разбавитель, " +
                "«м²» — покровный материал, если у изоляции включён «Покровный материал»).",
                "Немоделируемые.xlsx"),
            new NonModelRuleInfo("ET_VENT",
                "Изоляция ET Vent (воздуховоды): расходные материалы по площади изоляции и коэффициентам таблицы.",
                "Немоделируемые.xlsx"),
            new NonModelRuleInfo("ROLS_SECOND_LAYER",
                "Второй слой изоляции ROLS: отдельная строка на ADSK_Группирование + Принадлежность системы + наименование, " +
                "наименование и артикул по типу изоляции и толщине второго слоя.",
                "ROLS_Изоляция.xlsx"),
            new NonModelRuleInfo("OTHER",
                "Элемент создан генерацией немоделируемых (семейство pmN.Условное семейство Экземпляр) и перезаполняется при запуске Сводной спеки.",
                ""),
        };
        public static IEnumerable<NonModelRuleInfo> All { get { return _rules; } }
        public static NonModelRuleInfo Find(string id) { foreach (var r in _rules) if (r.Id == id) return r; return null; }

        /// <summary>Правило по имени типа сгенерированного кубика (типы называются по типу изоляции из таблицы).</summary>
        public static NonModelRuleInfo ByTypeName(string typeName)
        {
            string t = typeName ?? "";
            if (t.Contains("Второй слой")) return Find("ROLS_SECOND_LAYER");
            if (t.Contains("ET Vent")) return Find("ET_VENT");
            if (t.Contains("ROLS")) return Find("ROLS");
            return Find("OTHER");
        }
    }
}
