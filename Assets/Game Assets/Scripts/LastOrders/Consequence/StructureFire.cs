using System;
using UnityEngine;

namespace LastOrders.Consequence
{
    /// <summary>
    /// Fire on one structure. Lit by Burners, and by Rain of Ash putting every wooden thing
    /// at risk (16.2).
    ///
    /// Fire is the only damage source in the game that keeps working after the thing that
    /// started it is dead, which is what makes the Douse verb worth a Crisis Bar slot and
    /// what makes a wall of scrap wood a decision rather than a default.
    /// </summary>
    [RequireComponent(typeof(DamageableStructure))]
    public sealed class StructureFire : MonoBehaviour
    {
        [Tooltip("Damage per second while burning.")]
        [SerializeField] float damagePerSecond = 8f;

        [Tooltip("Seconds before the fire burns itself out unaided. Long enough that ignoring " +
                 "it is a real loss, short enough that one Burner cannot end the night.")]
        [SerializeField] float burnDurationSeconds = 20f;

        [Tooltip("Seconds between attempts to spread to a neighbouring flammable structure.")]
        [SerializeField] float spreadIntervalSeconds = 6f;

        DamageableStructure structure;
        float remaining;
        float spreadTimer;

        public bool IsBurning => remaining > 0f;

        public event Action OnIgnited;
        public event Action OnExtinguished;

        void Awake() => structure = GetComponent<DamageableStructure>();

        /// <summary>
        /// Lights this structure, if it can burn at all. Fireproof materials simply refuse,
        /// which is the entire payoff for the player having spent Parts on brick.
        /// </summary>
        public void Ignite()
        {
            PlacedObjectTypeSO type = GetComponent<PlacedObject_Done>()?.PlacedObjectTypeSO;
            if (type != null && type.wallMaterial != null)
            {
                if (type.wallMaterial.fireproof || !type.wallMaterial.flammable) return;
            }

            bool wasBurning = IsBurning;

            // Re-igniting refreshes the timer rather than stacking a second fire. Two Burners
            // on one wall should not silently double its damage.
            remaining = burnDurationSeconds;

            if (!wasBurning) OnIgnited?.Invoke();
        }

        /// <summary>The Crisis Bar's Douse verb, and what a water tank buys. Wetting a tile
        /// also prevents immediate re-ignition for the rest of the tick.</summary>
        public void Extinguish()
        {
            if (!IsBurning) return;

            remaining = 0f;
            spreadTimer = 0f;
            OnExtinguished?.Invoke();
        }

        void Update()
        {
            if (!IsBurning) return;

            float delta = Time.deltaTime;
            remaining -= delta;

            if (structure != null) structure.ApplyDamage(damagePerSecond * delta);

            spreadTimer += delta;
            if (spreadTimer >= spreadIntervalSeconds)
            {
                spreadTimer = 0f;
                TrySpread();
            }

            if (remaining <= 0f) Extinguish();
        }

        /// <summary>
        /// Spreads to one orthogonally adjacent flammable structure.
        ///
        /// One neighbour per interval, not all four. Fire that spreads in every direction at
        /// once stops being a crisis the player can triage and becomes an unrecoverable
        /// cascade, which 15 rules out.
        /// </summary>
        void TrySpread()
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return;

            var self = GetComponent<PlacedObject_Done>();
            if (self == null) return;

            var tiles = self.GetGridPositionList();
            if (tiles.Count == 0) return;

            Vector2Int origin = tiles[0];
            Span<Vector2Int> neighbours = stackalloc Vector2Int[4]
            {
                origin + Vector2Int.up,
                origin + Vector2Int.down,
                origin + Vector2Int.left,
                origin + Vector2Int.right,
            };

            for (int i = 0; i < neighbours.Length; i++)
            {
                PlacedObject_Done occupant =
                    grid.GetGridObject(neighbours[i].x, neighbours[i].y)?.GetPlacedObject();

                if (occupant == null || occupant == self) continue;

                var fire = occupant.GetComponent<StructureFire>();
                if (fire == null || fire.IsBurning) continue;

                fire.Ignite();
                return;
            }
        }

        /// <summary>Fires do not survive the night that lit them. Called at Resolution so a
        /// player never wakes to a building still alight from eight hours ago.</summary>
        public void ResetForNewDay() => Extinguish();
    }
}
