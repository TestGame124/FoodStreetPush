using UnityEngine;

namespace LastOrders.Ambience
{
    /// <summary>
    /// Which patches of the level's ground are open - clear grass, a path, the road - and which have
    /// something standing on them, so a flock lands in the open and not inside a station or on top of a
    /// shed.
    ///
    /// Almost nothing in the level has a collider, so this is read off what is drawn: every mesh
    /// standing more than a hand's height off the ground marks the cells under its bounds as taken.
    /// Flat things - the road, the paths, ground stains - leave the ground open; huge things - the
    /// ground itself, the fog rings - are skipped. The diner is taken whole, floor and all: it has no
    /// roof, but birds do not go indoors. Rebuilt every so often, so a building the player puts down
    /// takes its ground.
    /// </summary>
    public sealed class GroundMap
    {
        const float StandingHeight = 0.15f;
        const float LargestPiece = 30f;
        const float Margin = 0.35f;

        readonly Rect area;
        readonly float cell;
        readonly int width, depth;
        readonly bool[] taken;

        public GroundMap(Rect area, float cell)
        {
            this.area = area;
            this.cell = Mathf.Max(0.25f, cell);
            width = Mathf.Max(1, Mathf.CeilToInt(area.width / this.cell));
            depth = Mathf.Max(1, Mathf.CeilToInt(area.height / this.cell));
            taken = new bool[width * depth];
        }

        public Rect Area => area;

        /// <summary>
        /// Marks the ground again from what stands on it now. <paramref name="ignore"/> - the wildlife's
        /// own root - is left out; <paramref name="indoors"/> are roots taken whole, floor and all.
        /// </summary>
        public void Rebuild(Transform ignore, string[] indoors)
        {
            System.Array.Clear(taken, 0, taken.Length);

            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled) continue;

                Transform root = renderer.transform.root;
                if (ignore != null && root == ignore) continue;

                Bounds bounds = renderer.bounds;
                if (IsIndoors(root, indoors))
                {
                    // Floor included: the whole building is no place for a bird.
                    Take(bounds);
                    continue;
                }

                if (bounds.max.y < StandingHeight) continue;
                if (bounds.size.x > LargestPiece || bounds.size.z > LargestPiece) continue;

                Take(bounds);
            }
        }

        /// <summary>True if nothing stands within <paramref name="radius"/> of <paramref name="point"/>.</summary>
        public bool IsOpen(Vector3 point, float radius)
        {
            if (!area.Contains(new Vector2(point.x, point.z))) return false;

            int x0 = CellX(point.x - radius), x1 = CellX(point.x + radius);
            int z0 = CellZ(point.z - radius), z1 = CellZ(point.z + radius);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || z < 0 || x >= width || z >= depth) return false;
                    if (taken[z * width + x]) return false;
                }

            return true;
        }

        static bool IsIndoors(Transform root, string[] indoors)
        {
            if (indoors == null) return false;
            foreach (string name in indoors)
                if (root.name == name) return true;
            return false;
        }

        void Take(Bounds bounds)
        {
            int x0 = Mathf.Max(0, CellX(bounds.min.x - Margin)), x1 = Mathf.Min(width - 1, CellX(bounds.max.x + Margin));
            int z0 = Mathf.Max(0, CellZ(bounds.min.z - Margin)), z1 = Mathf.Min(depth - 1, CellZ(bounds.max.z + Margin));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    taken[z * width + x] = true;
        }

        int CellX(float x) => Mathf.FloorToInt((x - area.xMin) / cell);
        int CellZ(float z) => Mathf.FloorToInt((z - area.yMin) / cell);
    }
}
