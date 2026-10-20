using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TNovCommon;
using TNovCommon.Server;

namespace TNovMEPSpec
{
    /// <summary>
    /// Каталог немоделируемых MEPCatalog.json в корне серверной папки плагина. Если файла ещё нет,
    /// он создаётся из стартового каталога, встроенного в сборку.
    /// </summary>
    public sealed class NonModelCatalogService
    {
        public const string FileName = "MEPCatalog.json";
        private readonly string _path;
        public NonModelCatalogService(string path) { _path = path; }
        public NonModelCatalogService() : this(ServerData.PathOf(FileName)) { }

        public string Path => _path;

        public IList<NonModelCatalogRow> Load()
        {
            string json = File.Exists(_path) ? File.ReadAllText(_path) : ReadSeed();
            return JsonConvert.DeserializeObject<List<NonModelCatalogRow>>(json ?? "") ?? new List<NonModelCatalogRow>();
        }

        /// <summary>Сохраняет строки, сливая их с текущим файлом (его могли дополнить другие пользователи).</summary>
        public void Save(IEnumerable<NonModelCatalogRow> rows)
        {
            var merged = new List<NonModelCatalogRow>();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in (File.Exists(_path) ? Load() : new List<NonModelCatalogRow>()).Concat(rows ?? Enumerable.Empty<NonModelCatalogRow>()))
            {
                if (row == null || string.IsNullOrWhiteSpace(row.ADSK_Наименование)) continue;
                string key = row.CatalogKey();
                if (index.TryGetValue(key, out int i)) merged[i] = row;
                else { index[key] = merged.Count; merged.Add(row); }
            }
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonConvert.SerializeObject(merged, Formatting.Indented));
        }

        public void Add(NonModelCatalogRow row)
        {
            if (row == null) throw new ArgumentNullException("row");
            Save(new[] { row });
        }

        private static string ReadSeed()
        {
            try
            {
                using (Stream s = typeof(NonModelCatalogService).Assembly.GetManifestResourceStream("TNovMEPSpec.MEPCatalog.json"))
                {
                    if (s == null) return "[]";
                    using (var reader = new StreamReader(s)) return reader.ReadToEnd();
                }
            }
            catch (Exception ex) { Logger.Log("Каталог немоделируемых: не прочитан встроенный каталог: " + ex.Message, 4); return "[]"; }
        }
    }
}
