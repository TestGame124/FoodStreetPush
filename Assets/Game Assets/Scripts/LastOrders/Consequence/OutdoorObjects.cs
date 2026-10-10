using System.Collections.Generic;
using LastOrders.Defense;
using LastOrders.Housing;
using LastOrders.Livestock;
using LastOrders.Medical;
using LastOrders.Perimeter;
using LastOrders.Restaurant;
using UnityEngine;

namespace LastOrders.Consequence
{
    /// <summary>
    /// Finds what in the scene counts as the yard: the things a zombie that got over the wall can
    /// smash, each given an <see cref="OutdoorWreck"/> if it has none yet.
    ///
    /// The yard is planting beds, livestock pens, the defenses standing on the ground inside the
    /// walls, the shelters and the hospital tent - and only those inside the walls, outside the
    /// restaurant. Left out on purpose: everything in the restaurant (the kitchen is never touched),
    /// the stores (the ingredients in them stay safe), the walls and gates (they have their own
    /// health and repairs) and the guns up on the wall (they go over with their bastion).
    /// </summary>
    public static class OutdoorObjects
    {
        public static List<OutdoorWreck> Tag(CompoundPerimeterService compound, RestaurantService restaurant, float health)
        {
            var found = new List<OutdoorWreck>();

            foreach (PlantPot pot in Object.FindObjectsByType<PlantPot>(FindObjectsSortMode.None))
                Add(pot, "Planting bed", compound, restaurant, health, found);

            foreach (LivestockPen pen in Object.FindObjectsByType<LivestockPen>(FindObjectsSortMode.None))
                Add(pen, pen.DisplayName, compound, restaurant, health, found);

            foreach (DefensePost post in Object.FindObjectsByType<DefensePost>(FindObjectsSortMode.None))
            {
                if (post.Unit == null || !post.Unit.IsBuilt || IsOnTheWall(post)) continue;
                Add(post, post.DisplayName, compound, restaurant, health, found);
            }

            foreach (ShelterHut hut in Object.FindObjectsByType<ShelterHut>(FindObjectsSortMode.None))
            {
                if (!hut.IsBuilt) continue;
                Add(hut, hut.DisplayName, compound, restaurant, health, found);
            }

            foreach (HospitalTent tent in Object.FindObjectsByType<HospitalTent>(FindObjectsSortMode.None))
                Add(tent, tent.DisplayName, compound, restaurant, health, found);

            return found;
        }

        static void Add(Component thing, string displayName, CompoundPerimeterService compound,
                        RestaurantService restaurant, float health, List<OutdoorWreck> into)
        {
            if (thing == null || !thing.gameObject.activeInHierarchy) return;

            Vector3 at = thing.transform.position;

            if (compound != null && compound.HasInterior && !compound.IsInside(at)) return;
            if (restaurant != null && restaurant.HasFootprint && RestaurantService.DistanceTo(restaurant.Footprint, at) <= 0f) return;
            if (thing.GetComponentInParent<CompoundStructure>() != null) return;

            if (!thing.TryGetComponent(out OutdoorWreck wreck)) wreck = thing.gameObject.AddComponent<OutdoorWreck>();

            wreck.Configure(health, displayName);
            into.Add(wreck);
        }

        /// <summary>Up on the compound wall: on a bastion, at a wall site, or mounted there from the shop.</summary>
        static bool IsOnTheWall(DefensePost post)
        {
            WallMount mount = post.GetComponentInParent<WallMount>();
            if (mount != null && mount.IsMounted) return true;

            WallSite site = post.GetComponentInParent<WallSite>();
            return site != null && !site.OnGround;
        }
    }
}
