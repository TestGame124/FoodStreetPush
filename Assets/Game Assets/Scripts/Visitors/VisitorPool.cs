using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// Keeps instances of each prefab rather than each type.
    ///
    /// The project's own ObjectPoolingManager keys on the component TYPE, which is exactly one
    /// pool per class - fine when there is one enemy prefab, useless here, where five visitor
    /// definitions and four vehicle definitions all carry the same two components. Asking it
    /// for a Visitor would hand back whichever prefab happened to be registered first, and
    /// every customer in the game would be the same person in the same car.
    ///
    /// Small on purpose: take, return, and a parent to keep the hierarchy tidy. Anything more
    /// belongs to whichever system owns the objects.
    /// </summary>
    public sealed class VisitorPool
    {
        readonly Dictionary<GameObject, Stack<GameObject>> idle =
            new Dictionary<GameObject, Stack<GameObject>>();

        /// <summary>Which prefab each live instance came from, so it can be put back in the
        /// right stack. Kept here rather than on a component, so nothing on the prefab has to
        /// know it is pooled.</summary>
        readonly Dictionary<GameObject, GameObject> origin = new Dictionary<GameObject, GameObject>();

        readonly Transform parent;

        public VisitorPool(Transform poolRoot) => parent = poolRoot;

        public int LiveCount => origin.Count;

        /// <summary>
        /// An instance of a prefab, active and parented to the pool root.
        ///
        /// Returns null rather than throwing on a null prefab: a definition whose prefab was
        /// never generated should stop one arrival and log, not take the director down.
        /// </summary>
        public T Take<T>(GameObject prefab, Vector3 at) where T : Component
        {
            if (prefab == null) return null;

            GameObject instance = null;

            if (idle.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                // Destroyed instances are skipped rather than trusted. A scene reload leaves
                // nulls in the stack, and handing one out is a NullReference two systems away
                // from the cause.
                while (stack.Count > 0 && instance == null) instance = stack.Pop();
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab, at, Quaternion.identity, parent);
            }
            else
            {
                instance.transform.SetPositionAndRotation(at, Quaternion.identity);
                instance.SetActive(true);
            }

            var component = instance.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"[Visitors] '{prefab.name}' has no {typeof(T).Name}.", prefab);
                Object.Destroy(instance);
                return null;
            }

            origin[instance] = prefab;
            return component;
        }

        public void Return(Component component)
        {
            if (component == null) return;

            GameObject instance = component.gameObject;

            if (!origin.TryGetValue(instance, out GameObject prefab))
            {
                // Not ours. Destroying it would be worse than leaving it: something else in the
                // scene put it there.
                instance.SetActive(false);
                return;
            }

            origin.Remove(instance);

            instance.SetActive(false);
            instance.transform.SetParent(parent, worldPositionStays: false);

            if (!idle.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                idle[prefab] = stack;
            }

            stack.Push(instance);
        }
    }
}
