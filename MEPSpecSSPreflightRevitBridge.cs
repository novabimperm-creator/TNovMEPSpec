using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TNovCommon;

namespace TNovMEPSpec
{
    public sealed class MEPSpecSSPreflightActionHandler : IExternalEventHandler
    {
        readonly ConcurrentQueue<Action<UIApplication>> _queue = new ConcurrentQueue<Action<UIApplication>>();
        public ExternalEvent Event { get; set; }

        public void Enqueue(Action<UIApplication> action)
        {
            _queue.Enqueue(action);
            Event?.Raise();
        }

        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var action))
            {
                try { action(app); }
                catch (Exception ex)
                {
                    Logger.Log("SS: ошибка Revit-операции: " + ex.Message, 4);
                }
            }
        }

        public string GetName() => "TNov Сводная спека — SS";
    }

    public static class MEPSpecSSPreflightRevitBridge
    {
        static MEPSpecSSPreflightActionHandler _handler;

        public static void Initialize()
        {
            if (_handler != null) return;
            _handler = new MEPSpecSSPreflightActionHandler();
            _handler.Event = ExternalEvent.Create(_handler);
        }

        public static void Enqueue(Action<UIApplication> action)
        {
            if (_handler == null)
                throw new InvalidOperationException("MEPSpecSSPreflightRevitBridge не инициализирован");
            _handler.Enqueue(action);
        }

        public static void SelectElements(ICollection<ElementId> elementIds)
        {
            Enqueue(app =>
            {
                UIDocument uidoc = app.ActiveUIDocument;
                if (uidoc == null || elementIds == null || elementIds.Count == 0) return;

                Document doc = uidoc.Document;
                List<ElementId> ids = elementIds
                    .Where(id => id != null && doc.GetElement(id) != null)
                    .ToList();
                if (ids.Count == 0) return;

                uidoc.Selection.SetElementIds(ids);
                try
                {
                    if (ids.Count == 1)
                        uidoc.ShowElements(ids[0]);
                    else
                        uidoc.ShowElements(ids);
                }
                catch (Exception ex)
                {
                    Logger.Log("Preflight: ShowElements: " + ex.Message, 4);
                }
            });
        }
    }
}
