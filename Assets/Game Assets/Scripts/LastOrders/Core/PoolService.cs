using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Core
{
    /// <summary>
    /// Owns every <see cref="PrefabPool"/> in the game and keeps their instances parked
    /// under one tidy root.
    ///
    /// Pools are created on demand rather than declared up front. The alternative — a
    /// serialised list of every poolable prefab — has to be maintained by hand every time
    /// a buildable or an enemy is added, and the whole point of the data-driven content
    /// pipeline is that adding an enemy is editing a ScriptableObject and nothing else.
    /// </summary>
    public sealed class PoolService
    {
        readonly Dictionary<Component, PrefabPool> pools = new Dictionary<Component, PrefabPool>();
        readonly Transform root;

        /// <summary>Maps a checked-out instance back to the pool that owns it, so callers
        /// can release an object without remembering where it came from.</summary>
        readonly Dictionary<Component, PrefabPool> ownership = new Dictionary<Component, PrefabPool>();

        public PoolService(Transform root)
        {
            this.root = root;
        }

        public PrefabPool GetPool(Component prefab, int prewarm = 8, int maxSize = 256)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolService] Asked for a pool of a null prefab.");
                return null;
            }

            if (pools.TryGetValue(prefab, out PrefabPool pool))
                return pool;

            var host = new GameObject($"Pool [{prefab.name}]").transform;
            host.SetParent(root, false);

            pool = new PrefabPool(prefab, host, prewarm, maxSize);
            pools.Add(prefab, pool);
            return pool;
        }

        /// <summary>Takes an instance and remembers which pool it belongs to.</summary>
        public T Take<T>(T prefab, int prewarm = 8, int maxSize = 256) where T : Component
        {
            PrefabPool pool = GetPool(prefab, prewarm, maxSize);
            if (pool == null) return null;

            Component instance = pool.Take();
            ownership[instance] = pool;
            return (T)instance;
        }

        public T Take<T>(T prefab, Vector3 position, Quaternion rotation,
                         int prewarm = 8, int maxSize = 256) where T : Component
        {
            T instance = Take(prefab, prewarm, maxSize);
            if (instance == null) return null;

            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>
        /// Releases an instance back to whichever pool produced it. Destroying an unpooled
        /// object rather than throwing, because the alternative — a caller having to know
        /// whether a given reference is pooled — puts that branch at every call site.
        /// </summary>
        public void Return(Component instance)
        {
            if (instance == null) return;

            if (ownership.TryGetValue(instance, out PrefabPool pool))
            {
                ownership.Remove(instance);
                pool.Return(instance);
                return;
            }

            Object.Destroy(instance.gameObject);
        }

        /// <summary>
        /// Recalls every outstanding instance. Run at dawn: the night's agents are gone by
        /// then anyway, and anything still checked out is a leak that would otherwise
        /// accumulate across a fifty-day campaign.
        /// </summary>
        public void ReturnAll()
        {
            foreach (PrefabPool pool in pools.Values) pool.ReturnAll();
            ownership.Clear();
        }

        /// <summary>Total instances currently checked out across all pools, for the overlay.</summary>
        public int TotalActive
        {
            get
            {
                int total = 0;
                foreach (PrefabPool pool in pools.Values) total += pool.ActiveCount;
                return total;
            }
        }

        public IEnumerable<KeyValuePair<Component, PrefabPool>> Pools => pools;

        public void Clear()
        {
            foreach (PrefabPool pool in pools.Values) pool.Clear();
            pools.Clear();
            ownership.Clear();
        }
    }
}
