using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using TNovCommon;

namespace TNovMEPSpec
{
    /// <summary>Комплект *.СО: листы по порядку номеров и сводная спецификация на них.</summary>
    internal class MEPSpecSheetsSet
    {
        public string Name;
        public List<ViewSheet> Sheets;
        public ViewSchedule Schedule;
        public XYZ FirstPoint;   // точка спецификации на первом листе (Форма 3)
        public XYZ OtherPoint;   // точка на последующих листах (Форма 6)
        public double FirstHeightMm;
        public double OtherHeightMm;
    }

    /// <summary>
    /// «Спеку на листы»: ровная разбивка сводной спецификации комплекта *.СО на сегменты
    /// по высоте поля листа, создание недостающих и удаление лишних (пустых) листов.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MEPSpecSheets : IExternalCommand
    {
        Guid adskSheetSetParamGuid = new Guid("e1b06433-f527-403c-8986-af9a01e6be7f"); //ADSK_Комплект чертежей
        Guid NSheetNumberParamGuid = new Guid("b6e73342-b6cd-42c5-86c5-64b04b5b88de"); //N_Ш.НомерЛиста
        Guid NSheetThroughNumberParamGuid = new Guid("e186302c-74fe-43ba-9224-cb5bab3c6ae7"); //Ш.НомерЛистаСквозной

        // Низ поля спецификации от низа листа, мм: рамка 5 + штамп (Форма 3 — 55, Форма 6 — 15)
        const double Form3BottomMm = 60;
        const double Form6BottomMm = 20;
        // Габарит сегмента на листе выше заданной высоты на ~4,2 мм, верх габарита — на ~2 мм выше точки вставки
        const double SegmentExtraMm = 4.5;
        const double PointAboveMm = 2.5;
        const double FeetToMm = 304.8;

        #region Индикатор прогресса (1 единица — 1 лист)
        private volatile TNovProgressBar progressBar; // создаётся в своём STA-потоке
        private int pbValue, pbMax;

        private void StartProgress(int max)
        {
            pbValue = 0; pbMax = max;
            Thread thread = new Thread(() =>
            {
                progressBar = new TNovProgressBar();
                progressBar.Show();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            int n = 0;
            while (progressBar == null && n++ < 200)
                Thread.Sleep(10);
            UpdateProgress("Подготовка...");
        }

        /// <summary>Обновить окно; info == null — подпись не меняется.</summary>
        private void UpdateProgress(string info = null)
        {
            if (progressBar == null) return;
            int value = Math.Min(pbValue, pbMax), max = Math.Max(pbMax, 1);
            progressBar.Dispatcher.Invoke((Action)(() =>
            {
                if (info != null && progressBar.info != null) progressBar.info.Text = info;
                progressBar.TNov_ProgressBar.Minimum = 0;
                progressBar.TNov_ProgressBar.Maximum = max;
                progressBar.TNov_ProgressBar.Value = value;
                progressBar.value.Text = value.ToString();
                progressBar.maxvalue.Text = max.ToString();
            }));
        }

        private void StepProgress(string info = null)
        {
            pbValue++;
            UpdateProgress(info);
        }

        private void CloseProgress()
        {
            if (progressBar == null) return;
            try
            {
                progressBar.Dispatcher.Invoke((Action)(() => progressBar.Close()));
                progressBar.Dispatcher.InvokeShutdown();
            }
            catch (Exception) { }
            progressBar = null;
        }
        #endregion

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Спеку на листы";
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);
            if (config == null || !CorpGate.Require(config)) return Result.Failed;
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            #region Комплекты *.СО
            List<string> warnings = new List<string>();
            List<MEPSpecSheetsSet> sets = CollectSets(doc, warnings);
            if (sets.Count == 0)
            {
                Logger.Log("Комплекты *.СО со спецификацией не найдены. Завершение работы.", 3);
                new InfoWindow280("Не найдено листов с ADSK_Комплект чертежей на «.СО» и спецификацией на первом листе."
                    + (warnings.Count > 0 ? "\n\n" + string.Join("\n", warnings) : "")).ShowDialog();
                return Result.Cancelled;
            }
            #endregion

            #region Окно
            var viewModel = new MEPSpecSheetsViewModel();
            foreach (var set in sets)
            {
                viewModel.Sets.Add(new MEPSpecSheetsSetItem
                {
                    SheetSet = set.Name,
                    ScheduleName = set.Schedule.Name,
                    Info = "листов: " + set.Sheets.Count + ", сегментов: " + (set.Schedule.IsSplit() ? set.Schedule.GetSegmentCount() : 1),
                    FirstHeight = set.FirstHeightMm.ToString(CultureInfo.InvariantCulture),
                    OtherHeight = set.OtherHeightMm.ToString(CultureInfo.InvariantCulture),
                    Data = set
                });
            }
            var wpfview = new MEPSpecSheetsWPF(viewModel);
            bool? ok = wpfview.ShowDialog();
            if (ok != true) { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }

            var selected = new List<MEPSpecSheetsSet>();
            foreach (var item in viewModel.Sets.Where(i => i.IsSelected))
            {
                double h1, hn;
                if (!TryParseMm(item.FirstHeight, out h1) || !TryParseMm(item.OtherHeight, out hn))
                {
                    new InfoWindow280("Комплект " + item.SheetSet + ": высота должна быть числом больше 20 мм.").ShowDialog();
                    return Result.Cancelled;
                }
                item.Data.FirstHeightMm = h1; item.Data.OtherHeightMm = hn;
                selected.Add(item.Data);
            }
            if (selected.Count == 0) return Result.Cancelled;
            #endregion

            #region Разбивка
            var report = new StringBuilder();
            // Сначала в максимуме — текущие листы; когда для комплекта подобрано число сегментов, максимум уточняется
            StartProgress(selected.Sum(s => s.Sheets.Count));
            try
            {
                using (TransactionGroup group = new TransactionGroup(doc, "TNov - Спеку на листы"))
                {
                    group.Start();
                    foreach (var set in selected)
                    {
                        int setStart = pbValue;
                        int setUnits = set.Sheets.Count;
                        using (Transaction t = new Transaction(doc, "TNov - Спеку на листы " + set.Name))
                        {
                            FailureHandlingOptions fho = t.GetFailureHandlingOptions();
                            fho.SetFailuresPreprocessor(new WarningSwallower());
                            t.SetFailureHandlingOptions(fho);
                            t.Start();
                            try
                            {
                                string line = ProcessSet(doc, uidoc, set, viewModel.DeleteExtraSheets, ref setUnits);
                                UpdateProgress(set.Name + ": сохранение изменений");
                                t.Commit();
                                report.AppendLine(line);
                                Logger.Log(line, 1);
                            }
                            catch (Exception ex)
                            {
                                if (t.HasStarted() && !t.HasEnded()) t.RollBack();
                                report.AppendLine(set.Name + ": ошибка, изменения отменены — " + ex.Message);
                                Logger.Log(set.Name + ": " + ex, 4);
                            }
                        }
                        // При ошибке часть листов не пройдена — дотягиваем счётчик до конца комплекта
                        pbValue = setStart + setUnits;
                        UpdateProgress();
                    }
                    group.Assimilate();
                }
            }
            finally { CloseProgress(); }
            #endregion

            if (warnings.Count > 0) report.AppendLine().AppendLine(string.Join("\n", warnings));
            Logger.Log("Завершено", 5);
            new InfoWindow280(report.ToString().TrimEnd()).ShowDialog();
            return Result.Succeeded;
        }

        #region Сбор комплектов
        private List<MEPSpecSheetsSet> CollectSets(Document doc, List<string> warnings)
        {
            var result = new List<MEPSpecSheetsSet>();

            var bySet = new Dictionary<string, List<ViewSheet>>();
            foreach (ViewSheet sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
            {
                if (sheet.IsPlaceholder) continue;
                Parameter p = sheet.get_Parameter(adskSheetSetParamGuid) ?? sheet.LookupParameter("ADSK_Комплект чертежей");
                string sSet = p == null ? null : p.AsString();
                if (sSet == null) continue;
                sSet = sSet.Trim();
                if (!sSet.EndsWith(".СО")) continue;
                if (!bySet.ContainsKey(sSet)) bySet[sSet] = new List<ViewSheet>();
                bySet[sSet].Add(sheet);
            }

            var instancesBySheet = new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .Where(x => !x.IsTitleblockRevisionSchedule)
                .GroupBy(x => x.OwnerViewId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kv in bySet.OrderBy(k => k.Key))
            {
                var sheets = kv.Value
                    .OrderBy(s => CleanNumber(s.SheetNumber) ?? int.MaxValue)
                    .ThenBy(s => s.SheetNumber)
                    .ToList();
                ViewSheet first = sheets[0];

                var setInstances = sheets.SelectMany(s => instancesBySheet.TryGetValue(s.Id, out var l) ? l : new List<ScheduleSheetInstance>()).ToList();
                var candidates = setInstances
                    .Where(x => !IsServiceSchedule(doc.GetElement(x.ScheduleId).Name))
                    .GroupBy(x => x.ScheduleId)
                    .OrderByDescending(g => g.Any(x => x.OwnerViewId == first.Id))
                    .ThenByDescending(g => g.Count())
                    .ThenByDescending(g => doc.GetElement(g.Key).Name.IndexOf("пецификац", StringComparison.Ordinal) >= 0)
                    .ThenBy(g => g.Where(x => x.OwnerViewId == first.Id).Select(x => x.Point.Y).DefaultIfEmpty(double.MaxValue).Min())
                    .ToList();
                if (candidates.Count == 0 || !candidates[0].Any(x => x.OwnerViewId == first.Id))
                {
                    warnings.Add(kv.Key + ": на первом листе не найдена сводная спецификация — комплект пропущен.");
                    continue;
                }

                var schedule = (ViewSchedule)doc.GetElement(candidates[0].Key);
                var setSheetIds = new HashSet<ElementId>(sheets.Select(s => s.Id));
                var foreign = schedule.GetScheduleInstances(-1)
                    .Concat(Enumerable.Range(0, schedule.IsSplit() ? schedule.GetSegmentCount() : 0).SelectMany(i => schedule.GetScheduleInstances(i)))
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null && !setSheetIds.Contains(e.OwnerViewId))
                    .ToList();
                if (foreign.Count > 0)
                {
                    warnings.Add(kv.Key + ": спецификация «" + schedule.Name + "» стоит и на листах других комплектов — комплект пропущен.");
                    continue;
                }

                var firstInst = candidates[0].Where(x => x.OwnerViewId == first.Id).OrderBy(x => x.SegmentIndex).First();
                XYZ firstPoint = firstInst.Point;

                XYZ otherPoint = null;
                if (sheets.Count > 1)
                {
                    var secondInst = candidates[0].FirstOrDefault(x => x.OwnerViewId == sheets[1].Id);
                    if (secondInst != null) otherPoint = secondInst.Point;
                }
                if (otherPoint == null)
                {
                    // Продолжений нет — берём самый верхний из видов-спецификаций в рамке первого листа
                    double topY = firstPoint.Y;
                    BoundingBoxXYZ tbb = TitleBlockBox(doc, first);
                    if (tbb != null && instancesBySheet.TryGetValue(first.Id, out var firstList))
                    {
                        foreach (var x in firstList)
                        {
                            if (x.Point.X < tbb.Min.X || x.Point.X > tbb.Max.X || x.Point.Y > tbb.Max.Y) continue;
                            if (IsServiceSchedule(doc.GetElement(x.ScheduleId).Name)) continue;
                            topY = Math.Max(topY, x.Point.Y);
                        }
                    }
                    otherPoint = new XYZ(firstPoint.X, topY, firstPoint.Z);
                }

                double firstBottom = IsForm3(TitleBlockSymbol(doc, first)) ? Form3BottomMm : Form6BottomMm;
                for (int i = 1; i < sheets.Count; i++)
                {
                    if (IsForm3(TitleBlockSymbol(doc, sheets[i])))
                        warnings.Add(kv.Key + ": на листе " + Visible(sheets[i].SheetNumber) + " штамп Форма 3 вместо Формы 6 — проверьте.");
                }

                result.Add(new MEPSpecSheetsSet
                {
                    Name = kv.Key,
                    Sheets = sheets,
                    Schedule = schedule,
                    FirstPoint = firstPoint,
                    OtherPoint = otherPoint,
                    FirstHeightMm = Math.Floor(firstPoint.Y * FeetToMm - firstBottom - PointAboveMm),
                    OtherHeightMm = Math.Floor(otherPoint.Y * FeetToMm - Form6BottomMm - PointAboveMm),
                });
            }
            return result;
        }

        private static bool IsServiceSchedule(string name)
        {
            return name.IndexOf("Кол-во листов", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Проверка", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        #endregion

        #region Обработка комплекта
        /// <param name="setUnits">Сколько листов комплекта пройдёт индикатор: на входе текущее число, на выходе — уточнённое.</param>
        private string ProcessSet(Document doc, UIDocument uidoc, MEPSpecSheetsSet set, bool deleteExtra, ref int setUnits)
        {
            ViewSchedule vs = set.Schedule;
            Logger.Log("> Комплект " + set.Name + ", спецификация " + vs.Name + ", высоты " + set.FirstHeightMm + "/" + set.OtherHeightMm, 1);
            UpdateProgress(set.Name + ": замер спецификации");

            // Старые размещения удаляем: после перерезки индексы сегментов меняются
            var oldInstances = new List<ElementId>(vs.GetScheduleInstances(-1));
            if (vs.IsSplit())
                for (int i = 0; i < vs.GetSegmentCount(); i++) oldInstances.AddRange(vs.GetScheduleInstances(i));
            oldInstances = oldInstances.Distinct().ToList();
            if (oldInstances.Count > 0) doc.Delete(oldInstances);

            // Временный лист для замеров: фактическую высоту строк (с переносами) Revit отдаёт
            // только через габарит размещённого последнего, неограниченного сегмента.
            ViewSheet probe = ViewSheet.Create(doc, ElementId.InvalidElementId);

            ApplySplit(vs, 1, set);
            double total = MeasureLast(doc, vs, probe);
            Logger.Log("   высота спецификации целиком: " + Math.Round(total, 1) + " мм", 2);

            int n = 1;
            if (total > set.FirstHeightMm + SegmentExtraMm)
                n = 1 + (int)Math.Ceiling((total - set.FirstHeightMm - SegmentExtraMm) / set.OtherHeightMm);

            // Каждая проверка — перерезка и две регенерации, поэтому результаты кэшируются
            int applied = -1;
            var fitsCache = new Dictionary<int, bool> { [1] = n == 1 };
            Func<int, bool> fits = k =>
            {
                if (fitsCache.TryGetValue(k, out bool cached)) return cached;
                UpdateProgress(set.Name + ": подбор разбивки, проверка " + k + " лист.");
                ApplySplit(vs, k, set);
                applied = k;
                double last = MeasureLast(doc, vs, probe);
                Logger.Log("   сегментов " + k + ": последний " + Math.Round(last, 1) + " мм", 2);
                return fitsCache[k] = last <= (k == 1 ? set.FirstHeightMm : set.OtherHeightMm) + SegmentExtraMm;
            };
            if (n > 1)
            {
                while (!fits(n))
                {
                    n++;
                    if (n > 500) throw new InvalidOperationException("не удалось подобрать число листов");
                }
                while (n > 1 && fits(n - 1)) n--;
                if (applied != n) ApplySplit(vs, n, set);
            }
            doc.Delete(probe.Id);

            // Листы
            List<ViewSheet> sheets = set.Sheets;
            int before = sheets.Count;
            // Единиц на комплект: n листов с сегментами + лишние листы (удалены или оставлены)
            int units = Math.Max(n, before);
            pbMax += units - setUnits;
            setUnits = units;
            UpdateProgress(set.Name + ": листы");
            var created = new List<string>();
            var deleted = new List<string>();
            var kept = new List<string>();

            if (sheets.Count < n)
            {
                ViewSheet template = sheets.Last();
                FamilySymbol tbSymbol = TitleBlockSymbol(doc, template);
                if (sheets.Count == 1 || IsForm3(tbSymbol))
                    tbSymbol = FindForm6(doc) ?? tbSymbol;
                var existingNumbers = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber));

                for (int i = sheets.Count; i < n; i++)
                {
                    string number = MakeNumber(template.SheetNumber, i + 1);
                    if (existingNumbers.Contains(number))
                        throw new InvalidOperationException("номер листа " + Visible(number) + " уже занят");

                    ViewSheet ns = ViewSheet.Create(doc, tbSymbol == null ? ElementId.InvalidElementId : tbSymbol.Id);
                    CopyParams(template, ns);
                    ns.SheetNumber = number;
                    ns.Name = MakeName(template.Name, sheets.Count, i + 1);
                    SetNSheetNumber(ns, i + 1);
                    Parameter throughSrc = template.get_Parameter(NSheetThroughNumberParamGuid);
                    Parameter throughDst = ns.get_Parameter(NSheetThroughNumberParamGuid);
                    if (throughSrc != null && throughDst != null && !throughDst.IsReadOnly
                        && throughSrc.StorageType == StorageType.Integer && throughSrc.AsInteger() > 0)
                        throughDst.Set(throughSrc.AsInteger() + (i + 1 - before));
                    Element srcTb = TitleBlock(doc, template), dstTb = TitleBlock(doc, ns);
                    if (srcTb != null && dstTb != null && srcTb.GetTypeId() == dstTb.GetTypeId()) CopyParams(srcTb, dstTb);

                    existingNumbers.Add(number);
                    created.Add(Visible(number));
                    sheets.Add(ns);
                }
            }
            else if (sheets.Count > n)
            {
                foreach (ViewSheet extra in sheets.Skip(n).ToList())
                {
                    string why = deleteExtra ? NotEmptyReason(doc, uidoc, extra) : "удаление выключено";
                    if (why == null)
                    {
                        deleted.Add(Visible(extra.SheetNumber));
                        doc.Delete(extra.Id);
                        sheets.Remove(extra);
                    }
                    else kept.Add(Visible(extra.SheetNumber) + " (" + why + ")");
                    StepProgress();
                }
            }

            // Размещение сегментов
            for (int i = 0; i < n; i++)
            {
                XYZ point = i == 0 ? set.FirstPoint : set.OtherPoint;
                ScheduleSheetInstance inst = n > 1
                    ? ScheduleSheetInstance.Create(doc, sheets[i].Id, vs.Id, point, i)
                    : ScheduleSheetInstance.Create(doc, sheets[i].Id, vs.Id, point);
                inst.Point = point;
                StepProgress(set.Name + ": лист " + Visible(sheets[i].SheetNumber));
            }

            string line = set.Name + ": листов " + before + " → " + (before + created.Count - deleted.Count);
            if (created.Count > 0) line += "; созданы " + string.Join(", ", created);
            if (deleted.Count > 0) line += "; удалены " + string.Join(", ", deleted);
            if (kept.Count > 0) line += "; без сегмента оставлены " + string.Join(", ", kept);
            return line;
        }

        /// <summary>Ровная разбивка: первый сегмент — под Форму 3, остальные — под Форму 6, последний без ограничения.</summary>
        private static void ApplySplit(ViewSchedule vs, int segments, MEPSpecSheetsSet set)
        {
            while (vs.IsSplit() && vs.GetSegmentCount() > 1) vs.MergeSegments(0, 1);
            if (segments <= 1) return;
            var heights = new List<double> { set.FirstHeightMm / FeetToMm };
            for (int i = 2; i < segments; i++) heights.Add(set.OtherHeightMm / FeetToMm);
            vs.Split(heights);
        }

        /// <summary>Фактическая высота последнего сегмента (мм) по габариту на временном листе.</summary>
        private static double MeasureLast(Document doc, ViewSchedule vs, ViewSheet probe)
        {
            doc.Regenerate();
            ScheduleSheetInstance inst = vs.IsSplit()
                ? ScheduleSheetInstance.Create(doc, probe.Id, vs.Id, XYZ.Zero, vs.GetSegmentCount() - 1)
                : ScheduleSheetInstance.Create(doc, probe.Id, vs.Id, XYZ.Zero);
            doc.Regenerate();
            BoundingBoxXYZ bb = inst.get_BoundingBox(probe);
            double h = bb == null ? 0 : (bb.Max.Y - bb.Min.Y) * FeetToMm;
            doc.Delete(inst.Id);
            return h;
        }

        /// <summary>null — лист можно удалить; иначе причина, по которой он оставлен.</summary>
        private static string NotEmptyReason(Document doc, UIDocument uidoc, ViewSheet sheet)
        {
            if (uidoc.ActiveView != null && uidoc.ActiveView.Id == sheet.Id) return "открыт в Revit";
            if (uidoc.GetOpenUIViews().Any(v => v.ViewId == sheet.Id)) return "открыт в Revit";
            if (sheet.GetAllViewports().Count > 0) return "есть виды";
            var other = new FilteredElementCollector(doc, sheet.Id).WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.Id != sheet.Id)
                .Where(e => e.Category.Id != new ElementId(BuiltInCategory.OST_TitleBlocks))
                .Where(e => !(e is ScheduleSheetInstance ssi && ssi.IsTitleblockRevisionSchedule))
                .Where(e => !(e is FamilyInstance fi && fi.SuperComponent != null)) // вложенные в штамп (строки подписей)
                .Select(e => e.Category.Name)
                .Distinct()
                .ToList();
            return other.Count == 0 ? null : "есть " + string.Join(", ", other);
        }
        #endregion

        #region Листы, номера, параметры
        /// <summary>Номер листа без невидимых символов Менеджера листов (последняя группа цифр).</summary>
        internal static int? CleanNumber(string sheetNumber)
        {
            Match m = Regex.Match(sheetNumber ?? "", @"\d+(?!.*\d)", RegexOptions.Singleline);
            return m.Success && int.TryParse(m.Value, out int v) ? v : (int?)null;
        }

        /// <summary>Номер по образцу соседнего листа: те же префиксы/суффиксы U+202A, другая группа цифр.</summary>
        internal static string MakeNumber(string templateNumber, int newNumber)
        {
            Match m = Regex.Match(templateNumber, @"\d+(?!.*\d)", RegexOptions.Singleline);
            if (!m.Success) return templateNumber + newNumber;
            return templateNumber.Substring(0, m.Index) + newNumber + templateNumber.Substring(m.Index + m.Length);
        }

        /// <summary>«Спецификация лист 4» → «Спецификация лист 5»; имя без номера листа не меняется.</summary>
        internal static string MakeName(string templateName, int templatePosition, int newPosition)
        {
            Match m = Regex.Match(templateName, @"^(.*?)(\d+)\s*$");
            if (m.Success && int.TryParse(m.Groups[2].Value, out int v) && v == templatePosition)
                return m.Groups[1].Value + newPosition;
            return templateName;
        }

        private static string Visible(string sheetNumber)
        {
            return sheetNumber.Replace("‪", "");
        }

        private void SetNSheetNumber(ViewSheet sheet, int number)
        {
            Parameter p = sheet.get_Parameter(NSheetNumberParamGuid);
            if (p == null || p.IsReadOnly) return;
            switch (p.StorageType)
            {
                case StorageType.Integer: p.Set(number); break;
                case StorageType.Double: p.Set((double)number); break;
                case StorageType.String: p.Set(number.ToString()); break;
            }
        }

        private void CopyParams(Element src, Element dst)
        {
            foreach (Parameter p in src.Parameters)
            {
                if (p.IsReadOnly || !p.HasValue) continue;
                if (p.IsShared && (p.GUID == NSheetNumberParamGuid || p.GUID == NSheetThroughNumberParamGuid)) continue;
                Parameter target;
                if (p.IsShared) target = dst.get_Parameter(p.GUID);
                else if (p.Definition is InternalDefinition idef && idef.BuiltInParameter != BuiltInParameter.INVALID)
                {
                    if (idef.BuiltInParameter == BuiltInParameter.SHEET_NUMBER || idef.BuiltInParameter == BuiltInParameter.SHEET_NAME
                        || idef.BuiltInParameter == BuiltInParameter.VIEW_NAME) continue;
                    target = dst.get_Parameter(idef.BuiltInParameter);
                }
                else target = dst.LookupParameter(p.Definition.Name);
                if (target == null || target.IsReadOnly || target.StorageType != p.StorageType) continue;
                try
                {
                    switch (p.StorageType)
                    {
                        case StorageType.String: target.Set(p.AsString() ?? ""); break;
                        case StorageType.Integer: target.Set(p.AsInteger()); break;
                        case StorageType.Double: target.Set(p.AsDouble()); break;
                        case StorageType.ElementId: target.Set(p.AsElementId()); break;
                    }
                }
                catch (Exception ex) { Logger.Log("   параметр " + p.Definition.Name + " не скопирован: " + ex.Message, 2); }
            }
        }

        private static Element TitleBlock(Document doc, ViewSheet sheet)
        {
            return new FilteredElementCollector(doc, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType().FirstElement();
        }

        private static FamilySymbol TitleBlockSymbol(Document doc, ViewSheet sheet)
        {
            Element tb = TitleBlock(doc, sheet);
            return tb == null ? null : doc.GetElement(tb.GetTypeId()) as FamilySymbol;
        }

        private static BoundingBoxXYZ TitleBlockBox(Document doc, ViewSheet sheet)
        {
            Element tb = TitleBlock(doc, sheet);
            return tb == null ? null : tb.get_BoundingBox(sheet);
        }

        private static bool IsForm3(FamilySymbol symbol)
        {
            return symbol != null && symbol.FamilyName.Contains("Форма 3");
        }

        /// <summary>Типоразмер Формы 6: самый используемый в модели, иначе любой загруженный.</summary>
        private static FamilySymbol FindForm6(Document doc)
        {
            var used = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType()
                .GroupBy(e => e.GetTypeId())
                .OrderByDescending(g => g.Count())
                .Select(g => doc.GetElement(g.Key) as FamilySymbol)
                .FirstOrDefault(s => s != null && s.FamilyName.Contains("Форма 6"));
            if (used != null) return used;
            return new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>().FirstOrDefault(s => s.FamilyName.Contains("Форма 6"));
        }

        private static bool TryParseMm(string text, out double value)
        {
            bool ok = double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return ok && value > 20;
        }
        #endregion
    }
}
