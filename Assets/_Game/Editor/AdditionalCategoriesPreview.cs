using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class AdditionalCategoriesPreview
    {
        public static void ImportAndRender()
        {
            AdditionalCategoriesImporter.Import();
            Render();
        }

        // Batch entry point. Render the saved gameplay prefabs, including their final materials.
        public static void Render()
        {
            RenderEntries(AdditionalCategoriesImporter.ReadSelection(), "Logs/additional-categories");
        }

        [MenuItem("Tools/Game/Items/Render Duplicate Name Review %#F8")]
        public static void RenderDuplicateNames()
        {
            var categories = new[] { "tools", "fantasy", "beach", "casino", "halloween", "toys" };
            var catalog = AssetDatabase.LoadAssetAtPath<Game.SOAP.Config.ItemCatalogSO>(ItemCatalogSetup.Path);
            var ids = catalog.categories.Where(c => categories.Contains(c.id))
                .SelectMany(c => c.items.Where(i => i.enabled).GroupBy(i => i.displayName)
                    .Where(g => g.Count() > 1).SelectMany(g => g.Select(i => i.id))).ToHashSet();
            var manifest = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(
                File.ReadAllText(ItemCollectionBuilder.Folder + "/ItemCollection.json"));
            var entries = manifest.items.Where(e => ids.Contains("i" + e.id.Substring(e.id.IndexOf('_') + 1))).ToArray();
            if (entries.Length != ids.Count) throw new InvalidOperationException("Catalog/manifest mismatch.");
            RenderEntries(entries, "Logs/duplicate-names", true);
        }

        private static void RenderEntries(ItemCollectionBuilder.Entry[] entries, string outputFolder, bool allowSparsePreview = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rendering.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var folder = outputFolder + "/renders";
            Directory.CreateDirectory(folder);
            var camera = new GameObject("Item preview camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.14f, .17f, .21f);
            camera.cullingMask = 1 << 30;
            camera.nearClipPlane = .001f;
            camera.farClipPlane = 100;
            camera.orthographic = true;
            var light = new GameObject("Preview key light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(40, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            RenderSettings.fog = false;
            var wireMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            wireMaterial.SetColor("_BaseColor", Color.yellow);
            try
            {
                foreach (var entry in entries)
                {
                    var path = ItemCollectionBuilder.Folder + "/Prefabs/" + entry.category + "/" + entry.id + ".prefab";
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) throw new InvalidOperationException("Missing generated prefab: " + path);
                    var instance = Object.Instantiate(asset);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    instance.transform.position += new Vector3(10000, 0, 0);
                    try
                    {
                        foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                        var renderers = instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                        camera.orthographicSize = bounds.size.magnitude * .62f;
                        camera.transform.position = bounds.center + new Vector3(-1.4f, .8f, 1.8f).normalized * bounds.size.magnitude * 3;
                        camera.transform.LookAt(bounds.center);
                        Capture(camera, folder + "/" + entry.id + ".png", allowSparsePreview);
                        DrawCollider(instance, wireMaterial);
                        camera.transform.position = bounds.center + new Vector3(1.4f, 1, -1.8f).normalized * bounds.size.magnitude * 3;
                        camera.transform.LookAt(bounds.center);
                        Capture(camera, folder + "/" + entry.id + "_back.png", allowSparsePreview);
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                File.WriteAllText(outputFolder + "/render-result.txt", $"PASS: {entries.Length} saved prefabs rendered from two sides.");
            }
            finally
            {
                Object.DestroyImmediate(wireMaterial);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(light.gameObject);
                if (previousScene.IsValid() && previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void DrawCollider(GameObject instance, Material material)
        {
            var box = instance.GetComponent<BoxCollider>();
            for (var corner = 0; corner < 8; corner++)
            for (var axis = 0; axis < 3; axis++)
            {
                if ((corner & (1 << axis)) != 0) continue;
                Vector3 Point(int bits) => box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f,
                    new Vector3((bits & 1) == 0 ? -1 : 1, (bits & 2) == 0 ? -1 : 1, (bits & 4) == 0 ? -1 : 1)));
                var edge = new GameObject("Collider edge");
                edge.layer = 30;
                edge.transform.SetParent(instance.transform, false);
                var line = edge.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.positionCount = 2;
                line.SetPositions(new[] { Point(corner), Point(corner | (1 << axis)) });
                line.startWidth = line.endWidth = box.size.magnitude * .002f;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
        }

        private static void Capture(Camera camera, string path, bool allowSparsePreview)
        {
            var target = RenderTexture.GetTemporary(384, 320, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(384, 320, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 384, 320), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                var background = pixels[0];
                var foreground = pixels.Count(p => Math.Abs(p.r-background.r)+Math.Abs(p.g-background.g)+Math.Abs(p.b-background.b)>35);
                if (foreground < 20)
                {
                    if (!allowSparsePreview) throw new InvalidOperationException("Empty render: " + path);
                    Debug.LogWarning("Preview has little visible geometry; inspect the opposite view: " + path);
                }
                if (pixels.Count(p => p.r > 220 && p.b > 220 && p.g < 40) > pixels.Length / 20)
                    throw new InvalidOperationException("Error-shader magenta render: " + path);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
