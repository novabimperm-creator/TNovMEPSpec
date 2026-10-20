using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using TNovCommon;

namespace TNovMEPSpec
{
    /// <summary>
    /// Немоделируемые элементы в модели: экземпляры «pmN.Условное семейство Экземпляр» (генерация по правилам)
    /// и «pmN.Условное семейство (ОбМод)» (ручные, из каталога). Методы, меняющие модель, вызываются внутри транзакции.
    /// </summary>
    public static class NonModelElementService
    {
        public const string ParamGroup = "ADSK_Группирование";
        public const string ParamNaim = "ADSK_Наименование";
        public const string ParamMark = "ADSK_Марка";
        public const string ParamCode = "ADSK_Код изделия";
        public const string ParamManufacturer = "ADSK_Завод-изготовитель";
        public const string ParamUnit = "ADSK_Единица измерения";
        public const string ParamQuantity = "ADSK_Количество";
        public const string ParamCategory = "N_Категория";
        const string CategoryValue = "6. Материалы и прочие элементы";

        //---------------- чтение ----------------

        public static List<FamilyInstance> Instances(Document doc, Func<string, bool> familyFilter)
        {
            return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)
                .WhereElementIsNotElementType().OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(fi => familyFilter(fi.Symbol?.FamilyName)).ToList();
        }

        public static List<FamilySymbol> Symbols(Document doc, Func<string, bool> familyFilter)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>().Where(s => familyFilter(s.FamilyName)).ToList();
        }

        /// <summary>Строки окна «Немоделируемые». Пустые кубики-шаблоны (без наименования) не показываются.</summary>
        public static List<NonModelElementRow> Collect(Document doc)
        {
            var rows = new List<NonModelElementRow>();
            foreach (FamilyInstance fi in Instances(doc, n => NonModelFamilyNames.IsGenerated(n) || NonModelFamilyNames.IsManual(n)))
            {
                string naim = GetString(fi, ParamNaim);
                if (string.IsNullOrWhiteSpace(naim)) continue;
                bool generated = NonModelFamilyNames.IsGenerated(fi.Symbol.FamilyName);
                NonModelRuleInfo rule = generated ? NonModelRuleRegistry.ByTypeName(fi.Symbol.Name) : null;
                rows.Add(new NonModelElementRow
                {
                    ElementId = fi.Id.LongValue(),
                    TypeName = fi.Symbol.Name,
                    GeneratedByRule = generated,
                    RuleId = rule?.Id,
                    RuleDescription = rule?.FullText(),
                    ADSK_Группирование = GetString(fi, ParamGroup),
                    ADSK_Наименование = naim,
                    ADSK_Марка = GetString(fi, ParamMark),
                    ADSK_КодИзделия = GetString(fi, ParamCode),
                    ADSK_ЗаводИзготовитель = GetString(fi, ParamManufacturer),
                    ADSK_ЕдиницаИзмерения = GetString(fi, ParamUnit),
                    ADSK_Количество = GetString(fi, ParamQuantity),
                });
            }
            return rows.OrderBy(r => r.GeneratedByRule).ThenBy(r => r.ADSK_Группирование).ThenBy(r => r.ADSK_Наименование).ToList();
        }

        //---------------- запись ----------------

        /// <summary>
        /// Записывает значения строки в элемент. Если меняются параметры типа, а тип общий с другими элементами,
        /// элемент переводится на свой тип, чтобы не испортить остальные позиции.
        /// </summary>
        public static void Apply(Document doc, NonModelElementRow row)
        {
            Element e = doc.GetElement(ElementIdCompat.ToElementId(row.ElementId));
            if (e == null) return;
            var values = new Dictionary<string, string>
            {
                [ParamGroup] = row.ADSK_Группирование,
                [ParamNaim] = row.ADSK_Наименование,
                [ParamMark] = row.ADSK_Марка,
                [ParamCode] = row.ADSK_КодИзделия,
                [ParamManufacturer] = row.ADSK_ЗаводИзготовитель,
                [ParamUnit] = row.ADSK_ЕдиницаИзмерения,
                [ParamQuantity] = row.ADSK_Количество,
            };
            if (e is FamilyInstance fi) EnsureOwnType(doc, fi, values);
            foreach (var kv in values) SetString(e, kv.Key, kv.Value);
        }

        /// <summary>
        /// Параметры, которые у семейства хранятся в типе: если их новые значения отличаются от текущих, а у типа есть
        /// другие экземпляры, экземпляр переводится на тип с такими значениями (существующий или дубликат с именем по наименованию).
        /// </summary>
        static void EnsureOwnType(Document doc, FamilyInstance fi, IDictionary<string, string> values)
        {
            FamilySymbol symbol = fi.Symbol;
            var typeLevel = values.Where(kv => fi.LookupParameter(kv.Key) == null && symbol.LookupParameter(kv.Key) != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value ?? "");
            if (typeLevel.Count == 0) return;
            Func<FamilySymbol, bool> matches = s => typeLevel.All(kv => SameValue(s.LookupParameter(kv.Key), kv.Value));
            if (matches(symbol)) return;

            bool shared = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Any(o => o.Id != fi.Id && o.Symbol.Id == symbol.Id);
            if (!shared) return; //тип только у этого элемента — меняем его как есть

            List<FamilySymbol> familySymbols = FamilySymbols(doc, symbol);
            values.TryGetValue(ParamNaim, out string naim);
            string baseName = TypeNameFor(naim);
            FamilySymbol target = familySymbols.FirstOrDefault(s => s.Id != symbol.Id && matches(s) && NameFits(s.Name, baseName));
            if (target == null)
            {
                string name = UniqueName(familySymbols, baseName);
                target = (FamilySymbol)symbol.Duplicate(name);
                Logger.Log("Немоделируемые: создан тип " + name, 1);
            }
            if (!target.IsActive) { target.Activate(); doc.Regenerate(); }
            fi.ChangeTypeId(target.Id);
        }

        static bool SameValue(Parameter p, string value)
        {
            if (p == null) return true;
            if (p.StorageType == StorageType.String) return (p.AsString() ?? "") == value;
            if (!TryParseNumber(value, out double d)) return false;
            if (p.StorageType == StorageType.Double) return Math.Abs(p.AsDouble() - d) < 1e-9;
            if (p.StorageType == StorageType.Integer) return p.AsInteger() == (int)Math.Round(d);
            return true;
        }

        //---------------- имена типов = ADSK_Наименование ----------------

        static readonly string[] DataParams = { ParamCategory, ParamGroup, ParamNaim, ParamMark, ParamCode, ParamManufacturer, ParamUnit, ParamQuantity };

        static List<FamilySymbol> FamilySymbols(Document doc, FamilySymbol symbol) =>
            symbol.Family.GetFamilySymbolIds().Select(id => doc.GetElement(id)).OfType<FamilySymbol>().ToList();

        //«Наименование» или «Наименование (2)» — имя подходит к наименованию
        static bool NameFits(string typeName, string baseName)
        {
            if (typeName == baseName) return true;
            if (!typeName.StartsWith(baseName + " (") || !typeName.EndsWith(")")) return false;
            string n = typeName.Substring(baseName.Length + 2, typeName.Length - baseName.Length - 3);
            return n.Length > 0 && n.All(char.IsDigit);
        }

        static string UniqueName(List<FamilySymbol> familySymbols, string baseName)
        {
            string name = baseName;
            for (int i = 2; familySymbols.Any(s => s.Name == name); i++) name = baseName + " (" + i + ")";
            return name;
        }

        //у типов одинаковые значения ADSK-параметров, хранящихся в типе
        static bool SameTypeValues(FamilySymbol a, FamilySymbol b)
        {
            return DataParams.All(n => a.LookupParameter(n) == null || GetString(a, n) == GetString(b, n));
        }

        /// <summary>
        /// Имена типов семейства «pmN.Условное семейство (ОбМод)» приводятся к ADSK_Наименование: тип переименовывается;
        /// если тип с таким именем уже есть и совпадает по параметрам типа — экземпляры переводятся на него (без дублей);
        /// если на одном типе элементы с разными наименованиями — они разводятся по своим типам.
        /// Типы генерации (Экземпляр) не трогаются: по их именам работают правила. Вызывается внутри транзакции.
        /// Возвращает количество изменений.
        /// </summary>
        public static int SyncManualTypeNames(Document doc)
        {
            int changes = 0;
            var byType = Instances(doc, NonModelFamilyNames.IsManual).GroupBy(fi => fi.Symbol.Id).ToDictionary(g => g.Key, g => g.ToList());
            foreach (FamilySymbol symbol in Symbols(doc, NonModelFamilyNames.IsManual))
            {
                if (!byType.TryGetValue(symbol.Id, out List<FamilyInstance> insts) || insts.Count == 0)
                {
                    //тип без экземпляров: имя по наименованию типа, если оно хранится в типе
                    string typeNaim = symbol.LookupParameter(ParamNaim) != null ? GetString(symbol, ParamNaim).Trim() : "";
                    if (typeNaim.Length > 0) changes += EnsureTypeName(doc, symbol, typeNaim, new List<FamilyInstance>());
                    continue;
                }
                var groups = insts.GroupBy(fi => GetString(fi, ParamNaim).Trim()).Where(g => g.Key.Length > 0)
                    .OrderByDescending(g => g.Count()).ToList();
                if (groups.Count == 0) continue;
                //больше всего элементов остаётся на типе, остальные наименования — на свои типы
                foreach (var g in groups.Skip(1))
                {
                    string baseName = TypeNameFor(g.Key);
                    List<FamilySymbol> familySymbols = FamilySymbols(doc, symbol);
                    FamilySymbol target = familySymbols.FirstOrDefault(s => s.Id != symbol.Id && NameFits(s.Name, baseName) && SameTypeValues(s, symbol))
                        ?? (FamilySymbol)symbol.Duplicate(UniqueName(familySymbols, baseName));
                    if (!target.IsActive) { target.Activate(); doc.Regenerate(); }
                    foreach (FamilyInstance fi in g) fi.ChangeTypeId(target.Id);
                    Logger.Log("Немоделируемые: «" + g.Key + "» (" + g.Count() + " шт.) переведены с типа «" + symbol.Name + "» на «" + target.Name + "»", 1);
                    changes++;
                }
                changes += EnsureTypeName(doc, symbol, groups[0].Key, groups[0].ToList());
            }
            return changes;
        }

        static int EnsureTypeName(Document doc, FamilySymbol symbol, string naim, List<FamilyInstance> insts)
        {
            string baseName = TypeNameFor(naim);
            if (NameFits(symbol.Name, baseName)) return 0;
            List<FamilySymbol> familySymbols = FamilySymbols(doc, symbol);
            //такой тип уже есть и совпадает по параметрам — переводим экземпляры на него вместо дубля
            FamilySymbol same = familySymbols.FirstOrDefault(s => s.Id != symbol.Id && NameFits(s.Name, baseName) && SameTypeValues(s, symbol));
            if (same != null && insts.Count > 0)
            {
                if (!same.IsActive) { same.Activate(); doc.Regenerate(); }
                foreach (FamilyInstance fi in insts) fi.ChangeTypeId(same.Id);
                Logger.Log("Немоделируемые: экземпляры типа «" + symbol.Name + "» переведены на «" + same.Name + "»", 1);
                return 1;
            }
            if (same != null) return 0; //пустой дубль — не переименовываем, чтобы не плодить «(2)»
            string name = UniqueName(familySymbols, baseName);
            try { symbol.Name = name; }
            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
            {
                //одна неудачная позиция не должна срывать проверку остальных
                Logger.Log("Немоделируемые: тип «" + symbol.Name + "» не переименован в «" + name + "»: " + ex.Message, 4);
                return 0;
            }
            Logger.Log("Немоделируемые: тип переименован в «" + name + "»", 1);
            return 1;
        }

        /// <summary>
        /// Проверяет, что семейство есть в проекте (иначе загружает с сервера) и размещено хотя бы одним экземпляром
        /// (иначе ставит один в 0,0,0). Возвращает текст проблемы или null.
        /// </summary>
        public static string EnsurePlaced(Document doc, string familyName, string rfaRelativePath, out bool changed)
        {
            changed = false;
            Func<string, bool> filter = n => n != null && n.Contains(familyName);
            List<FamilySymbol> symbols = Symbols(doc, filter);
            if (symbols.Count == 0)
            {
                string rfa = Path.Combine(TNovPaths.BimLibrary, rfaRelativePath);
                if (!File.Exists(rfa)) return "Семейство " + familyName + " не загружено в проект, файл не найден: " + rfa;
                if (!doc.LoadFamily(rfa, out Family family) || family == null) return "Не удалось загрузить семейство " + familyName + " из " + rfa;
                changed = true;
                Logger.Log("Загружено семейство " + family.Name, 1);
                symbols = Symbols(doc, filter);
                if (symbols.Count == 0) return "В семействе " + family.Name + " нет типов.";
            }
            if (Instances(doc, filter).Count > 0) return null;
            Place(doc, symbols[0], XYZ.Zero);
            changed = true;
            Logger.Log("Размещён экземпляр " + familyName + " в 0,0,0", 1);
            return null;
        }

        /// <summary>Создаёт ручной элемент (семейство ОбМод) по строке каталога с заданными группированием (параметр экземпляра) и количеством.</summary>
        public static long AddFromCatalog(Document doc, NonModelCatalogRow row, string grouping, string quantity)
        {
            string problem = EnsurePlaced(doc, NonModelFamilyNames.Manual, NonModelFamilyNames.ManualRfa, out _);
            if (problem != null) throw new InvalidOperationException(problem);

            List<FamilyInstance> existing = Instances(doc, NonModelFamilyNames.IsManual);
            FamilyInstance anchor = existing.First();
            XYZ basePoint = (anchor.Location as LocationPoint)?.Point ?? XYZ.Zero;
            FamilyInstance fi = Place(doc, anchor.Symbol, basePoint + new XYZ(0, 0, existing.Count / 304.8));

            //в ОбМод часть данных хранится в типе: новый элемент получает свой тип, чужие позиции не трогаем
            var values = new Dictionary<string, string>
            {
                [ParamCategory] = CategoryValue,
                [ParamGroup] = grouping,
                [ParamNaim] = row.ADSK_Наименование,
                [ParamMark] = row.ADSK_Марка,
                [ParamCode] = row.ADSK_КодИзделия,
                [ParamManufacturer] = row.ADSK_ЗаводИзготовитель,
                [ParamUnit] = row.ADSK_ЕдиницаИзмерения,
                [ParamQuantity] = quantity,
            };
            EnsureOwnType(doc, fi, values);
            foreach (var kv in values) SetString(fi, kv.Key, kv.Value);
            return fi.Id.LongValue();
        }

        //---------------- вспомогательные ----------------

        static FamilyInstance Place(Document doc, FamilySymbol symbol, XYZ point)
        {
            if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
            try
            {
                return doc.Create.NewFamilyInstance(point, symbol, StructuralType.NonStructural);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                //семейство на основе уровня
                Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => Math.Abs(l.Elevation)).First();
                return doc.Create.NewFamilyInstance(point, symbol, level, StructuralType.NonStructural);
            }
        }

        static string TypeNameFor(string naim)
        {
            //запрещённые символы определяет сам Revit (переводы строк, табуляция, \:{}[]|;<>?`~ и др.)
            var sb = new System.Text.StringBuilder();
            foreach (char c in (naim ?? "").Trim())
                sb.Append(char.IsWhiteSpace(c) || char.IsControl(c) ? ' ' : NamingUtils.IsValidName(c.ToString()) ? c : '_');
            string name = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            if (name.Length == 0 || !NamingUtils.IsValidName(name)) return "Без наименования";
            return name;
        }

        //параметр экземпляра, иначе — типа
        static Parameter Find(Element e, string name)
        {
            Parameter p = e.LookupParameter(name);
            if (p != null) return p;
            Element type = e.Document.GetElement(e.GetTypeId());
            return type?.LookupParameter(name);
        }

        public static string GetString(Element e, string name)
        {
            Parameter p = Find(e, name);
            if (p == null || !p.HasValue) return "";
            switch (p.StorageType)
            {
                case StorageType.String: return p.AsString() ?? "";
                case StorageType.Double: return p.AsDouble().ToString("0.###", CultureInfo.CurrentCulture);
                case StorageType.Integer: return p.AsInteger().ToString(CultureInfo.CurrentCulture);
                default: return p.AsValueString() ?? "";
            }
        }

        static void SetString(Element e, string name, string value)
        {
            Parameter p = Find(e, name);
            if (p == null || p.IsReadOnly) return;
            value = value ?? "";
            switch (p.StorageType)
            {
                case StorageType.String:
                    if ((p.AsString() ?? "") != value) p.Set(value);
                    break;
                case StorageType.Double:
                    if (TryParseNumber(value, out double d) && Math.Abs(p.AsDouble() - d) > 1e-9) p.Set(d);
                    break;
                case StorageType.Integer:
                    if (TryParseNumber(value, out double i) && p.AsInteger() != (int)Math.Round(i)) p.Set((int)Math.Round(i));
                    break;
            }
        }

        public static bool TryParseNumber(string s, out double value)
        {
            s = (s ?? "").Trim().Replace(" ", "");
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            s = s.Replace(".", sep).Replace(",", sep);
            if (s.Length == 0) { value = 0; return true; }
            return double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
