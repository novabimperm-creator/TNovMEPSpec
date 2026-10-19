using System.Collections.Generic;

namespace TNovMEPSpec
{
    public sealed class NonModelRuleInfo
    {
        public string Id { get; private set; }
        public string Description { get; private set; }
        public string ExcelFile { get; private set; }
        public NonModelRuleInfo(string id, string description, string excelFile) { Id=id; Description=description; ExcelFile=excelFile; }
    }
    public static class NonModelRuleRegistry
    {
        private static readonly IList<NonModelRuleInfo> _rules = new List<NonModelRuleInfo>
        {
            new NonModelRuleInfo("ROLS", "Расходные материалы для изоляции ROLS по площади и коэффициентам.", "Немоделируемые.xlsx"),
            new NonModelRuleInfo("ET_VENT", "Расходные материалы для изоляции ET Vent по площади.", "Немоделируемые.xlsx"),
            new NonModelRuleInfo("ROLS_SECOND_LAYER", "Материалы второго слоя изоляции ROLS.", "Немоделируемые.xlsx")
        };
        public static IEnumerable<NonModelRuleInfo> All { get { return _rules; } }
        public static NonModelRuleInfo Find(string id) { foreach(var r in _rules) if(r.Id==id) return r; return null; }
    }
}