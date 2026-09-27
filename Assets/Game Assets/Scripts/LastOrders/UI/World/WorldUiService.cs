using System;
using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.UI.World
{
    /// <summary>
    /// Hands out pooled world-space UI and takes it back.
    ///
    /// Owns its OWN pool rather than sharing the game's. Dawn recalls every instance of the
    /// shared pool - that is how a night's agents are swept up - and dawn also runs when the
    /// game starts. A timer over debris is not a night agent, and one recalled from under the
    /// work it is timing would leave its owner holding a parked instance.
    ///
    /// Keeps its own record of what is out, so releasing twice is harmless. The shared pool
    /// destroys anything it did not hand out, and an element released once by its owner and
    /// once by itself would otherwise destroy an instance that is sitting in the free list.
    /// </summary>
    public sealed class WorldUiService
    {
        readonly PoolService pools;
        readonly WorldUiCatalog catalog;
        readonly HashSet<WorldUiElement> live = new HashSet<WorldUiElement>();

        Camera view;
        ActionPromptView prompt;
        bool warnedMissingCatalog;

        public WorldUiService(WorldUiCatalog catalog, Transform root)
        {
            this.catalog = catalog;
            pools = new PoolService(root);
        }

        public WorldUiCatalog Catalog => catalog;

        /// <summary>Elements currently shown. For the debug overlay and the tests.</summary>
        public int LiveCount => live.Count;

        /// <summary>The prompt currently open, if any.</summary>
        public ActionPromptView CurrentPrompt => prompt;

        /// <summary>The camera elements face. Found lazily; settable for scenes with several.</summary>
        public Camera Camera
        {
            get
            {
                if (view == null) view = Camera.main;
                return view;
            }
            set => view = value;
        }

        /// <summary>Shows an element that follows <paramref name="anchor"/>.</summary>
        public T Spawn<T>(T prefab, Transform anchor, Vector3 offset) where T : WorldUiElement
        {
            return Spawn(prefab, anchor, anchor != null ? anchor.position : Vector3.zero, offset);
        }

        /// <summary>Shows an element at a fixed point.</summary>
        public T SpawnAt<T>(T prefab, Vector3 position) where T : WorldUiElement
        {
            return Spawn(prefab, null, position, Vector3.zero);
        }

        public void Release(WorldUiElement element)
        {
            if (element == null || !live.Remove(element)) return;

            if (ReferenceEquals(element, prompt)) prompt = null;
            pools.Return(element);
        }

        /// <summary>
        /// Opens the action prompt over something. Only one is ever open: opening another closes
        /// the first, which is what tapping a second pile of debris should do.
        /// </summary>
        public ActionPromptView ShowPrompt(Transform anchor, Sprite icon, Action onConfirm)
        {
            HidePrompt();

            if (!HasPrefab(catalog != null ? catalog.actionPrompt : null, "actionPrompt")) return null;

            prompt = Spawn(catalog.actionPrompt, anchor, catalog.promptOffset);
            if (prompt != null) prompt.Configure(icon, onConfirm);

            return prompt;
        }

        public void HidePrompt()
        {
            if (prompt != null) Release(prompt);
        }

        /// <summary>True when the open prompt belongs to this anchor.</summary>
        public bool IsPromptOpenFor(Transform anchor) =>
            prompt != null && anchor != null && prompt.Anchor == anchor;

        public WorkProgressView ShowProgress(Transform anchor)
        {
            if (!HasPrefab(catalog != null ? catalog.workProgress : null, "workProgress")) return null;
            return Spawn(catalog.workProgress, anchor, catalog.progressOffset);
        }

        /// <summary>
        /// Shows a standing button - not the single dismissable prompt. It stays until its owner
        /// releases it, however many other buttons and prompts come and go.
        /// </summary>
        public ActionPromptView ShowButton(ActionPromptView prefab, Transform anchor, Vector3 offset,
                                           Sprite icon, Action onPressed)
        {
            if (prefab == null) return null;

            ActionPromptView button = Spawn(prefab, anchor, offset);
            if (button != null) button.Configure(icon, onPressed);

            return button;
        }

        /// <summary>The expand icon over land that is cleared and ready.</summary>
        public ActionPromptView ShowExpandPrompt(Transform anchor, Action onPressed)
        {
            if (!HasPrefab(catalog != null ? catalog.expandPrompt : null, "expandPrompt")) return null;
            return ShowButton(catalog.expandPrompt, anchor, catalog.expandPromptOffset, null, onPressed);
        }

        /// <summary>The wrench over something broken.</summary>
        public ActionPromptView ShowRepairPrompt(Transform anchor, Action onPressed)
        {
            if (!HasPrefab(catalog != null ? catalog.repairPrompt : null, "repairPrompt")) return null;
            return ShowButton(catalog.repairPrompt, anchor, catalog.repairPromptOffset, null, onPressed);
        }

        /// <summary>The timer over land while its boundary is built.</summary>
        public WorkProgressView ShowExpansionProgress(Transform anchor)
        {
            if (!HasPrefab(catalog != null ? catalog.expansionProgress : null, "expansionProgress")) return null;
            return Spawn(catalog.expansionProgress, anchor, catalog.expansionProgressOffset);
        }

        /// <summary>The health bar over a worker in a fight.</summary>
        public HealthBarView ShowHealthBar(Transform anchor)
        {
            if (!HasPrefab(catalog != null ? catalog.healthBar : null, "healthBar")) return null;
            return Spawn(catalog.healthBar, anchor, catalog.healthBarOffset);
        }

        /// <summary>A hit taken or a warning, in the catalog's danger colour.</summary>
        public FloatingTextView ShowWarning(Vector3 position, string text) =>
            ShowFloatingText(position, text, catalog != null ? catalog.dangerTextColour : Color.red);

        public FloatingTextView ShowFloatingText(Vector3 position, string text, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (!HasPrefab(catalog != null ? catalog.floatingText : null, "floatingText")) return null;

            FloatingTextView view = SpawnAt(catalog.floatingText, position + catalog.floatingTextOffset);
            if (view != null) view.Play(text, colour);

            return view;
        }

        /// <summary>"+2 XP" in the catalog's experience colour.</summary>
        public FloatingTextView ShowReward(Vector3 position, string text) =>
            ShowFloatingText(position, text, catalog != null ? catalog.experienceTextColour : Color.yellow);

        T Spawn<T>(T prefab, Transform anchor, Vector3 position, Vector3 offset) where T : WorldUiElement
        {
            if (prefab == null) return null;

            // A handful of each is plenty: there is one prompt, and a timer per job in hand.
            T element = pools.Take(prefab, prewarm: 2, maxSize: 64);
            if (element == null) return null;

            live.Add(element);
            element.Bind(this, anchor, position, offset, Camera);
            return element;
        }

        bool HasPrefab(UnityEngine.Object prefab, string field)
        {
            if (prefab != null) return true;

            if (!warnedMissingCatalog)
            {
                Debug.LogError($"[WorldUI] No '{field}' prefab in the World UI catalog, so it cannot be " +
                               "shown. Run 'Last Orders / Generate Starting Content' or assign " +
                               "GameBalance > World Ui.");
                warnedMissingCatalog = true;
            }

            return false;
        }
    }
}
