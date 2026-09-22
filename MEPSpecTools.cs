using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        static Guid adskAreaparamGuid = new Guid("b6a46386-70e9-4b1f-9fdb-8e1e3f18a673");//ADSK_Размер_Площадь
        
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
        public static bool Setadskpparam(ElementId elemid, in string category, in string fileName, in bool countDuctFuttingInsulation = false)
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
                    //новый блок - учет труб с Днар
                    if (viewModel2.naimPar2 == "Размер трубы")
                    {
                        try
                        {
                            InsulationLiningBase insulation = (InsulationLiningBase)elem;
                            Element parentElem = doc.GetElement(insulation.HostElementId);
                            if (parentElem != null && parentElem.GetTypeId().IntValue() > 0)
                            {
                                Element parentElemType = doc.GetElement(parentElem.GetTypeId());
                                string pipeTypeMrkT = Param.GetStringParamValue(doc, BuiltInParameter.WINDOW_TYPE_ID, parentElemType);
                                if (pipeTypeMrkT != null && pipeTypeMrkT.Contains("Днар"))
                                {
                                    double paramDoubleValue = parentElem.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER).AsDouble() * 0.3048 * 1000;
                                    paramDoubleValue = Math.Round(paramDoubleValue, 1);
                                    param2 = "ø" + paramDoubleValue.ToString().Replace(',', '.');
                                }
                            }
                        }
                        catch { }
                    }
                    if (param2 != null && param2.Length > 0) { } //окончание нового блока
                    else
                    {
                        if (elem.LookupParameter(viewModel2.naimPar2).StorageType == StorageType.Double)
                        {
                            double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar2).AsDouble() * 0.3048 * 1000;
                            paramDoubleValue = Math.Round(paramDoubleValue, 1);
                            param2 = paramDoubleValue.ToString().Replace(',', '.');
                        }
                        else param2 = elem.LookupParameter(viewModel2.naimPar2).AsValueString();
                    }
                    //окончание редактирования
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
                    //новый блок - учет труб с Днар
                    if (viewModel2.naimPar3 == "Размер трубы")
                    {
                        try
                        {
                            InsulationLiningBase insulation = (InsulationLiningBase)elem;
                            Element parentElem = doc.GetElement(insulation.HostElementId);
                            if (parentElem != null && parentElem.GetTypeId().IntValue() > 0)
                            {
                                Element parentElemType = doc.GetElement(parentElem.GetTypeId());
                                string pipeTypeMrkT = Param.GetStringParamValue(doc, BuiltInParameter.WINDOW_TYPE_ID, parentElemType);
                                if (pipeTypeMrkT != null && pipeTypeMrkT.Contains("Днар"))
                                {
                                    double paramDoubleValue = parentElem.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER).AsDouble() * 0.3048 * 1000;
                                    paramDoubleValue = Math.Round(paramDoubleValue, 1);
                                    param3 = "ø" + paramDoubleValue.ToString().Replace(',', '.');
                                }
                            }
                        }
                        catch { }
                    }
                    if (param3 != null && param3.Length > 0) { } //окончание нового блока
                    else
                    {
                        if (elem.LookupParameter(viewModel2.naimPar3).StorageType == StorageType.Double)
                        {
                            double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar3).AsDouble() * 0.3048 * 1000;
                            paramDoubleValue = Math.Round(paramDoubleValue, 1);
                            param3 = paramDoubleValue.ToString().Replace(',', '.');
                        }
                        else param3 = elem.LookupParameter(viewModel2.naimPar3).AsValueString();
                    }
                    //окончание редактирования
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
                    //новый блок - учет труб с Днар
                    if (viewModel2.naimPar4 == "Размер трубы")
                    {
                        try
                        {
                            InsulationLiningBase insulation = (InsulationLiningBase)elem;
                            Element parentElem = doc.GetElement(insulation.HostElementId);
                            if (parentElem != null && parentElem.GetTypeId().IntValue() > 0)
                            {
                                Element parentElemType = doc.GetElement(parentElem.GetTypeId());
                                string pipeTypeMrkT = Param.GetStringParamValue(doc, BuiltInParameter.WINDOW_TYPE_ID, parentElemType);
                                if (pipeTypeMrkT != null && pipeTypeMrkT.Contains("Днар"))
                                {
                                    double paramDoubleValue = parentElem.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER).AsDouble() * 0.3048 * 1000;
                                    paramDoubleValue = Math.Round(paramDoubleValue, 1);
                                    param4 = "ø"+paramDoubleValue.ToString().Replace(',', '.');
                                }
                            }
                        }
                        catch { }
                    }
                    if (param4 != null && param4.Length > 0) { } //окончание нового блока
                    else
                    {
                        if (elem.LookupParameter(viewModel2.naimPar4).StorageType == StorageType.Double)
                        {
                            double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar4).AsDouble() * 0.3048 * 1000;
                            paramDoubleValue = Math.Round(paramDoubleValue, 1);
                            param4 = paramDoubleValue.ToString().Replace(',', '.');
                        }
                        else param4 = elem.LookupParameter(viewModel2.naimPar4).AsValueString();
                    }
                    //окончание редактирования
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
                    Logger.Log("число", 2); countValue = 1; Logger.Log("1", 2);
                    break;
                case "Длина":
                    Logger.Log("длина", 2); Parameter paramL = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (paramL != null) countValue = paramL.AsDouble();
                    countValue = countValue * 0.3048; Logger.Log($"{countValue}", 2);
                    break;
                case "Площадь":
                    Logger.Log("площадь", 2); 
                    //новый блок - учет фитингов воздуховодов по площади
                    if(countDuctFuttingInsulation&&elem.Category!=null&&elem.Category.Id.IntValue()== -2008123)
                    {
                        InsulationLiningBase insulation = (InsulationLiningBase)elem;
                        Element parentElem = doc.GetElement(insulation.HostElementId);
                        if (parentElem.Category != null && parentElem.Category.Id.IntValue() == -2008010)
                        {
                            Logger.Log("считаем по хосту", 2);

                            //ищем у хоста параметр Площадь детали, если не нашли - ADSK_Размер_Площадь
                            if (Param.ParamExist("Площадь детали", parentElem))
                            {
                                try { countValue = parentElem.LookupParameter("Площадь детали").AsDouble(); } catch { }
                            }
                            else countValue = Param.GetDoubleParamValue(doc, adskAreaparamGuid, parentElem);
                        }
                        else
                        {
                            Parameter paramA = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
                            if (paramA != null) countValue = paramA.AsDouble();
                        }
                    }
                    //окончание нового блока
                    else
                    {
                        Parameter paramA = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
                        if (paramA != null) countValue = paramA.AsDouble();
                    }
                    //окончание редактирования
                    countValue = countValue * 0.3048 * 0.3048; Logger.Log($"{countValue}", 2);
                    break;
                case "Объем":
                    Logger.Log("объем", 2); Parameter paramV = elem.get_Parameter(BuiltInParameter.RBS_INSULATION_LINING_VOLUME);
                    if (paramV != null) countValue = paramV.AsDouble();
                    countValue = countValue * 0.3048 * 0.3048 * 0.3048; Logger.Log($"{countValue}", 2);
                    break;
                default:
                    Logger.Log("viewModel2.countPar не распознан", 2);break;
            }
            double coeff = 1;
            string vmk = viewModel2.countK; // без замены
            if (double.TryParse(vmk, NumberStyles.Any, CultureInfo.InvariantCulture, out coeff))
            {
                // парсинг успешен
            }
            else
            {
                Logger.Log($"Не удалось распарсить коэффициент: {vmk}", 2);
            }
            countValue = countValue * coeff;
            countValue = Math.Round(countValue, 1); Logger.Log($"итоговое колво {countValue.ToString(CultureInfo.InvariantCulture)}", 2);

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

        public static bool IsIdParamSet(Element elem,string paramName)
        {
            if (Param.ParamExist(paramName, elem) == false) return false;
            Parameter param = elem.LookupParameter(paramName);
            if (param == null) return false;
            if (param.HasValue==false) return false;
            if (param.AsElementId() == null) return false;
#if R2022
            if (param.AsElementId().IntegerValue==-1) return false;
#else
            if (param.AsElementId().Value == -1) return false;
#endif
            return true;
        }

        static readonly BuiltInCategory[] VkovPostcheckCategories =
        {
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_DuctLinings,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_DuctInsulations,
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PlumbingFixtures
        };

        static readonly HashSet<BuiltInCategory> VkovLengthCategories = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_DuctInsulations
        };

        const string VkovSkipNaimValue = "!Не учитывать";
        const double VkovLengthLimitMm = 500;

        public static List<Element> CollectVKOVPostcheckElements(Document doc)
        {
            var result = new List<Element>();
            if (doc == null) return result;
            foreach (BuiltInCategory cat in VkovPostcheckCategories)
            {
                result.AddRange(new FilteredElementCollector(doc)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .ToElements());
            }
            return result;
        }

        public static List<MEPSpecIssueRow> BuildVKOVPostcheckRows(IEnumerable<Element> elements)
        {
            var raw = new List<SSCablePreflightIssue>();
            foreach (Element elem in elements ?? Enumerable.Empty<Element>())
            {
                if (elem == null) continue;

                string naim = GetGuidStringInstanceOrType(elem, adskNparamGuid);
                if (naim != null && naim.Trim() == VkovSkipNaimValue)
                    continue;

                var problems = new List<string>();

                // Изоляция с нерассчитанной «Длиной»: ADSK_Количество = 0 / не назначено — не ошибка
                if (!IsInsulationWithUncalculatedLength(elem))
                {
                    Parameter qtyParam = GetAssignedParamInstanceOrType(elem, adskCparamGuid);
                    if (qtyParam == null)
                        problems.Add("Количество не назначено");
                    else if (IsNumericZero(qtyParam))
                    {
                        if (IsLengthCategory(elem) && TryGetLengthMm(elem, out double lengthMm) && lengthMm > VkovLengthLimitMm)
                            problems.Add("Количество = 0 при длине > 500 мм");
                    }
                }

                Parameter groupParam = GetAssignedParamInstanceOrType(elem, adskGparamGuid);
                string grouping = GetParameterString(groupParam);
                if (groupParam == null || string.IsNullOrWhiteSpace(grouping))
                    problems.Add("Группирование не заполнено");

                if (problems.Count == 0) continue;

                string category = elem.Category != null ? elem.Category.Name : "";
                raw.Add(new SSCablePreflightIssue
                {
                    ElementId = elem.Id,
                    ElementIdText = GetElementIdText(elem.Id),
                    Category = category,
                    Grouping = grouping ?? "",
                    ProblemParams = problems
                });
            }

            return GroupIssueRows(raw);
        }

        /// <summary>
        /// Preflight: если задан «Кабель тип N», параметры RBZ_ПучокN_* должны быть
        /// read-only и заполнены — иначе основной код их проигнорирует.
        /// </summary>
        public static List<MEPSpecIssueRow> BuildSSCableBundlePreflightRows(
            IEnumerable<Element> elements,
            Guid adskGroupGuid)
        {
            var raw = new List<SSCablePreflightIssue>();
            string[] bundleSuffixes = { "Ед.измерения", "Марка", "Описание", "Производитель" };

            foreach (Element elem in elements)
            {
                if (elem == null) continue;

                var problems = new List<string>();
                for (int n = 1; n <= 5; n++)
                {
                    string cableTypeParam = "Кабель тип " + n.ToString();
                    if (!IsIdParamSet(elem, cableTypeParam)) continue;

                    foreach (string suffix in bundleSuffixes)
                    {
                        string paramName = "RBZ_Пучок" + n.ToString() + "_" + suffix;
                        if (!IsBundleParamReady(elem, paramName))
                            problems.Add(paramName);
                    }
                }

                if (problems.Count == 0) continue;

                string grouping = "";
                if (Param.ParamExistByGuid(adskGroupGuid, elem) && elem.get_Parameter(adskGroupGuid).HasValue)
                    grouping = elem.get_Parameter(adskGroupGuid).AsString() ?? "";

                string category = elem.Category != null ? elem.Category.Name : "";
                raw.Add(new SSCablePreflightIssue
                {
                    ElementId = elem.Id,
                    ElementIdText = GetElementIdText(elem.Id),
                    Category = category,
                    Grouping = grouping ?? "",
                    ProblemParams = problems.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList()
                });
            }

            return GroupIssueRows(raw);
        }

        static List<MEPSpecIssueRow> GroupIssueRows(List<SSCablePreflightIssue> raw)
        {
            return raw
                .GroupBy(i => new
                {
                    Grouping = i.Grouping ?? "",
                    Category = i.Category ?? "",
                    Problems = string.Join(", ", i.ProblemParams)
                })
                .Select(g => new MEPSpecIssueRow
                {
                    Grouping = g.Key.Grouping,
                    Category = g.Key.Category,
                    ProblemParams = g.Key.Problems,
                    Count = g.Count(),
                    ElementIds = g.Select(x => x.ElementId).ToList(),
                    ElementIdsText = string.Join(", ", g.Select(x => x.ElementIdText))
                })
                .OrderBy(r => r.Grouping, StringComparer.Ordinal)
                .ThenBy(r => r.ProblemParams, StringComparer.Ordinal)
                .ThenBy(r => r.Category, StringComparer.Ordinal)
                .ToList();
        }

        static Parameter GetAssignedParamInstanceOrType(Element elem, Guid guid)
        {
            if (elem == null) return null;
            Parameter instance = null;
            if (Param.ParamExistByGuid(guid, elem))
                instance = elem.get_Parameter(guid);
            if (instance != null && instance.HasValue)
                return instance;

            ElementId typeId = elem.GetTypeId();
            if (typeId != null)
            {
                Element type = elem.Document.GetElement(typeId);
                if (type != null && Param.ParamExistByGuid(guid, type))
                {
                    Parameter typeParam = type.get_Parameter(guid);
                    if (typeParam != null && typeParam.HasValue)
                        return typeParam;
                }
            }

            return null;
        }

        static string GetGuidStringInstanceOrType(Element elem, Guid guid)
        {
            if (elem == null) return null;
            Parameter instance = null;
            if (Param.ParamExistByGuid(guid, elem))
                instance = elem.get_Parameter(guid);
            string instanceVal = GetParameterString(instance);
            if (!string.IsNullOrWhiteSpace(instanceVal))
                return instanceVal;

            ElementId typeId = elem.GetTypeId();
            if (typeId == null) return instanceVal;
            Element type = elem.Document.GetElement(typeId);
            if (type == null || !Param.ParamExistByGuid(guid, type))
                return instanceVal;
            string typeVal = GetParameterString(type.get_Parameter(guid));
            return !string.IsNullOrWhiteSpace(typeVal) ? typeVal : instanceVal;
        }

        static string GetParameterString(Parameter p)
        {
            if (p == null || !p.HasValue) return null;
            string value = p.AsString();
            if (value == null) value = p.AsValueString();
            return value;
        }

        static bool IsNumericZero(Parameter p)
        {
            if (p == null || !p.HasValue) return false;
            if (p.StorageType == StorageType.Double)
                return Math.Abs(p.AsDouble()) < 0.0000001;
            if (p.StorageType == StorageType.Integer)
                return p.AsInteger() == 0;
            return false;
        }

        static bool IsLengthCategory(Element elem)
        {
            if (elem?.Category == null) return false;
#if R2022
            int catId = elem.Category.Id.IntegerValue;
#else
            int catId = (int)elem.Category.Id.Value;
#endif
            return VkovLengthCategories.Contains((BuiltInCategory)catId);
        }

        static bool IsInsulationWithUncalculatedLength(Element elem)
        {
            if (!(elem is InsulationLiningBase)) return false;
            Parameter dlina = elem.LookupParameter("Длина");
            return dlina != null && !dlina.HasValue;
        }

        static bool TryGetLengthMm(Element elem, out double lengthMm)
        {
            lengthMm = 0;
            if (elem == null) return false;

            if (elem is InsulationLiningBase)
            {
                Parameter isolLength = elem.LookupParameter("Длина");
                if (isolLength != null && !isolLength.HasValue)
                    return false;
                if (isolLength != null && isolLength.HasValue && isolLength.StorageType == StorageType.Double)
                {
                    lengthMm = isolLength.AsDouble() * 304.8;
                    return true;
                }
            }

            Parameter curveLength = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            if (curveLength != null && curveLength.HasValue)
            {
                lengthMm = curveLength.AsDouble() * 304.8;
                return true;
            }

            Parameter dlina = elem.LookupParameter("Длина");
            if (dlina != null && dlina.HasValue && dlina.StorageType == StorageType.Double)
            {
                lengthMm = dlina.AsDouble() * 304.8;
                return true;
            }

            return false;
        }

        static string GetElementIdText(ElementId id)
        {
            if (id == null) return "";
#if R2022
            return id.IntegerValue.ToString();
#else
            return id.Value.ToString();
#endif
        }

        static bool IsBundleParamReady(Element elem, string paramName)
        {
            if (!Param.ParamExist(paramName, elem)) return false;
            Parameter prm = elem.LookupParameter(paramName);
            if (prm == null) return false;
            if (!prm.IsReadOnly) return false;
            if (!prm.HasValue) return false;
            string value = prm.AsString();
            return !string.IsNullOrWhiteSpace(value);
        }

        public static string BuildSSCableTrayTypeKey(Element c, Guid adskGroupGuid)
        {
#if R2022
            return
                c.get_Parameter(adskGroupGuid).AsString() +
                c.LookupParameter("Кабель тип 1").AsElementId().IntegerValue.ToString() + c.LookupParameter("Кабель 1 Группирование").AsString() +
                c.LookupParameter("Кабель тип 2").AsElementId().IntegerValue.ToString() + c.LookupParameter("Кабель 2 Группирование").AsString() +
                c.LookupParameter("Кабель тип 3").AsElementId().IntegerValue.ToString() + c.LookupParameter("Кабель 3 Группирование").AsString() +
                c.LookupParameter("Кабель тип 4").AsElementId().IntegerValue.ToString() + c.LookupParameter("Кабель 4 Группирование").AsString() +
                c.LookupParameter("Кабель тип 5").AsElementId().IntegerValue.ToString() + c.LookupParameter("Кабель 5 Группирование").AsString();
#else
            return
                c.get_Parameter(adskGroupGuid).AsString() +
                c.LookupParameter("Кабель тип 1").AsElementId().Value.ToString() + c.LookupParameter("Кабель 1 Группирование").AsString() +
                c.LookupParameter("Кабель тип 2").AsElementId().Value.ToString() + c.LookupParameter("Кабель 2 Группирование").AsString() +
                c.LookupParameter("Кабель тип 3").AsElementId().Value.ToString() + c.LookupParameter("Кабель 3 Группирование").AsString() +
                c.LookupParameter("Кабель тип 4").AsElementId().Value.ToString() + c.LookupParameter("Кабель 4 Группирование").AsString() +
                c.LookupParameter("Кабель тип 5").AsElementId().Value.ToString() + c.LookupParameter("Кабель 5 Группирование").AsString();
#endif
        }

        public static string BuildSSConduitTypeKey(Element c, Guid adskGroupGuid)
        {
#if R2022
            return BuildSSCableTrayTypeKey(c, adskGroupGuid) +
                c.LookupParameter("Труба").AsElementId().IntegerValue.ToString() +
                c.LookupParameter("Крепеж").AsElementId().IntegerValue.ToString();
#else
            return BuildSSCableTrayTypeKey(c, adskGroupGuid) +
                c.LookupParameter("Труба").AsElementId().Value.ToString() +
                c.LookupParameter("Крепеж").AsElementId().Value.ToString();
#endif
        }

        public static List<SSTypePreviewRow> BuildSSTypePreviewRows(
            IEnumerable<Element> conduits,
            IEnumerable<Element> cableTrays,
            IList<string> conduitTypes,
            IList<string> cableTrayTypes,
            Guid adskGroupGuid)
        {
            var rows = new List<SSTypePreviewRow>();

            foreach (string cType in conduitTypes ?? new List<string>())
            {
                var ids = new List<ElementId>();
                Element first = null;
                foreach (Element c in conduits)
                {
                    if (c == null) continue;
                    try
                    {
                        if (BuildSSConduitTypeKey(c, adskGroupGuid) != cType) continue;
                    }
                    catch
                    {
                        continue;
                    }
                    ids.Add(c.Id);
                    if (first == null) first = c;
                }
                rows.Add(CreateSSTypePreviewRow("Короб", first, ids, adskGroupGuid));
            }

            foreach (string cType in cableTrayTypes ?? new List<string>())
            {
                var ids = new List<ElementId>();
                Element first = null;
                foreach (Element c in cableTrays)
                {
                    if (c == null) continue;
                    try
                    {
                        if (BuildSSCableTrayTypeKey(c, adskGroupGuid) != cType) continue;
                    }
                    catch
                    {
                        continue;
                    }
                    ids.Add(c.Id);
                    if (first == null) first = c;
                }
                rows.Add(CreateSSTypePreviewRow("Лоток", first, ids, adskGroupGuid));
            }

            return rows;
        }

        static SSTypePreviewRow CreateSSTypePreviewRow(
            string category,
            Element first,
            List<ElementId> ids,
            Guid adskGroupGuid)
        {
            string grouping = "";
            if (first != null && Param.ParamExistByGuid(adskGroupGuid, first) && first.get_Parameter(adskGroupGuid).HasValue)
                grouping = first.get_Parameter(adskGroupGuid).AsString() ?? "";

            return new SSTypePreviewRow
            {
                Category = category,
                Grouping = grouping ?? "",
                Count = ids.Count,
                ElementIds = ids,
                Parameters = CollectSSTypePreviewParameters(first)
            };
        }

        static List<SSTypeParamItem> CollectSSTypePreviewParameters(Element elem)
        {
            var result = new List<SSTypeParamItem>();
            if (elem == null) return result;

            Document doc = elem.Document;
            TryAddIdParam(result, doc, elem, "Труба");
            TryAddIdParam(result, doc, elem, "Крепеж");
            for (int n = 1; n <= 5; n++)
            {
                string cableTypeName = "Кабель тип " + n.ToString();
                if (!IsIdParamSet(elem, cableTypeName)) continue;
                string typeValue = FormatParameterValue(doc, elem.LookupParameter(cableTypeName));
                if (string.IsNullOrWhiteSpace(typeValue)) continue;

                string grouping = "";
                Parameter groupingParam = elem.LookupParameter("Кабель " + n.ToString() + " Группирование");
                if (groupingParam != null && groupingParam.HasValue)
                    grouping = groupingParam.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(grouping))
                    grouping = FormatParameterValue(doc, groupingParam);

                string value = string.IsNullOrWhiteSpace(grouping)
                    ? typeValue
                    : typeValue + " - " + grouping;
                result.Add(new SSTypeParamItem { Name = cableTypeName, Value = value });
            }
            return result;
        }

        static void TryAddIdParam(List<SSTypeParamItem> result, Document doc, Element elem, string paramName)
        {
            if (!IsIdParamSet(elem, paramName)) return;
            string value = FormatParameterValue(doc, elem.LookupParameter(paramName));
            if (string.IsNullOrWhiteSpace(value)) return;
            result.Add(new SSTypeParamItem { Name = paramName, Value = value });
        }

        static string FormatParameterValue(Document doc, Parameter p)
        {
            if (p == null || !p.HasValue) return "";
            try
            {
                string vs = p.AsValueString();
                if (!string.IsNullOrWhiteSpace(vs)) return vs;

                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.AsString() ?? "";
                    case StorageType.Integer:
                        return p.AsInteger().ToString();
                    case StorageType.Double:
                        return p.AsDouble().ToString(CultureInfo.InvariantCulture);
                    case StorageType.ElementId:
                        ElementId id = p.AsElementId();
                        if (id == null) return "";
#if R2022
                        if (id.IntegerValue == -1) return "";
#else
                        if (id.Value == -1) return "";
#endif
                        Element e = doc != null ? doc.GetElement(id) : null;
                        if (e == null) return "";
                        ElementType et = e as ElementType;
                        if (et != null)
                        {
                            if (!string.IsNullOrWhiteSpace(et.FamilyName))
                                return et.FamilyName + " : " + et.Name;
                            return et.Name ?? "";
                        }
                        return e.Name ?? "";
                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }

    }

    public class SSCablePreflightIssue
    {
        public ElementId ElementId;
        public string ElementIdText;
        public string Category;
        public string Grouping;
        public List<string> ProblemParams;
    }

    public class MEPSpecIssueRow
    {
        public string Grouping { get; set; }
        public string Category { get; set; }
        public string ProblemParams { get; set; }
        public int Count { get; set; }
        public string ElementIdsText { get; set; }
        public List<ElementId> ElementIds { get; set; }
    }

    public class SSTypeParamItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class SSTypePreviewRow
    {
        public string Category { get; set; }
        public string Grouping { get; set; }
        public int Count { get; set; }
        public List<ElementId> ElementIds { get; set; }
        public List<SSTypeParamItem> Parameters { get; set; }
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
        public string adskPrim;
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
        public string AdskPrim {  get; set; }
    }

}
