using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Contact sheets of the workers' clips while they are being made: each clip sampled at a few
    /// moments on each worker's rig, in a row, shot from the front and from the side, written to a
    /// PNG. Nothing is left in the scene.
    /// </summary>
    public static class WorkerAnimationPreview
    {
        static readonly Vector3 Stage = new Vector3(0f, 0f, -600f);

        static readonly string[] Rigs =
        {
            "Assets/PolygonFarm/Prefabs/Characters/SM_Chr_FarmGirl_01.prefab",
            "Assets/PolygonFarm/Prefabs/Characters/SM_Chr_Farmer_Male_01.prefab",
            "Assets/Game Assets/Prefabs/Characters/MainCharacter.prefab",
        };

        /// <summary>
        /// Renders <paramref name="clips"/> (names under the worker clip folder, without "Worker_")
        /// at <paramref name="shares"/> of their length: one row per clip, a column per moment per rig.
        /// </summary>
        public static string Sheet(string file, string[] clips, float[] shares, float yaw = 180f, int rig = -1)
        {
            var made = new List<GameObject>();
            int columns = shares.Length * (rig < 0 ? Rigs.Length : 1);

            try
            {
                for (int row = 0; row < clips.Length; row++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{WorkerAnimations.ClipFolder}/Worker_{clips[row]}.anim");
                    if (clip == null) continue;

                    int column = 0;
                    for (int r = 0; r < Rigs.Length; r++)
                    {
                        if (rig >= 0 && r != rig) continue;

                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Rigs[r]);
                        foreach (float share in shares)
                        {
                            var go = (GameObject)Object.Instantiate(prefab);
                            go.hideFlags = HideFlags.DontSave;
                            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;

                            // The main character has two rigs in it; only the shown one.
                            Animator animator = null;
                            foreach (Animator a in go.GetComponentsInChildren<Animator>())
                                if (a.gameObject.activeInHierarchy) { animator = a; break; }
                            if (animator == null) { Object.DestroyImmediate(go); continue; }

                            go.transform.SetPositionAndRotation(Stage + new Vector3(column * 1.1f, -row * 2.2f, 0f), Quaternion.Euler(0f, yaw, 0f));

                            // A playable graph does nothing to an animator out of play mode; sampling does. It also
                            // moves a rig whose animator is on its root, so the figure is put back in its place after.
                            clip.SampleAnimation(animator.gameObject, share * clip.length);
                            go.transform.SetPositionAndRotation(Stage + new Vector3(column * 1.1f, -row * 2.2f, 0f), Quaternion.Euler(0f, yaw, 0f));
                            if (animator.transform != go.transform) animator.transform.localPosition = new Vector3(0f, animator.transform.localPosition.y, 0f);

                            foreach (SkinnedMeshRenderer skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                            made.Add(go);
                            column++;
                        }
                    }
                }

                float width = columns * 1.1f, height = clips.Length * 2.2f;
                var centre = Stage + new Vector3((columns - 1) * 1.1f * 0.5f, 1f - (clips.Length - 1) * 2.2f * 0.5f, 0f);
                return Shoot(file, centre, width, height);
            }
            finally
            {
                foreach (GameObject go in made) Object.DestroyImmediate(go);
            }
        }

        static string Shoot(string file, Vector3 centre, float width, float height)
        {
            var cameraObject = new GameObject("WorkerPreviewCamera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(height * 0.55f, width * 0.55f / 2.2f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.6f, 0.62f);

            int w = 2200, h = Mathf.Clamp(Mathf.RoundToInt(w * height / Mathf.Max(width, 0.1f)), 300, 2400);
            var texture = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.aspect = w / (float)h;
            camera.orthographicSize = Mathf.Max(height * 0.55f, width * 0.55f / camera.aspect);
            camera.transform.SetPositionAndRotation(centre + new Vector3(0f, 0.3f, -20f), Quaternion.Euler(1f, 0f, 0f));
            camera.Render();

            RenderTexture.active = texture;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, image.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(cameraObject);
            return file;
        }
    }
}
