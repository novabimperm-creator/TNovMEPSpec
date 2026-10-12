using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Microsoft.Office.Interop.Excel;
using TNovCommon;
using Range = Microsoft.Office.Interop.Excel.Range;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace TNovMEPSpec
{
    /// <summary>
    /// Каталог ROLS-ISOMARKET (изоляция труб и воздуховодов): наименование и артикул по типу изоляции,
    /// толщине и (для трубок) диаметру трубы. Второй слой («Толщина второго слоя») выводится отдельной
    /// строкой - немоделируемым элементом типа «Второй слой изоляции».
    /// Перенесено из графов Dynamo «ROLS_Изоляция воздуховодов_v1» и «ROLS_Изоляция трубопроводов_v1.1».
    /// Справочник - ROLS_Изоляция.xlsx: листы «Трубки», «Рулоны», «Диаметры».
    /// </summary>
    public static class moduleROLS
    {
        static Guid NGNparamGuid = new Guid("cb7d14c9-43a3-451a-926c-f569df8b8c03");//N_Группирование_Не перезаполнять
        static Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        static Guid adskCodeparamGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");//ADSK_Код изделия
        static Guid adskCparamGuid = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a");//ADSK_Количество

        const string FileName = "ROLS_Изоляция.xlsx";
        const string SecondLayerTypeName = "Второй слой изоляции"; //без «ROLS»: такие кубики чистит IsolNonModel
        const string CubeFamilyName = "pmN.Условное семейство Экземпляр";

        public class CatalogItem { public string Naim; public string Code; }

        public class Catalog
        {
            public Dictionary<string, CatalogItem> Tubes = new Dictionary<string, CatalogItem>(); //Тип_Dвн/Толщина
            public Dictionary<string, CatalogItem> Rolls = new Dictionary<string, CatalogItem>(); //Тип_Толщина
            public Dictionary<string, string> Diameters = new Dictionary<string, string>(); //код диаметра трубы -> Dвн трубки
        }

        public class SecondLayer
        {
            public ElementId SourceId;
            public string Grouping;
            public double PS;
            public string Naim;
            public string Code;
            public string Manufacturer;
            public string Unit;
            public double Quantity;
        }

        public static Catalog ReadCatalog()
        {
            Catalog cat = new Catalog();
            Microsoft.Office.Interop.Excel.Application xlApp = null;
            Workbooks workbooks = null;
            Workbook wb = null;
            try
            {
                xlApp = new Microsoft.Office.Interop.Excel.Application();
                workbooks = xlApp.Workbooks;
                wb = workbooks.Open(TNovPaths.MepTables + @"\" + FileName, 0, true, 5, "", "", false, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "", true, false, 0, true, false, false);
                foreach (string[] r in ReadSheet((Worksheet)wb.Sheets["Трубки"], 5))
                    cat.Tubes[r[0] + "_" + r[1] + "/" + r[2]] = new CatalogItem { Naim = r[3], Code = r[4] };
                foreach (string[] r in ReadSheet((Worksheet)wb.Sheets["Рулоны"], 4))
                    cat.Rolls[r[0] + "_" + r[1]] = new CatalogItem { Naim = r[2], Code = r[3] };
                foreach (string[] r in ReadSheet((Worksheet)wb.Sheets["Диаметры"], 2))
                    cat.Diameters[r[0]] = r[1];
            }
            finally
            {
                if (wb != null) { wb.Close(false); Marshal.ReleaseComObject(wb); }
                if (workbooks != null) Marshal.ReleaseComObject(workbooks);
                if (xlApp != null) { xlApp.Quit(); Marshal.ReleaseComObject(xlApp); }
            }
            Logger.Log($"Каталог ROLS: трубок {cat.Tubes.Count}, рулонов {cat.Rolls.Count}, диаметров {cat.Diameters.Count}", 1);
            return cat;
        }

        /// <summary>Строки листа со 2-й до первой пустой в колонке A, одним чтением диапазона.</summary>
        static List<string[]> ReadSheet(Worksheet ws, int cols)
        {
            List<string[]> rows = new List<string[]>();
            int last = ((Range)ws.Cells[ws.Rows.Count, 1]).End[XlDirection.xlUp].Row;
            if (last < 2) return rows;
            Range range = ws.Range[(Range)ws.Cells[2, 1], (Range)ws.Cells[last, cols]];
            object[,] values = (object[,])range.Value2;
            for (int i = 1; i <= values.GetLength(0); i++)
            {
                string[] r = new string[cols];
                for (int c = 1; c <= cols; c++) r[c - 1] = Convert.ToString(values[i, c], CultureInfo.InvariantCulture)?.Trim() ?? "";
                if (r[0].Length == 0) break;
                rows.Add(r);
            }
            return rows;
        }

        /// <summary>
        /// true - элемент не ROLS или параметры записаны; false - позиция не найдена в каталоге или ошибка записи.
        /// layers == null - количество между слоями не делится (оно не пересчитано в этом запуске).
        /// </summary>
        public static bool SetParams(Element ins, Catalog cat, List<SecondLayer> layers)
        {
            Document doc = ins.Document;
            string eid = ins.Id.ToString();
            Element eType = doc.GetElement(ins.GetTypeId());
            if (eType == null) return true;
            string manufacturer = eType.LookupParameter("Изготовитель")?.AsString() ?? "";
            if (!manufacturer.Contains("ROLS")) return true;

            Parameter ngn = ins.get_Parameter(NGNparamGuid);
            if (ngn != null && ngn.HasValue && ngn.AsInteger() == 1) { Logger.Log("   Элемент " + eid + ": пропуск, не перезаполнять", 2); return true; }

            InsulationLiningBase insulation = ins as InsulationLiningBase;
            Element host = insulation != null ? doc.GetElement(insulation.HostElementId) : null;
            bool straight = host is Pipe || host is Duct;
            string typeName = eType.Name;

            double t = MM(ins.LookupParameter("Толщина изоляции"));
            double t2 = MM(ins.LookupParameter("Толщина второго слоя"));
            if (t2 < 0 || t2 >= t) t2 = 0;
            double t1 = t - t2;
            Logger.Log($"   Элемент {eid}: ROLS {typeName}, толщина {t1}{(t2 > 0 ? " + " + t2 : "")}", 2);

            CatalogItem item1, item2 = null;
            if (typeName.Contains("Трубки"))
            {
                //трубки подбираются по диаметру трубы; изоляцию фитингов граф Dynamo тоже не обрабатывал
                if (!(host is Pipe)) { Logger.Log("      трубка не на прямом участке трубы - пропуск", 2); return true; }
                if (t2 > 0) { Logger.Log($"      {eid} Ошибка ROLS: второй слой для трубок не поддерживается", 4); return false; }
                double dNom = Math.Round(host.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble() * 304.8, 2);
                double dOut = Math.Round(host.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER).AsDouble() * 304.8, 2);
                string dCode = DiamCode(dNom, dOut);
                if (!cat.Diameters.TryGetValue(dCode, out string dIns))
                {
                    Logger.Log($"      {eid} Ошибка ROLS: диаметр трубы Ду{dNom} Дн{dOut} (код {dCode}) нет в листе «Диаметры»", 4);
                    return false;
                }
                dIns = IntText(double.Parse(dIns, CultureInfo.InvariantCulture));
                if (typeName.Contains("Super Protect") && dIns == "25") dIns = "28"; //у Super Protect нет трубок 25
                string key = typeName + "_" + dIns + "/" + IntText(t1);
                if (!cat.Tubes.TryGetValue(key, out item1)) { Logger.Log($"      {eid} Ошибка ROLS: нет трубки {key} в каталоге", 4); return false; }
            }
            else
            {
                string key1 = typeName + "_" + IntText(t1);
                if (!cat.Rolls.TryGetValue(key1, out item1)) { Logger.Log($"      {eid} Ошибка ROLS: нет рулона {key1} в каталоге", 4); return false; }
                if (t2 > 0)
                {
                    string key2 = typeName + "_" + IntText(t2);
                    if (!cat.Rolls.TryGetValue(key2, out item2)) { Logger.Log($"      {eid} Ошибка ROLS: нет рулона {key2} (второй слой) в каталоге", 4); return false; }
                }
            }

            bool success = SetString(ins, adskNparamGuid, item1.Naim) & SetString(ins, adskCodeparamGuid, item1.Code);
            Logger.Log("      назначено " + item1.Naim + " / " + item1.Code, 2);

            if (item2 != null && layers == null)
                Logger.Log($"      {eid} второй слой не пересчитан: выключено заполнение ADSK_Наименование и Количество", 3);
            else if (item2 != null)
            {
                //количество уже посчитано Setadskpparam (с коэффициентом) - делим его между слоями по геометрии
                Parameter pC = ins.get_Parameter(adskCparamGuid);
                if (pC == null || pC.IsReadOnly) { Logger.Log($"      {eid} Ошибка ROLS: ADSK_Количество недоступен для записи", 4); return false; }
                double q = pC.AsDouble();
                string unit = Txt(ins, "ADSK_Единица измерения"); if (unit.Length == 0) unit = Txt(eType, "ADSK_Единица измерения");
                bool volume = unit.Contains("³");
                double share1 = Layer1Share(host, straight, volume, t, t1); //доля внутреннего слоя
                double q1 = q * share1;
                double q2 = volume ? q - q1 : q; //площадь наружного слоя - площадь всей изоляции
                if (!straight && !volume) q1 = q; //на фитингах площадь обоих слоев принимаем по наружной поверхности
                try { pC.Set(Math.Round(q1, 1)); }
                catch (Exception ex) { Logger.Log($"      {eid} Ошибка: {ex.Message}", 4); success = false; }
                string manuf = Txt(ins, "ADSK_Завод-изготовитель"); if (manuf.Length == 0) manuf = Txt(eType, "ADSK_Завод-изготовитель");
                Parameter pPS = ins.LookupParameter("N_Принадлежность системы");
                layers.Add(new SecondLayer
                {
                    SourceId = ins.Id,
                    Grouping = Txt(ins, "ADSK_Группирование"),
                    PS = pPS != null && pPS.StorageType == StorageType.Double ? pPS.AsDouble() : 0,
                    Naim = item2.Naim,
                    Code = item2.Code,
                    Manufacturer = manuf,
                    Unit = unit,
                    Quantity = q2,
                });
                Logger.Log($"      слой 1: {Math.Round(q1, 1)}, слой 2: {item2.Naim} {Math.Round(q2, 1)}", 2);
            }
            return success;
        }

        /// <summary>
        /// Немоделируемые элементы «Второй слой изоляции»: прежние удаляются, новые создаются по одному на
        /// ADSK_Группирование + Принадлежность системы + Наименование. Возвращает количество созданных.
        /// </summary>
        public static int GenerateSecondLayers(Document doc, List<SecondLayer> layers)
        {
            List<FamilySymbol> symbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilySymbol>().Where(s => s.FamilyName.Contains(CubeFamilyName)).ToList();
            FamilySymbol symbol = symbols.FirstOrDefault(s => s.Name == SecondLayerTypeName);
            if (symbol != null)
            {
                List<ElementId> old = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.Id == symbol.Id).Select(fi => fi.Id).ToList();
                if (old.Count > 0) { doc.Delete(old); Logger.Log("Удалены прежние элементы второго слоя: " + old.Count, 1); }
            }
            if (layers.Count == 0) return 0;
            if (symbols.Count == 0)
            {
                Logger.Log("Второй слой изоляции: нет семейства " + CubeFamilyName, 4);
                new InfoWindow280("Для вывода второго слоя изоляции ROLS загрузите в модель семейство " + CubeFamilyName + ".").ShowDialog();
                return 0;
            }
            if (symbol == null) symbol = (FamilySymbol)symbols.First().Duplicate(SecondLayerTypeName);
            if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }

            FamilyInstance anchor = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .FirstOrDefault(fi => fi.Symbol.FamilyName.Contains(CubeFamilyName));
            XYZ basePoint = (anchor?.Location as LocationPoint)?.Point ?? XYZ.Zero;

            int created = 0;
            double step = 1 / 304.8;
            var groups = layers.GroupBy(l => new { l.Grouping, l.PS, l.Naim }).OrderBy(g => g.Key.Grouping).ThenBy(g => g.Key.PS).ThenBy(g => g.Key.Naim);
            foreach (var g in groups)
            {
                created++;
                FamilyInstance fi = doc.Create.NewFamilyInstance(basePoint + new XYZ(0, 0, step * created), symbol, StructuralType.NonStructural);
                SecondLayer first = g.First();
                double sum = Math.Round(g.Sum(l => l.Quantity), 1);
                fi.LookupParameter("N_Категория")?.Set("5. Изоляционные материалы");
                fi.LookupParameter("ADSK_Группирование")?.Set(g.Key.Grouping);
                fi.LookupParameter("N_Принадлежность системы")?.Set(g.Key.PS);
                fi.LookupParameter("ADSK_Наименование")?.Set(g.Key.Naim);
                fi.LookupParameter("ADSK_Код изделия")?.Set(first.Code);
                fi.LookupParameter("ADSK_Завод-изготовитель")?.Set(first.Manufacturer);
                fi.LookupParameter("ADSK_Единица измерения")?.Set(first.Unit);
                fi.LookupParameter("ADSK_Количество")?.Set(sum);
                Logger.Log($"   {fi.Id}: {g.Key.Grouping} / {g.Key.Naim} = {sum} (элементов {g.Count()})", 2);
            }
            Logger.Log("Создано элементов второго слоя изоляции: " + created, 1);
            return created;
        }

        /// <summary>Доля внутреннего слоя (толщиной t1 из общей t) в количестве изоляции.</summary>
        static double Layer1Share(Element host, bool straight, bool volume, double t, double t1)
        {
            if (!straight) return volume ? t1 / t : 1;
            double d = 0, s = 0; //диаметр круглого сечения либо полупериметр прямоугольного, мм
            if (host is Pipe) d = host.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER).AsDouble() * 304.8;
            else
            {
                Parameter pd = host.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (pd != null && pd.HasValue && pd.AsDouble() > 0) d = pd.AsDouble() * 304.8;
                else s = (host.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).AsDouble() + host.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).AsDouble()) * 304.8;
            }
            if (d > 0)
                return volume ? (Math.Pow(d + 2 * t1, 2) - d * d) / (Math.Pow(d + 2 * t, 2) - d * d) : (d + 2 * t1) / (d + 2 * t);
            return volume ? (s * t1 + 2 * t1 * t1) / (s * t + 2 * t * t) : (s + 4 * t1) / (s + 4 * t);
        }

        /// <summary>Код диаметра трубы для листа «Диаметры»: c - медь, p - пластик (Дн = Ду), s - сталь. Порт Python-ноды графа.</summary>
        static string DiamCode(double dNom, double dOut)
        {
            double[] copper = { 6, 6.4, 6.35, 8, 7.9, 7.93, 10, 9.5, 9.52, 9.53 };
            if (copper.Contains(dNom)) return Num(dNom) + "c";
            if (dNom == 12.7) return "12.7c";
            if (dOut == dNom) return Num(dNom) + "p";
            if (dOut == 12) return "12c";
            if (dNom == 40 && dOut == 45) return "4045s";
            if (dNom == 40 && dOut == 48) return "4048s";
            if (dNom == 50 && dOut == 57) return "5057s";
            if (dNom == 50 && dOut == 60) return "5060s";
            if (dOut == 108) return "100108s";
            if (dOut == 114) return "100114s";
            if (dOut == 110) return "110p";
            return Num(dNom) + "s";
        }

        static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Целая часть числа текстом, как «числа в текст» в графе (str(x).split('.')[0]).</summary>
        static string IntText(double v) => Math.Floor(Math.Round(v, 3)).ToString(CultureInfo.InvariantCulture);

        /// <summary>Значение параметра в мм: длина - из футов, текст/число - как есть; нет значения - 0.</summary>
        static double MM(Parameter p)
        {
            if (p == null || !p.HasValue) return 0;
            switch (p.StorageType)
            {
                case StorageType.Double: return Math.Round(p.AsDouble() * 304.8, 3);
                case StorageType.Integer: return p.AsInteger();
                case StorageType.String:
                    double.TryParse((p.AsString() ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v);
                    return v;
                default: return 0;
            }
        }

        static string Txt(Element e, string name)
        {
            Parameter p = e?.LookupParameter(name);
            if (p == null || !p.HasValue) return "";
            return (p.StorageType == StorageType.String ? p.AsString() : p.AsValueString()) ?? "";
        }

        static bool SetString(Element e, Guid guid, string value)
        {
            Parameter p = e.get_Parameter(guid);
            if (p == null || p.IsReadOnly) return true; //задано по типу - не трогаем, как в Setadskpparam
            try { p.Set(value ?? ""); return true; }
            catch (Exception ex) { Logger.Log($"      {e.Id} Ошибка: {ex.Message}", 4); return false; }
        }
    }
}
