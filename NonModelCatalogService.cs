using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TNovMEPSpec
{
    public sealed class NonModelCatalogService
    {
        private readonly string _path;
        public NonModelCatalogService(string path) { _path = path; }
        public IList<NonModelCatalogRow> Load()
        {
            if (!File.Exists(_path)) return new List<NonModelCatalogRow>();
            var json = File.ReadAllText(_path);
            return JsonConvert.DeserializeObject<List<NonModelCatalogRow>>(json) ?? new List<NonModelCatalogRow>();
        }
        public void Save(IEnumerable<NonModelCatalogRow> rows)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonConvert.SerializeObject(rows ?? Enumerable.Empty<NonModelCatalogRow>(), Formatting.Indented));
        }
        public void Add(NonModelCatalogRow row)
        {
            if (row == null) throw new ArgumentNullException("row");
            var rows = Load();
            if (!rows.Any(x => string.Equals(x.CatalogKey(), row.CatalogKey(), StringComparison.OrdinalIgnoreCase)))
            { rows.Add(row); Save(rows); }
        }
    }
}