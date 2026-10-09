using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Pictures of a model for checking it while it is being made: a copy stood on a patch of
    /// grass well away from the level, shot by a temporary camera with the level's light and grade,
    /// written to a PNG. Nothing is left behind in the scene.
    /// </summary>
    public static class LivestockPreview
    {
        static readonly Vector3 Stage = new Vector3(0f, 0f, -400f);

        /// <summary>
        /// Shoots <paramref name="prefab"/> from <paramref name="yaw"/> round and <paramref name="pitch"/>
        /// down, framing <paramref name="size"/> metres, into <paramref name="file"/>. The game camera
        /// looks from yaw about 145 (south-east of the subject), pitch 47.
        /// </summary>
        public static string Shoot(GameObject prefab, string file, float yaw, float pitch, float size, int width = 900, int height = 700,
                                   float fov = 15f, Vector3 lookAt = default, float spin = 0f)
        {
            GameObject subject = Object.Instantiate(prefab, Stage, Quaternion.Euler(0f, spin, 0f));
            subject.hideFlags = HideFlags.DontSave;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.hideFlags = HideFlags.DontSave;
            ground.transform.position = Stage + Vector3.down * 0.002f;
            ground.transform.localScale = Vector3.one * 3f;
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.SetColor("_BaseColor", new Color(0.4f, 0.5f, 0.28f));
            groundMaterial.SetFloat("_Smoothness", 0f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            var cameraObject = new GameObject("LivestockPreviewCamera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.6f, 0.62f);

            Vector3 target = Stage + (lookAt == default ? Vector3.up * size * 0.3f : lookAt);
            float distance = size * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.15f;
            Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(target - view * Vector3.forward * distance, view);

            var texture = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.aspect = width / (float)height;
            camera.Render();

            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, image.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(groundMaterial);
            Object.DestroyImmediate(subject);
            return file;
        }
    }
}
