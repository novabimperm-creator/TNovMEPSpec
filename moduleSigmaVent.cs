using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using TNovCommon;

namespace TNovMEPSpec
{
    /// <summary>
    /// Каталог Сигма-Вент (арматура воздуховодов): марка и наименование клапанов по СВ_-параметрам семейств.
    /// Перенесено из графов Dynamo «СB_Наименование_кассеты_r2017» и «СВ_марки_r2017_v1».
    /// Тип отбирается по «Комментарии к типоразмеру».
    /// </summary>
    public static class moduleSigmaVent
    {
        static Guid NGNparamGuid = new Guid("cb7d14c9-43a3-451a-926c-f569df8b8c03");//N_Группирование_Не перезаполнять
        static Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        static Guid adskMarkparamGuid = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d");//ADSK_Марка
        static Guid adskCodeparamGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");//ADSK_Код изделия

        const string CommentCassette = "Сигмавент"; //кассетный клапан
        const string CommentValve = "Сигмавент_клапан"; //прочие клапаны

        static readonly string[] CassetteNameSizeParams =
        {
            "СВ_ТипоразмерА", "СВ_Х", "СВ_ТипоразмерВ", "Тире",
            "СВ_КоличествоПриводов", "СВ_ПрименяемыйПривод", "СВ_НапряжениеПитанияПривода"
        };
        static readonly string[] CassetteMarkParams =
        {
            "СВ_ПределОгнестойкости", "Тире", "СВ_ТипоразмерА", "СВ_Х", "СВ_ТипоразмерВ", "Тире",
            "СВ_КоличествоПриводов", "СВ_ПрименяемыйПривод", "СВ_НапряжениеПитанияПривода",
            "СВ_КомплектацияКлеммнойКолодкой", "СВ_ВзрывозащищенноеИсполнение"
        };
        static readonly string[] ValveMarkParams =
        {
            "СВ_МодификацияКлапана_НО_Или_НЗ", "СВ_ТипоразмерD", "СВ_ТипоразмерА", "СВ_Х", "СВ_ТипоразмерВ",
            "СВ_Исполнение", "СВ_ИсполнениеСУкороченнымКорпусом", "СВ_КлассОчистки", "СВ_РасположениеПривода",
            "СВ_ПрименяемыйПривод", "СВ_НапряжениеПитанияПривода", "СВ_МорозостойкоеИсполнение",
            "СВ_КомплектацияКлеммнойКолодкой", "СВ_ВзрывозащищенноеИсполнение"
        };

        /// <summary>true — элемент не из каталога Сигма-Вент или параметры записаны; false — ошибка записи.</summary>
        public static bool SetParams(Element elem)
        {
            Element eType = elem.Document.GetElement(elem.GetTypeId());
            string comment = eType?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.AsString() ?? "";
            bool cassette = comment == CommentCassette;
            if (!cassette && comment != CommentValve) return true;

            Parameter ngn = elem.get_Parameter(NGNparamGuid);
            if (ngn != null && ngn.HasValue && ngn.AsInteger() == 1) { Logger.Log("   Элемент " + elem.Id.ToString() + ": пропуск, не перезаполнять", 2); return true; }

            if (cassette)
            {
                string nonz = Txt(elem, "НормальноОткрытый") == "1" ? "-НО" : "-НЗ";
                string sizes = string.Concat(CassetteNameSizeParams.Select(n => Txt(elem, n).Split('.')[0]));
                string naim = "Клапан кассетный Сигмавент" + Txt(elem, "СВ_ПределОгнестойкости") + nonz + "-" + sizes +
                              " исп." + Txt(elem, "Количество в ряд") + "-" + Txt(elem, "Количество в высоту");
                string code = elem.get_Parameter(adskCodeparamGuid)?.AsString() ?? eType.get_Parameter(adskCodeparamGuid)?.AsString() ?? "";
                string mark = code + nonz + string.Concat(CassetteMarkParams.Select(n => Txt(elem, n)));
                bool ok1 = Set(elem, adskNparamGuid, "ADSK_Наименование", Cyr(naim));
                bool ok2 = Set(elem, adskMarkparamGuid, "ADSK_Марка", Cyr(mark));
                return ok1 && ok2;
            }

            string valveMark = "Сигмавент" + Txt(elem, "СВ_ПределОгнестойкости") + string.Concat(ValveMarkParams.Select(n => Txt(elem, n)));
            return Set(elem, adskMarkparamGuid, "ADSK_Марка", Cyr(valveMark));
        }

        /// <summary>Значение параметра экземпляра (или типа) как текст; числа — в единицах проекта, без хвостовых нулей.</summary>
        static string Txt(Element elem, string name)
        {
            Parameter p = elem.LookupParameter(name);
            if (p == null) { Element t = elem.Document.GetElement(elem.GetTypeId()); p = t?.LookupParameter(name); }
            if (p == null || !p.HasValue) return "";
            switch (p.StorageType)
            {
                case StorageType.String: return p.AsString() ?? "";
                case StorageType.Integer: return p.AsInteger().ToString();
                case StorageType.Double:
                    double v = p.AsDouble();
                    try { v = UnitUtils.ConvertFromInternalUnits(v, p.GetUnitTypeId()); } catch { }
                    return v.ToString("0.######", CultureInfo.InvariantCulture);
                default: return p.AsValueString() ?? "";
            }
        }

        /// <summary>Латинская x в размерах (1000x200, -x150) — на кириллическую х; обозначения приводов (SVE, DA…) не трогаем.</summary>
        static string Cyr(string s) => Regex.Replace(s, @"(?<![A-Za-z])x(?=\d)", "х");

        static bool Set(Element elem, Guid guid, string name, string value)
        {
            Parameter p = elem.get_Parameter(guid);
            if (p == null) { Logger.Log($"      {elem.Id} Ошибка Сигма-Вент: параметра {name} нет", 4); return false; }
            //параметр задан формулой семейства (стакан СВС, гибкая вставка ОГВ) — значение формирует само семейство
            if (p.IsReadOnly) { Logger.Log($"   Элемент {elem.Id}: {name} задан в семействе, пропуск", 2); return true; }
            p.Set(value);
            Logger.Log($"   Элемент {elem.Id}: {name} = {value}", 2);
            return true;
        }
    }
}
