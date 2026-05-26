using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;
using static Autodesk.Revit.DB.SpecTypeId;

namespace TNovMEPSpec
{
    public static class MEPSpecTools
    {
        #region Параметры
        //Параметры
        static Guid NGNparamGuid = new Guid("cb7d14c9-43a3-451a-926c-f569df8b8c03");//N_Группирование_Не перезаполнять
        static Guid NCatparamGuid = new Guid("e71ab526-6b0b-4c3f-9b52-ba7f61a83d46");//N_Категория
        static Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование
        static Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        static Guid adskMarkparamGuid = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d");//ADSK_Марка
        static Guid adskOboznparamGuid = new Guid("9c98831b-9450-412d-b072-7d69b39f4029");//ADSK_Обозначение
        static Guid adskCodeparamGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");//ADSK_Код изделия
        static Guid adskManufparamGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");//ADSK_Завод-изготовитель
        static Guid adskEdparamGuid = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d");//ADSK_Единица измерения
        static Guid adskCparamGuid = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a");//ADSK_Количество
        static Guid NEGparamGuid = new Guid("837842da-379d-496f-9ef3-be8886a0161f");//N_ЭЛ.Группирование ЭЛ
        static Guid NSortparamGuid = new Guid("dbd21888-5efd-4e29-8722-2fe8c6d4f799");//N_Сортировка
        static Guid OSetparamGuid = new Guid("8dd021be-382d-4776-afd4-75996e351de3");//О_Комплект
        
        #endregion

        public static void MEPSpecOVVKBaseParams(in string mark, in string fileName, MEPSpecOVVKParamsViewModel viewModel2)
        {
            //базовые значения
            if (mark.Contains("Трубы"))
            {
                viewModel2.naimPrefix2 = " ø";
                if (fileName.Contains("-ОВ") || fileName.Contains("_ОВ")) viewModel2.naimPrefix3 = "х";
                if (fileName.Contains("-ОВ") || fileName.Contains("_ОВ")) viewModel2.naimPar3 = "ADSK_Толщина стенки";
                if (mark.Contains("Днар")) viewModel2.naimPar2 = "Внешний диаметр";
                else viewModel2.naimPar2 = "Диаметр";
                viewModel2.countK = "1.1";
                viewModel2.countPar = "Длина";
            }
            else if (mark.Contains("Материалы изоляции труб"))
            {
                viewModel2.naimPrefix2 = ", b=";
                viewModel2.naimPar2 = "Толщина изоляции";
                viewModel2.naimPrefix3 = " для ";
                viewModel2.naimPar3 = "Размер трубы";
                viewModel2.countPar = "Длина";
                viewModel2.countK = "1.3";
                if (mark.Contains("Цилиндры"))
                {
                    viewModel2.countK = "1";
                    viewModel2.countPar = "Объем";
                }
                else if (mark.Contains("Трубки"))
                {
                    viewModel2.countK = "1.1";
                }
            }
            else if (mark.Contains("Гибкие трубы"))
            {
                viewModel2.naimPrefix2 = " ø";
                viewModel2.naimPar2 = "Диаметр";
                viewModel2.countPar = "Длина";
                if (mark.Contains("Подводка стальная")) viewModel2.naimPar2 = "Внешний диаметр";
            }
            else if (mark.Contains("Воздуховоды"))
            {
                viewModel2.naimPrefix2 = " ";
                viewModel2.naimPar2 = "Размер";
                viewModel2.countPar = "Длина";
                if (mark.Contains("Пластик")) { }
                else
                {
                    viewModel2.naimPrefix3 = ", b=";
                    viewModel2.naimPar3 = "ADSK_Толщина стенки";
                    viewModel2.naimPrefix4 = ", класс герметичности ";
                    viewModel2.naimPar4 = "Класс герметичности";
                }
            }
            else if (mark.Contains("Материалы изоляции воздуховодов"))
            {
                viewModel2.naimPrefix2 = " ";
                if (mark.Contains("Огнезащита")) { }
                else viewModel2.naimPar2 = "Толщина изоляции";
            }
            else if (mark.Contains("Гибкие воздуховоды"))
            {
                viewModel2.naimPrefix2 = " ";
                viewModel2.naimPar2 = "Размер";
                viewModel2.countPar = "Длина";
            }
            else if (mark.Contains("Соединительные детали воздуховодов"))
            {
                viewModel2.naimPrefix2 = " ";
                viewModel2.naimPar2 = "ADSK_Размер_УголПоворота";
                viewModel2.naimPrefix3 = " ";
                viewModel2.naimPar2 = "Размер";
            }
        }
        public static bool Setadskgparam(ElementId elemid, in string paramname, in bool systemcut)
        {
            string eid = elemid.ToString();
            Element elem = RevitAPI.Document.GetElement(elemid);
            Logger.Log("   Элемент " + eid + ":", 2);
            Parameter param0 = elem.LookupParameter(paramname);
            //отбрасываем элементы с пустым исходным параметром
            if (param0 == null || param0.HasValue == false)
            {
                Logger.Log("      Пропуск: исходный параметр пуст", 2); return true;
            }
            //отбрасываем элементы с "Не перезаполнять"
            if (Param.ParamExistByGuid(NGNparamGuid, elem))
            {
                Parameter NGNparam = elem.get_Parameter(NGNparamGuid);
                if(NGNparam.HasValue&& NGNparam.AsInteger()==1) { Logger.Log("      Пропуск: не перезаполнять", 2); return true; }
            }
            //05.2026 - убрано отбрасывание элементов с наименованием не учитывать

            bool adskgParamExist = Param.ParamExistByGuid(adskGparamGuid, elem);
            if (!adskgParamExist)
            {
                Logger.Log($"      {eid} Ошибка: параметра ADSK_Группирование нет", 4);
                return false;
            }
            Parameter param = elem.get_Parameter(adskGparamGuid);
            if (param.IsReadOnly) 
            {
                Logger.Log($"      {eid} Ошибка: параметр ADSK_Группирование доступен только для чтения", 4);
                return false; 
            }

            string system = elem.LookupParameter(paramname).AsValueString(); //получаем значение исходного параметра

            if (systemcut && system.Contains(","))
            {
                string[] systemParts = system.Split(',');
                bool systemK = false; //добавлено 05.2026 - каналья в приоритете
                foreach (string systemPart in systemParts)
                {
                    if (systemPart.StartsWith("К"))
                    {
                        systemK = true; system = systemPart; break;
                    }
                }
                if (!systemK) system = systemParts[0];

                //добавлено 05.2026
                int spaceIndex = system.IndexOf(' ');
                if (spaceIndex != -1)
                {
                    string prefix = system.Substring(0, spaceIndex);
                    // Если в части до пробела есть хотя бы одна цифра – оставляем только её
                    if (prefix.Any(char.IsDigit))
                        system = prefix;
                }
            }

            try
            {
                elem.get_Parameter(adskGparamGuid)?.Set(system);
                Logger.Log("      назначено " + system, 2);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"      {eid} Ошибка: {ex.Message}", 4);
                return false;
            }
        }
        public static bool SetNCategory(ElementId elemid)
        {
            string eid = elemid.ToString();
            Element elem = RevitAPI.Document.GetElement(elemid);
            Logger.Log("   Элемент " + eid + ":", 2);

            if(Param.ParamExistByGuid(NCatparamGuid, elem)==false)
            {
                Logger.Log("      Ошибка: параметра N_Категория нет", 4);
                return false;
            }  

            if (elem.get_Parameter(NCatparamGuid).IsReadOnly)
            {
                Logger.Log($"      {eid} Ошибка: параметр N_Категория доступен только для чтения", 4);
                return false;
            }
            
            string category = elem.Category.Name;
            string Ncategory = "6. Материалы и прочие элементы";
            switch (category)
            {
                case "Материалы изоляции труб":
                    Ncategory = "5. Изоляционные материалы"; break;
                case "Материалы изоляции воздуховодов":
                    Ncategory = "5. Изоляционные материалы"; break;
                case "Материалы внутренней изоляции воздуховодов":
                    Ncategory = "5. Изоляционные материалы"; break;
                case "Спринклеры":
                    Ncategory = "2. Спринклеры"; break;
                case "Арматура трубопроводов":
                    Ncategory = "3. Арматура"; break;
                case "Арматура воздуховодов":
                    Ncategory = "3. Арматура"; break;
                case "Воздухораспределители":
                    Ncategory = "2. Воздухораспределители"; break;
                case "Гибкие воздуховоды":
                    Ncategory = "4. Воздуховоды"; break;
                case "Воздуховоды":
                    Ncategory = "4. Воздуховоды"; break;
                case "Соединительные детали воздуховодов":
                    Ncategory = "4. Воздуховоды"; break;
                case "Соединительные детали трубопроводов":
                    Ncategory = "4. Трубопроводы"; break;
                case "Оборудование":
                    Ncategory = "1. Оборудование"; break;
                case "Трубы":
                    Ncategory = "4. Трубопроводы"; break;
                case "Гибкие трубы":
                    Ncategory = "4. Трубопроводы"; break;
                case "Сантехнические приборы":
                    Ncategory = "2. Сантехнические приборы"; break;
            }

            try
            {
                elem.get_Parameter(NCatparamGuid)?.Set(Ncategory); 
                Logger.Log("      N_Категория " + Ncategory, 2);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"      {eid} Ошибка: {ex.Message}", 4);
                return false;
            }
        }
        public static bool Setadskpparam(ElementId elemid, in string category, in string fileName)
        {
            string eid = elemid.ToString();
            Element elem = RevitAPI.Document.GetElement(elemid);
            Logger.Log("   Элемент " + eid + ":", 2);

            //вьюмодель
            Element type = RevitAPI.Document.GetElement(elem.GetTypeId());

            string mark = "пустая маркировка"; 
            try
            {
                mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
            }
            catch (Exception ex)
            {
                Logger.Log($"      {eid} Ошибка получения Маркировки типоразмера: {ex.Message}", 4);
            }
            if (mark == null || mark.Length == 0)
            {
                mark = "пустая маркировка";
            }
            
            mark = category + "_" + mark;
            MEPSpecOVVKParamsViewModel viewModel2 = new MEPSpecOVVKParamsViewModel();
            viewModel2.elemType = mark;
            // Десериализация
            bool forProject2 = true;
            string VMName = "ADSK Параметры_" + mark;
            json js = new json(in VMName, in forProject2, out bool canserialize2, out string jsonpath2);
            if (canserialize2)
            {
                viewModel2 = JsonConvert.DeserializeObject<MEPSpecOVVKParamsViewModel>(File.ReadAllText(jsonpath2));
                Logger.Log("Десериализация прошла успешно: " + VMName, 2);
            }
            else
            {
                //базовые значения
                MEPSpecTools.MEPSpecOVVKBaseParams(mark, fileName, viewModel2);
                //Сериализация
                try
                {
                    File.WriteAllText(jsonpath2, JsonConvert.SerializeObject(viewModel2));
                    Logger.Log("Cериализация прошла успешно: " + VMName, 2);
                }
                catch (Exception e) { Logger.Log("Ошибка сериализации: " + e.Message, 4); }
            }
            //вычисление Наименования
            string naimValue = "";
            Document doc = RevitAPI.Document;
            string param1 = Param.GetStringParamValue(doc, viewModel2.naimPar1, elem); //1-й параметр - строка, по типу либо экз

            naimValue = param1;

            if (viewModel2.naimPar2 != "выкл")
            {
                bool param2exist = Param.ParamExist(viewModel2.naimPar2, elem);
                if (param2exist)
                {
                    string param2 = "";
                    if (elem.LookupParameter(viewModel2.naimPar2).StorageType == StorageType.Double)
                    {
                        double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar2).AsDouble() * 0.3048 * 1000;
                        paramDoubleValue = Math.Round(paramDoubleValue, 1);
                        param2 = paramDoubleValue.ToString().Replace(',', '.');
                    }
                    else param2 = elem.LookupParameter(viewModel2.naimPar2).AsValueString();
                    if (param2 != null && param2.Length > 0)
                    {
                        if (viewModel2.naimPrefix2 != null && viewModel2.naimPrefix2.Length > 0) naimValue = naimValue + viewModel2.naimPrefix2;
                        naimValue = naimValue + param2;
                    }
                }
            }

            if (viewModel2.naimPar3 != "выкл")
            {
                bool param3exist = Param.ParamExist(viewModel2.naimPar3, elem);
                if (param3exist)
                {
                    string param3 = "";
                    if (elem.LookupParameter(viewModel2.naimPar3).StorageType == StorageType.Double)
                    {
                        double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar3).AsDouble() * 0.3048 * 1000;
                        paramDoubleValue = Math.Round(paramDoubleValue, 1);
                        param3 = paramDoubleValue.ToString().Replace(',', '.');
                    }
                    else param3 = elem.LookupParameter(viewModel2.naimPar3).AsValueString();
                    if (param3 != null && param3.Length > 0)
                    {
                        if (viewModel2.naimPrefix3 != null && viewModel2.naimPrefix3.Length > 0) naimValue = naimValue + viewModel2.naimPrefix3;
                        naimValue = naimValue + param3;
                    }
                }
            }

            if (viewModel2.naimPar4 != "выкл")
            {
                bool param4exist = Param.ParamExist(viewModel2.naimPar4, elem);
                if (param4exist)
                {
                    string param4 = "";
                    if (elem.LookupParameter(viewModel2.naimPar4).StorageType == StorageType.Double)
                    {
                        double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar4).AsDouble() * 0.3048 * 1000;
                        paramDoubleValue = Math.Round(paramDoubleValue, 1);
                        param4 = paramDoubleValue.ToString().Replace(',', '.');
                    }
                    else param4 = elem.LookupParameter(viewModel2.naimPar4).AsValueString();
                    if (param4 != null && param4.Length > 0)
                    {
                        if (viewModel2.naimPrefix4 != null && viewModel2.naimPrefix4.Length > 0) naimValue = naimValue + viewModel2.naimPrefix4;
                        naimValue = naimValue + param4;
                    }
                }
            }

            //вычисление Количества
            double countValue = 0;
            switch (viewModel2.countPar)
            {
                case "Число":
                    countValue = 1;
                    break;
                case "Длина":
                    Parameter paramL = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (paramL != null) countValue = paramL.AsDouble();
                    countValue = countValue * 0.3048;
                    break;
                case "Площадь":
                    Parameter paramA = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
                    if (paramA != null) countValue = paramA.AsDouble();
                    countValue = countValue * 0.3048 * 0.3048;
                    break;
                case "Объем":
                    Parameter paramV = elem.get_Parameter(BuiltInParameter.RBS_INSULATION_LINING_VOLUME);
                    if (paramV != null) countValue = paramV.AsDouble();
                    countValue = countValue * 0.3048 * 0.3048 * 0.3048;
                    break;
            }
            double coeff = 1;
            string vmk = viewModel2.countK.Replace('.', ',');
            Double.TryParse(vmk, out coeff);
            countValue = countValue * coeff;
            countValue = Math.Round(countValue, 1);

            //заполнение параметров
            bool success1 = false;
            //bool adskNparamexist = param.ParamExist("ADSK_Наименование", elem);
            bool adskNparamexist = Param.ParamExistByGuid(adskNparamGuid, elem);
            if (adskNparamexist)
            {
                bool isReadOnly = elem.get_Parameter(adskNparamGuid).IsReadOnly;
                if (!isReadOnly)
                {
                    try
                    {
                        elem.get_Parameter(adskNparamGuid)?.Set(naimValue);
                        success1 = true;
                        Logger.Log("      назначено " + naimValue, 2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"      {eid} Ошибка: {ex.Message}", 4);
                    }
                }
                else success1 = true;
            }
            else
            {
                bool adskCparamexistType = Param.ParamExistByGuid(adskNparamGuid, type);
                if (adskCparamexistType) //наименование назначено по типу
                {
                    success1 = true;
                }
            }
            bool success2 = false;
            //bool adskCparamexist = param.ParamExist("ADSK_Количество", elem);
            bool adskCparamexist = Param.ParamExistByGuid(adskCparamGuid, elem);
            if (adskCparamexist)
            {
                double currentC = elem.get_Parameter(adskCparamGuid).AsDouble();
                if (currentC == 1 && countValue == 1) //количество назначено 1 по экз
                {
                    success2 = true;
                }
                else
                {
                    try
                    {
                        elem.get_Parameter(adskCparamGuid)?.Set(countValue);
                        success2 = true;
                        Logger.Log("      назначено " + countValue.ToString(), 2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"      {eid} Ошибка: {ex.Message}", 4);
                    }
                }

            }
            else
            {
                bool adskCparamexistType = Param.ParamExistByGuid(adskCparamGuid, type); //количество назначено 1 по типу
                if (adskCparamexistType)
                {
                    double currentC = type.get_Parameter(adskCparamGuid).AsDouble();
                    if (currentC == 1 && countValue == 1) //количество назначено 1 по экз
                    {
                        success2 = true;
                    }
                }
            }
            bool success = success1 && success2;
            return success;


        }

    }
    public class ConduitCube
    {
        public string Name;
        public string ADSKGroup;
        public List<string> StringValues;
        public List<double> DoubleValues;
        public List<string> CableGroupStringValues;
    }

    public class ElNonModelCube
    {
        public string adskGroup;
        public string adskNaim;
        public string adskMark;
        public string adskObozn;
        public string adskCode;
        public string adskManuf;
        public string adskEd;
        public string NEGroup;
        public string NSort;
        public double adskC;
        public string OSet;
        public string NCableWay;
    }

    public sealed class ElementInfo
    {
        public ElementId Id { get; set; }
        public string AdskNaim { get; set; }
        public string Mark { get; set; }
        public string Neg { get; set; }
        public string OSet { get; set; }
        public double Count { get; set; }
        public string NCableWay {  get; set; }
        public string AdskGroup { get; set; }
        public string NSort { get; set; }
        public string Obozn { get; set; }
        public string Code { get; set; }
        public string Manuf { get; set; }
        public string Ed { get; set; }
    }

}
