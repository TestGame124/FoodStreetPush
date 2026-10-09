using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>
    /// Joins a placed object to its neighbours of the same kind: two plant pots side by side become
    /// one long bed, four in a square one big one. Put it on the placed object's root and point it
    /// at the renderer that draws the tile.
    ///
    /// Each tile claims the grid cell under its footprint's centre in a registry shared by all
    /// tiles, and draws the mesh its <see cref="AutoTileSet"/> builds for whichever of its eight
    /// neighbouring cells hold a tile of the same set. When a tile arrives, leaves or moves, the
    /// cells round it are told to look again.
    ///
    /// It watches its own transform rather than the placement grid, so it works the same for an
    /// object the player places, one moved or loaded from a save, and one authored into the scene
    /// that was never registered on the grid. The grid is only asked how big a cell is.
    ///
    /// The tile is drawn the same way round whatever way the object faces, so rows and borders
    /// line up across a joined bed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridAutoTile : MonoBehaviour
    {
        [Tooltip("The pieces this object is drawn from. Objects with the same set join up.")]
        [SerializeField] AutoTileSet set;

        [Tooltip("Draws the tile. Its transform is kept on the footprint's centre, unrotated.")]
        [SerializeField] MeshFilter target;

        [Tooltip("The footprint's centre, in this object's own space (0.5, 0, 0.5 for a 1 x 1 corner-pivoted prefab).")]
        [SerializeField] Vector3 footprintCentre = new Vector3(0.5f, 0f, 0.5f);

        [Tooltip("Squash and settle when a neighbour arrives or leaves, so the change reads as the pieces joining.")]
        [SerializeField] bool settleOnChange = true;

        readonly struct Key : System.IEquatable<Key>
        {
            readonly AutoTileSet set;
            readonly Vector2Int cell;

            public Key(AutoTileSet set, Vector2Int cell)
            {
                this.set = set;
                this.cell = cell;
            }

            public bool Equals(Key other) => set == other.set && cell == other.cell;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => ((set != null ? set.GetInstanceID() : 0) * 397) ^ cell.GetHashCode();
        }

        static readonly Dictionary<Key, GridAutoTile> Occupied = new Dictionary<Key, GridAutoTile>();

        Vector2Int cell;
        bool registered;
        bool dirty;
        bool present;
        int shownMask = -1;
        Renderer drawn;

        public AutoTileSet Set => set;

        /// <summary>The cell this tile has claimed, when it has one.</summary>
        public bool TryGetCell(out Vector2Int claimed)
        {
            claimed = cell;
            return registered;
        }

        /// <summary>Which neighbours this tile is joined to, as <see cref="TileMask"/> bits; -1 before it is first drawn.</summary>
        public int Mask => shownMask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Occupied.Clear();

        /// <summary>Wires the tile up, for builders that add it from code.</summary>
        public void Configure(AutoTileSet tiles, MeshFilter drawn, Vector3 centre)
        {
            set = tiles;
            target = drawn;
            footprintCentre = centre;
            this.drawn = null;
        }

        void OnEnable()
        {
            // Placed from a pool, an object is switched on before it is moved into place, so where
            // it stands is read on the first LateUpdate, not here.
            transform.hasChanged = true;
            dirty = true;
            present = true;
        }

        void OnDisable()
        {
            Release();
            shownMask = -1;
            if (target != null) target.transform.DOKill();
        }

        void LateUpdate()
        {
            // A tile that is not being drawn - picked up to be moved, say, while a ghost of it follows
            // the finger - is not there for its neighbours, who close their borders until it is back.
            if (drawn == null && target != null) drawn = target.GetComponent<Renderer>();
            bool shown = drawn == null || drawn.enabled;
            if (shown != present)
            {
                present = shown;
                if (present) transform.hasChanged = true;
                else Release();
            }

            if (present && transform.hasChanged)
            {
                transform.hasChanged = false;
                Claim();
            }

            if (dirty) Redraw();
        }

        /// <summary>Takes the cell under the footprint's centre, letting go of the old one if it has moved.</summary>
        void Claim()
        {
            if (set == null) return;

            Vector3 centre = transform.TransformPoint(footprintCentre);
            Vector2Int now = CellAt(centre);

            if (target != null)
            {
                // Square to the world, so every tile in a bed has its rows the same way.
                target.transform.SetPositionAndRotation(centre, Quaternion.identity);
            }

            if (registered && now == cell) return;

            Release();

            var key = new Key(set, now);
            if (Occupied.TryGetValue(key, out GridAutoTile holder) && holder != null && holder != this)
            {
                // Two objects in one cell: the first keeps it, this one stands alone.
                dirty = true;
                return;
            }

            Occupied[key] = this;
            cell = now;
            registered = true;
            dirty = true;
            TouchNeighbours();
        }

        void Release()
        {
            if (!registered) return;

            var key = new Key(set, cell);
            if (Occupied.TryGetValue(key, out GridAutoTile holder) && holder == this) Occupied.Remove(key);

            registered = false;
            TouchNeighbours();
        }

        void TouchNeighbours()
        {
            for (int i = 0; i < TileMask.Offsets.Length; i++)
            {
                if (Occupied.TryGetValue(new Key(set, cell + TileMask.Offsets[i]), out GridAutoTile neighbour) && neighbour != null)
                    neighbour.dirty = true;
            }
        }

        void Redraw()
        {
            dirty = false;
            if (set == null || target == null) return;

            int mask = registered ? NeighbourMask() : 0;
            if (mask == shownMask) return;

            bool changed = shownMask >= 0;
            shownMask = mask;
            target.sharedMesh = set.MeshFor(mask);

            if (changed && settleOnChange) Settle();
        }

        int NeighbourMask()
        {
            int mask = 0;
            for (int i = 0; i < TileMask.Offsets.Length; i++)
            {
                if (Occupied.TryGetValue(new Key(set, cell + TileMask.Offsets[i]), out GridAutoTile neighbour) && neighbour != null)
                    mask |= 1 << i;
            }

            return TileMask.Canonical(mask);
        }

        /// <summary>
        /// A quick squash down and back up. Only the height moves: cells joined side by side share
        /// their edges, and scaling across would open the seams for as long as it lasted.
        /// </summary>
        void Settle()
        {
            Transform t = target.transform;
            t.DOKill();
            t.localScale = new Vector3(1f, 0.82f, 1f);
            t.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        static Vector2Int CellAt(Vector3 world)
        {
            Vector3 origin = Vector3.zero;
            float size = 1f;

            GridBuildingSystem3D grid = GridBuildingSystem3D.Instance;
            if (grid != null && grid.Placement != null)
            {
                origin = grid.Placement.Layout.Origin;
                size = grid.Placement.Layout.CellSize;
            }
            else if (grid != null && grid.Settings != null)
            {
                origin = grid.Settings.origin;
                size = grid.Settings.cellSize;
            }

            return new Vector2Int(Mathf.FloorToInt((world.x - origin.x) / size), Mathf.FloorToInt((world.z - origin.z) / size));
        }
    }
}
