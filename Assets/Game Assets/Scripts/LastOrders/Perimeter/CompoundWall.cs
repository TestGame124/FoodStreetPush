using FoodStreet.Navigation;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// One section of the compound's outer wall.
    ///
    /// It stops zombies and nothing else. Staff and visitors walk authored routes that already
    /// go round the wall and in through the gates, so the wall has no opinion about them - and a
    /// route that happened to clip a corner is not a worker frozen against it all night.
    ///
    /// A zombie that walks into a section stands and hits it. Three looks say how that is going:
    /// the wall as built, the wall cracked and broken along its top once it is below half, and a
    /// heap of rubble once it has fallen - which is a gap the rest of the night walks through.
    /// Its health is its wall material's, the same table the build layer's walls use.
    /// </summary>
    public sealed class CompoundWall : CompoundStructure
    {
        [Tooltip("What it is built from. Its health comes from here.")]
        [SerializeField] WallMaterialDefinition material;

        [Tooltip("Health when no material is set.")]
        [SerializeField] float fallbackHealth = 350f;

        [Tooltip("Below this share of its health it shows as broken.")]
        [Range(0.05f, 0.95f)] [SerializeField] float damagedBelow = 0.5f;

        [Header("Looks")]
        [SerializeField] GameObject intactVisuals;
        [SerializeField] GameObject damagedVisuals;
        [SerializeField] GameObject rubbleVisuals;

        public override float MaxHealth =>
            material != null && material.health > 0f ? material.health : Mathf.Max(1f, fallbackHealth);

        public WallMaterialDefinition Material => material;

        /// <summary>Zombies only. The wall is not something anyone else walks into.</summary>
        public override bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover)
        {
            if (mover != MoverKind.Hostile || !IsInTheWay) return false;

            return MovementBarriers.StepEnters(from, to, transform.position, Yaw, HalfSize, radius);
        }

        protected override void ApplyVisuals()
        {
            bool down = IsDestroyed;
            bool broken = !down && HealthFraction < damagedBelow;

            SetActive(intactVisuals, !down && !broken);
            SetActive(damagedVisuals, broken);
            SetActive(rubbleVisuals, down);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>Editor wiring for the looks, for the setup tool.</summary>
        public void ConfigureWall(WallMaterialDefinition wallMaterial, GameObject intact, GameObject damaged,
                                  GameObject rubble)
        {
            material = wallMaterial;
            intactVisuals = intact;
            damagedVisuals = damaged;
            rubbleVisuals = rubble;
        }
    }
}
