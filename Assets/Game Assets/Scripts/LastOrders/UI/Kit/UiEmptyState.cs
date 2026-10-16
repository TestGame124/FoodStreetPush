using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Says why a list is empty instead of showing an empty box: watches a container (or whatever a
    /// scroll view is showing) and switches a message on while nothing in it is showing.
    /// </summary>
    public sealed class UiEmptyState : MonoBehaviour
    {
        [Tooltip("The list to watch. Ignored when a scroll view is set - its current content is watched instead.")]
        [SerializeField] Transform container;

        [SerializeField] ScrollRect scroll;

        [SerializeField] GameObject message;

        public void Configure(Transform list, ScrollRect view, GameObject empty)
        {
            container = list;
            scroll = view;
            message = empty;
        }

        void LateUpdate()
        {
            Transform list = scroll != null && scroll.content != null ? scroll.content : container;
            if (list == null || message == null) return;

            bool any = false;
            foreach (Transform child in list)
            {
                if (!child.gameObject.activeSelf) continue;
                any = true;
                break;
            }

            if (message.activeSelf == any) message.SetActive(!any);
        }
    }
}
