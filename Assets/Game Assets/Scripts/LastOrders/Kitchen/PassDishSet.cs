using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// What each dish looks like on the serving pass, in a waiter's hands and on a customer's table:
    /// one mesh per dish, painted in its vertices, all drawn with one material - so every plate in the
    /// diner batches together however many dishes are out.
    ///
    /// A dish with no mesh of its own (a recipe added after the set was built) comes out under a
    /// cloche: still a plate of food, never an empty space where the order should be.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Kitchen/Pass Dish Set", fileName = "PassDishes")]
    public sealed class PassDishSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemType item;
            public Mesh mesh;
        }

        [SerializeField] Entry[] dishes = Array.Empty<Entry>();

        [Tooltip("A plate under a cloche, for any dish without a mesh here.")]
        [SerializeField] Mesh covered;

        [Tooltip("Vertex-painted, shared by every dish: LastOrders/Stylized, as the buildings.")]
        [SerializeField] Material material;

        Dictionary<ItemType, Mesh> lookup;

        public Material Material => material;

        public Mesh MeshFor(ItemType type)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<ItemType, Mesh>(dishes.Length);
                foreach (Entry entry in dishes)
                    if (entry.mesh != null) lookup[entry.item] = entry.mesh;
            }

            return lookup.TryGetValue(type, out Mesh mesh) ? mesh : covered;
        }

        /// <summary>Set by the builder (<c>ServingPassModel</c>).</summary>
        public void Configure(Entry[] entries, Mesh fallback, Material shared)
        {
            dishes = entries ?? Array.Empty<Entry>();
            covered = fallback;
            material = shared;
            lookup = null;
        }

        void OnValidate() => lookup = null;
    }
}
