using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace FoodStreet.Placement
{
    /// <summary>Runtime pools live outside definition assets and reset on each Play session.</summary>
    public static class PlacementPools
    {
        sealed class Pools
        {
            public ObjectPool<Transform> objects, visuals;
        }
        static readonly Dictionary<PlacedObjectTypeSO, Pools> pools = new Dictionary<PlacedObjectTypeSO, Pools>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => pools.Clear();
        static ObjectPool<Transform> Create(Transform prefab, int capacity) => new ObjectPool<Transform>(
            () => Object.Instantiate(prefab),
            item => { if (item != null) item.gameObject.SetActive(true); },
            item => { if (item != null) item.gameObject.SetActive(false); },
            item => { if (item != null) Object.Destroy(item.gameObject); }, false, capacity, Mathf.Max(100, capacity));
        public static void Initialize(PlacedObjectTypeSO type)
        {
            if (pools.ContainsKey(type)) return;
            int capacity = Mathf.Max(1, type.maxItems > 0 ? type.maxItems : type.categoryType == ObjectCategory.Floor ? 100 : 20);
            pools.Add(type, new Pools { objects = Create(type.prefab, capacity), visuals = Create(type.visual, capacity) });
        }
        public static Transform Get(PlacedObjectTypeSO type, bool visual)
        {
            Initialize(type);
            var pool = visual ? pools[type].visuals : pools[type].objects;
            Transform item;
            // Scene unloading destroys pooled GameObjects; skip stale Unity references.
            do { item = pool.Get(); } while (item == null);
            return item;
        }
        public static void Release(PlacedObjectTypeSO type, Transform instance, bool visual)
        {
            if (instance == null) return;
            Initialize(type);
            (visual ? pools[type].visuals : pools[type].objects).Release(instance);
        }
    }
}
