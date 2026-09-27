using LastOrders.Core;
using LastOrders.Interaction;
using LastOrders.UI.Popups;
using LastOrders.UI.World;

namespace LastOrders.Field
{
    /// <summary>
    /// Opens the two field panels: a worker's info panel, and the panel for sending somebody to
    /// clear an area. A worker, a pile of debris and a zombie on the land all open one of these,
    /// and all of them do it the same way.
    /// </summary>
    public static class FieldPanels
    {
        /// <summary>A worker's info panel. False when there is no panel prefab to open.</summary>
        public static bool OpenWorker(GameComposition composition, IFieldWorker worker)
        {
            WorkerPanelView popup = Show(composition);
            if (popup == null || worker == null) return false;

            FieldWorkService field = composition.Field;
            string id = worker.WorkerId;

            popup.Bind(worker,
                       () => field.GetWorkerPanel(id),
                       option => Close(composition, worker, field.TakeWorkerOption(id, option, out string message), message),
                       () => Close(composition, worker, field.Recall(id, out string message), message));

            return true;
        }

        /// <summary>The panel for sending somebody to clear an area. False when there is no
        /// panel prefab to open.</summary>
        public static bool OpenArea(GameComposition composition, string areaId)
        {
            WorkerPanelView popup = Show(composition);
            if (popup == null || string.IsNullOrEmpty(areaId)) return false;

            FieldWorkService field = composition.Field;
            string owner = $"area:{areaId}";

            popup.Bind(owner,
                       () => field.GetAreaPanel(areaId),
                       option => Close(composition, owner, field.TakeAreaOption(areaId, option, out string message), message),
                       () => RecallFromArea(composition, owner, areaId));

            return true;
        }

        static void RecallFromArea(GameComposition composition, object owner, string areaId)
        {
            IFieldWorker worker = composition.Field.WorkerOn(areaId);
            string message = "Nobody is clearing it.";
            bool done = worker != null && composition.Field.Recall(worker.WorkerId, out message);

            Close(composition, owner, done, message);
        }

        static WorkerPanelView Show(GameComposition composition)
        {
            if (composition == null) return null;

            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            WorkerPanelView prefab = catalog != null ? catalog.workerPanel : null;
            return prefab != null ? composition.Popups.Show(prefab) : null;
        }

        /// <summary>Says what happened; a success closes the panel, a refusal leaves it open
        /// with the reason on it.</summary>
        static void Close(GameComposition composition, object owner, bool done, string message)
        {
            TapFeedback.Report(message);

            if (done && composition.Popups.Current is WorkerPanelView open && Equals(open.Owner, owner))
                composition.Popups.Hide();
        }
    }
}
