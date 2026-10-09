using System;
using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>
    /// The pieces a tiled object is drawn from - a plant bed, a fence, a hedge - and the meshes put
    /// together from them for each neighbourhood.
    ///
    /// Each cell is four quarters, and each quarter takes one of the five <see cref="QuarterShape"/>s,
    /// so twenty pieces cover every way a cell can sit among its neighbours. A post (the joint) is
    /// added wherever a straight border runs on into the next cell. Pieces are modelled round the
    /// cell's centre, axis-aligned in world space: north is +Z, east +X.
    ///
    /// Meshes are combined the first time a neighbourhood is needed and then shared by every cell
    /// with that neighbourhood; there are at most 85 of them per set.
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Placement/Auto Tile Set")]
    public sealed class AutoTileSet : ScriptableObject
    {
        public const int PieceCount = TileMask.Quarters * 5;

        [Tooltip("The quarter pieces, 5 shapes per quarter (Corner, BorderZ, BorderX, InsideCorner, Open), quarters NE, SE, SW, NW.")]
        [SerializeField] Mesh[] pieces = new Mesh[PieceCount];

        [Tooltip("Stands where a straight border runs on from one cell into the next. Optional.")]
        [SerializeField] Mesh joint;

        [Tooltip("From the cell's centre to its edge, in metres.")]
        [SerializeField] float halfCell = 0.5f;

        [Tooltip("From the cell's centre to the middle of a border, in metres: where joints stand.")]
        [SerializeField] float borderInset = 0.43f;

        [NonSerialized] Dictionary<int, Mesh> built;
        static readonly TileJoint[] JointBuffer = new TileJoint[4];

        public float HalfCell => halfCell;
        public float BorderInset => borderInset;

        public static int PieceIndex(int quarter, QuarterShape shape) => quarter * 5 + (int)shape;

        public Mesh Piece(int quarter, QuarterShape shape) => pieces[PieceIndex(quarter, shape)];

        /// <summary>Sets the pieces, the joint and the measurements: what the set's builder calls.</summary>
        public void Configure(Mesh[] quarterPieces, Mesh jointPost, float half, float inset)
        {
            if (quarterPieces == null || quarterPieces.Length != PieceCount)
                throw new ArgumentException($"An auto tile set takes {PieceCount} pieces.", nameof(quarterPieces));

            pieces = (Mesh[])quarterPieces.Clone();
            joint = jointPost;
            halfCell = half;
            borderInset = inset;
            Forget();
        }

        /// <summary>The mesh for a cell whose joined neighbours are <paramref name="mask"/> (<see cref="TileMask"/> bits).</summary>
        public Mesh MeshFor(int mask)
        {
            mask = TileMask.Canonical(mask);

            built ??= new Dictionary<int, Mesh>();
            if (built.TryGetValue(mask, out Mesh cached) && cached != null) return cached;

            Mesh mesh = Combine(mask, name);
            mesh.hideFlags = HideFlags.DontSave;
            built[mask] = mesh;
            return mesh;
        }

        /// <summary>
        /// A new mesh for <paramref name="mask"/>, owned by the caller: the four quarters and any
        /// joints, merged into one submesh. Every piece must share one material.
        /// </summary>
        public Mesh Combine(int mask, string label)
        {
            mask = TileMask.Canonical(mask);

            var parts = new List<CombineInstance>(8);
            for (int quarter = 0; quarter < TileMask.Quarters; quarter++)
            {
                Mesh piece = Piece(quarter, TileMask.Shape(mask, quarter));
                if (piece != null) parts.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity });
            }

            if (joint != null)
            {
                int joints = TileMask.Joints(mask, JointBuffer);
                for (int i = 0; i < joints; i++)
                {
                    Matrix4x4 at = Matrix4x4.Translate(JointBuffer[i].Position(halfCell, borderInset));
                    parts.Add(new CombineInstance { mesh = joint, transform = at });
                }
            }

            var mesh = new Mesh { name = $"{label} {mask:X2}" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Drops the combined meshes, so the next request rebuilds them from the current pieces.</summary>
        public void Forget()
        {
            if (built == null) return;

            foreach (Mesh mesh in built.Values)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }

            built.Clear();
        }

        void OnDisable() => Forget();
    }
}
