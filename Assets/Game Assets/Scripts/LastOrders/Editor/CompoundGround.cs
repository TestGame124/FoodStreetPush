using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Decides where the compound's grass shows.
    ///
    /// The level's ground is one large grass plane. The wasteland outside the walls is four grey
    /// aprons laid over it - south, north, west and east - that leave a window where the
    /// compound is. Widening the compound therefore means pulling the aprons back rather than
    /// laying new grass on top: the grass underneath is the level's own texture, and any slab
    /// laid over it is a different green that shows as a band along the old boundary.
    ///
    /// The aprons are rebuilt around the window every time, from the outer edge they cover
    /// together, so running this again - or with a different window - never leaves a gap or a
    /// double layer.
    /// </summary>
    public static class CompoundGround
    {
        const string WastelandName = "Wasteland";

        /// <summary>
        /// Pulls the aprons back so the grass shows across <paramref name="compound"/>, in world
        /// metres (x and y of the rect are world X and Z). False when the level has no aprons to
        /// pull back, and the caller has to surface the new ground some other way.
        /// </summary>
        public static bool OpenWindow(Rect compound)
        {
            Transform wasteland = FindWasteland();
            if (wasteland == null) return false;

            Transform south = wasteland.Find("Apron_South");
            Transform north = wasteland.Find("Apron_North");
            Transform west = wasteland.Find("Apron_West");
            Transform east = wasteland.Find("Apron_East");

            if (south == null || north == null || west == null || east == null) return false;

            // The aprons are laid as axis-aligned boxes in their parent's space. A turned or
            // scaled parent would need a different calculation, and guessing at one would draw
            // the wasteland over the compound.
            if (Quaternion.Angle(wasteland.rotation, Quaternion.identity) > 0.1f ||
                (wasteland.lossyScale - Vector3.one).sqrMagnitude > 1e-4f)
            {
                Debug.LogWarning("[Last Orders] The wasteland aprons' parent is turned or scaled, so the " +
                                 "compound's ground was left as it is.");
                return false;
            }

            Rect outer = Extent(south);
            foreach (Transform apron in new[] { north, west, east })
            {
                Rect r = Extent(apron);
                outer = Rect.MinMaxRect(Mathf.Min(outer.xMin, r.xMin), Mathf.Min(outer.yMin, r.yMin),
                                        Mathf.Max(outer.xMax, r.xMax), Mathf.Max(outer.yMax, r.yMax));
            }

            Lay(south, outer.xMin, outer.yMin, outer.xMax, compound.yMin);
            Lay(north, outer.xMin, compound.yMax, outer.xMax, outer.yMax);
            Lay(west, outer.xMin, compound.yMin, compound.xMin, compound.yMax);
            Lay(east, compound.xMax, compound.yMin, outer.xMax, compound.yMax);

            return true;
        }

        static Transform FindWasteland()
        {
            GameObject environment = GameObject.Find("ApocalypseEnvironment");
            return environment != null ? environment.transform.Find(WastelandName) : null;
        }

        /// <summary>The ground an apron covers, from its position and scale: it is a unit cube.</summary>
        static Rect Extent(Transform apron)
        {
            Vector3 p = apron.position;
            Vector3 s = apron.localScale;
            return Rect.MinMaxRect(p.x - s.x * 0.5f, p.z - s.z * 0.5f, p.x + s.x * 0.5f, p.z + s.z * 0.5f);
        }

        /// <summary>
        /// Stretches an apron over a rectangle, keeping its height and thickness. An empty
        /// rectangle - the window reaching the outer edge - hides the apron rather than leaving
        /// a zero-width box in the scene.
        /// </summary>
        static void Lay(Transform apron, float minX, float minZ, float maxX, float maxZ)
        {
            Undo.RecordObject(apron, "Open compound ground");
            Undo.RecordObject(apron.gameObject, "Open compound ground");

            bool empty = maxX - minX < 0.01f || maxZ - minZ < 0.01f;
            apron.gameObject.SetActive(!empty);
            if (empty) return;

            Vector3 p = apron.position;
            apron.position = new Vector3((minX + maxX) * 0.5f, p.y, (minZ + maxZ) * 0.5f);

            Vector3 s = apron.localScale;
            apron.localScale = new Vector3(maxX - minX, s.y, maxZ - minZ);
        }
    }
}
