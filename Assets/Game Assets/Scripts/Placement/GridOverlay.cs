using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FoodStreet.Placement
{
    /// <summary>One line mesh for the plot, rebuilt only on initialization; no per-cell GameObjects.</summary>
    public sealed class GridOverlay : IDisposable
    {
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        public GridOverlay(GridLayout layout)
        {
            root = new GameObject("Placement Grid Overlay");
            mesh = new Mesh { name = "Placement Grid Lines", indexFormat = IndexFormat.UInt32 };
            int count = (layout.Width + layout.Height + 2) * 2;
            var vertices = new Vector3[count];
            var indices = new int[count];
            int i = 0;
            for (int x = 0; x <= layout.Width; x++)
            {
                vertices[i++] = layout.CellToWorld(new Vector2Int(x, 0));
                vertices[i++] = layout.CellToWorld(new Vector2Int(x, layout.Height));
            }
            for (int z = 0; z <= layout.Height; z++)
            {
                vertices[i++] = layout.CellToWorld(new Vector2Int(0, z));
                vertices[i++] = layout.CellToWorld(new Vector2Int(layout.Width, z));
            }
            for (i = 0; i < count; i++) { vertices[i].y += .025f; indices[i] = i; }
            mesh.vertices = vertices; mesh.SetIndices(indices, MeshTopology.Lines, 0); mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader) { name = "Placement Grid Lines" };
                material.SetColor("_BaseColor", new Color(.45f, .65f, .55f));
                material.SetColor("_Color", new Color(.45f, .65f, .55f));
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            SetVisible(false);
        }
        public void SetVisible(bool visible) { if (root != null) root.SetActive(visible); }
        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
