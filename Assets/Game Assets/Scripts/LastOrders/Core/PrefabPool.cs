using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace LastOrders.Core
{
    /// <summary>
    /// A pool of instances of one specific prefab.
    ///
    /// Keyed by prefab, not by component type. The distinction matters here more than it
    /// usually does: all eight enemy archetypes in §14 share a single component, so a
    /// type-keyed pool would hand a Brute's transform back to something asking for a
    /// Shambler. Every pool in this game is therefore identified by the asset it clones.
    ///
    /// Wraps Unity's <see cref="ObjectPool{T}"/> rather than reimplementing it — the
    /// value added here is the reset contract and the parenting, not the free list.
    /// </summary>
    public sealed class PrefabPool
    {
        readonly Component prefab;
        readonly Transform parent;
        readonly ObjectPool<Component> pool;
        readonly HashSet<Component> live = new HashSet<Component>();

        /// <summary>Instances currently checked out. Watched during night waves — if this
        /// climbs monotonically across several nights, something is failing to release.</summary>
        public int ActiveCount => live.Count;

        public int InactiveCount => pool.CountInactive;

        public Component Prefab => prefab;

        public PrefabPool(Component prefab, Transform parent, int prewarm, int maxSize)
        {
            this.prefab = prefab;
            this.parent = parent;

            pool = new ObjectPool<Component>(
                createFunc: Create,
                actionOnGet: null,      // handled in Take, after the live-set bookkeeping
                actionOnRelease: null,  // handled in Return, before deactivation
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: true,  // catches a double release at the point of the bug
                defaultCapacity: Mathf.Max(1, prewarm),
                maxSize: Mathf.Max(1, maxSize));

            Prewarm(prewarm);
        }

        Component Create()
        {
            Component instance = Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        static void OnDestroyInstance(Component instance)
        {
            if (instance != null) Object.Destroy(instance.gameObject);
        }

        void Prewarm(int count)
        {
            if (count <= 0) return;

            // Cycling through Get/Release rather than calling Create directly so the
            // prewarmed instances take exactly the same path a runtime one will.
            var buffer = new Component[count];
            for (int i = 0; i < count; i++) buffer[i] = pool.Get();
            for (int i = 0; i < count; i++) pool.Release(buffer[i]);
        }

        public Component Take()
        {
            Component instance = pool.Get();
            live.Add(instance);

            instance.gameObject.SetActive(true);

            if (instance is IPoolable poolable)
                poolable.OnTakenFromPool();

            return instance;
        }

        /// <summary>
        /// Returns an instance. Safe to call with something this pool never handed out —
        /// that is a bug, but destroying a stray is better than corrupting the free list,
        /// so it is logged and destroyed rather than pooled.
        /// </summary>
        public void Return(Component instance)
        {
            if (instance == null) return;

            if (!live.Remove(instance))
            {
                Debug.LogError(
                    $"[PrefabPool] '{instance.name}' was returned to the pool for " +
                    $"'{prefab.name}' but was not taken from it. Destroying it instead.",
                    instance);
                Object.Destroy(instance.gameObject);
                return;
            }

            // Reset runs while the object is still active, so implementations may stop
            // coroutines and touch components that ignore calls on inactive objects.
            if (instance is IPoolable poolable)
                poolable.OnReturnedToPool();

            PoolIntegrity.Verify(instance, prefab);

            instance.gameObject.SetActive(false);

            // Re-parenting on release, not on take. An instance left under whatever
            // transform the gameplay code parented it to will be destroyed with that
            // transform, silently shrinking the pool.
            if (instance.transform.parent != parent)
                instance.transform.SetParent(parent, false);

            pool.Release(instance);
        }

        /// <summary>Returns everything currently checked out. Called at dawn so a wave that
        /// ended untidily cannot leak agents into the next night.</summary>
        public void ReturnAll()
        {
            if (live.Count == 0) return;

            var buffer = new List<Component>(live);
            for (int i = 0; i < buffer.Count; i++) Return(buffer[i]);
        }

        public void Clear()
        {
            ReturnAll();
            pool.Clear();
        }
    }
}
