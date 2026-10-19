using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Client.Interactions;
using Game.SOAP.Config;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>Curated props from separate packs, with a map prefab/mesh exclusion check.</summary>
    public static class AdditionalCategoriesImporter
    {
        public const string SelectionPath = ItemCollectionBuilder.Folder + "/AdditionalCategories.json";
        public static readonly string[] CategoryIds = { "music", "art", "christmas", "fashion" };
        private const string Sources = "Assets/ItemSources/AdditionalCategories/";
        private const float MinimumLongestAxis = 0.1622052f;

        [MenuItem("Tools/Game/Items/Add Music Art Christmas Fashion")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before importing items.");
            Directory.CreateDirectory("Logs/additional-categories");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var entries = ReadSelection();
            var mapAssets = CollectMapGeometryAssets();
            var meshFingerprints = MapMeshFingerprints(mapAssets);
            foreach (var entry in entries)
            {
                ValidateSource(entry.source, mapAssets);
                ValidateGeometryCopy(entry.source, meshFingerprints);
            }
            File.WriteAllText("Logs/additional-categories/map-audit.json", new JObject
            {
                ["mapPaths"] = new JArray(mapAssets.OrderBy(p => p)),
                ["distinctMapMeshFingerprints"] = meshFingerprints.Count,
                ["checkedSources"] = entries.Length,
                ["referenceOverlaps"] = 0, ["normalizedMeshCopies"] = 0,
                ["limitations"] = "Checks nested references and triangle geometry invariant to vertex order, translation and uniform scale. Not proof against rotated, retopologized or visually similar re-exports."
            }.ToString());

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path);
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Prefabs/PlayerCharacter.prefab")
                .transform.Find("Visual");
            var player = Object.Instantiate(visual.gameObject);
            Vector3 maximum;
            try { maximum = ItemCollectionBuilder.BoundsOf(player).size; }
            finally { Object.DestroyImmediate(player); }

            foreach (var id in CategoryIds)
            {
                var selected = entries.Where(e => e.category == id).ToArray();
                if (selected.Length < MatchRulesSO.MaxPlayerCount)
                    throw new InvalidOperationException("Not enough distinct items: " + id);
                var category = catalog.categories.FirstOrDefault(c => c.id == id);
                if (category == null)
                {
                    category = new ItemCatalogSO.Category { id = id };
                    var reserve = catalog.categories.FindIndex(c => c.id == "reserve");
                    catalog.categories.Insert(reserve < 0 ? catalog.categories.Count : reserve, category);
                }
                category.label = selected[0].categoryName;
                category.enabled = true;
                var items = new List<ItemCatalogSO.Item>();
                var folder = ItemCollectionBuilder.Folder + "/Prefabs/" + id;
                EnsureFolder(folder);
                foreach (var entry in selected)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.source);
                    var wrapper = BuildCarryable(source, entry, maximum);
                    try
                    {
                        var path = folder + "/" + entry.id + ".prefab";
                        entry.prefab = path;
                        var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, path);
                        if (prefab == null) throw new InvalidOperationException("Could not save " + path);
                        items.Add(new ItemCatalogSO.Item
                        {
                            id = "i" + entry.id.Substring(entry.id.LastIndexOf('_') + 1),
                            displayName = entry.displayName, enabled = true, prefab = prefab,
                        });
                    }
                    finally { Object.DestroyImmediate(wrapper); }
                }
                category.items = items;
                Debug.Log($"[AdditionalCategories] {category.label}: {items.Count} items");
            }
            MergeManifest(entries);
            catalog.Apply();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/additional-categories/import-result.json",
                JsonUtility.ToJson(new ItemCollectionBuilder.Manifest { items = entries }, true));
            Debug.Log("[AdditionalCategories] Import and map separation validation passed.");
        }

        // Preserve unknown root and item metadata; only update fields owned by this importer.
        public static void MergeManifest(ItemCollectionBuilder.Entry[] entries, string path = ItemCollectionBuilder.Folder + "/ItemCollection.json")
        {
            var manifest = JObject.Parse(File.ReadAllText(path));
            var items = (JArray)manifest["items"];
            foreach (var entry in entries)
            {
                var existing = items.OfType<JObject>().SingleOrDefault(e => (string)e["id"] == entry.id);
                var update = JObject.Parse(JsonUtility.ToJson(entry));
                if (existing == null) items.Add(update);
                else foreach (var property in update.Properties()) existing[property.Name] = property.Value.DeepClone();
            }
            File.WriteAllText(path, manifest.ToString());
        }

        public static ItemCollectionBuilder.Entry[] ReadSelection() =>
            JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(File.ReadAllText(SelectionPath)).items;

        // Follow only scene/prefab/model geometry references. Following all dependencies would
        // include the item catalog itself, falsely treating assigned items as map decorations.
        public static HashSet<string> CollectMapGeometryAssets()
        {
            var queue = new Queue<string>(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game/Content/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.EndsWith("/ItemGallery.unity", StringComparison.Ordinal) && !p.EndsWith("/ItemLab.unity", StringComparison.Ordinal)));
            var found = new HashSet<string>(StringComparer.Ordinal);
            var references = new Regex(@"guid:\s*([a-f0-9]{32})");
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                if (!found.Add(path) || !File.Exists(path)) continue;
                if (!path.EndsWith(".prefab") && !path.EndsWith(".unity")) continue;
                foreach (Match match in references.Matches(File.ReadAllText(path)))
                {
                    var dependency = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                    var extension = Path.GetExtension(dependency).ToLowerInvariant();
                    if (extension == ".prefab") queue.Enqueue(dependency);
                    else if (extension == ".fbx" || extension == ".obj" || extension == ".asset")
                    {
                        if (extension != ".asset" || AssetDatabase.LoadAssetAtPath<Mesh>(dependency) != null)
                            found.Add(dependency);
                    }
                }
            }
            return found;
        }

        public static void ValidateSource(string path, HashSet<string> mapAssets)
        {
            if (!path.StartsWith(Sources, StringComparison.Ordinal))
                throw new InvalidOperationException("Item source must be in the separate vendor collection: " + path);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new FileNotFoundException("Missing source", path);
            foreach (var dependency in AssetDatabase.GetDependencies(path))
                if (mapAssets.Contains(dependency))
                    throw new InvalidOperationException("Source reuses map geometry: " + dependency);
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0 || filters.Any(f => f.sharedMesh == null))
                throw new InvalidOperationException("Source has missing meshes: " + path);
        }

        public static Dictionary<string, string> MapMeshFingerprints(IEnumerable<string> paths)
        {
            var result = new Dictionary<string, string>();
            foreach (var path in paths.Where(p => !p.EndsWith(".unity") && !p.EndsWith(".prefab")))
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                result.TryAdd(MeshFingerprint(mesh), path + "#" + mesh.name);
            return result;
        }

        public static void ValidateGeometryCopy(string source, Dictionary<string, string> map)
        {
            foreach (var path in AssetDatabase.GetDependencies(source))
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                if (map.TryGetValue(MeshFingerprint(mesh), out var match))
                    throw new InvalidOperationException("Source contains a copy of map mesh: " + source + " => " + match);
        }

        public static string MeshFingerprint(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var bounds = mesh.bounds;
            var size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .000001f);
            var points = vertices.Select(v => (v - bounds.center) / size).Select(v =>
                Mathf.RoundToInt(v.x * 100000) + "," + Mathf.RoundToInt(v.y * 100000) + "," + Mathf.RoundToInt(v.z * 100000)).ToArray();
            var indices = mesh.triangles;
            var triangles = new string[indices.Length / 3];
            for (var i = 0; i < triangles.Length; i++)
            {
                var corners = new[] { points[indices[i*3]], points[indices[i*3+1]], points[indices[i*3+2]] };
                Array.Sort(corners, StringComparer.Ordinal);
                triangles[i] = string.Join(";", corners);
            }
            Array.Sort(triangles, StringComparer.Ordinal);
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", triangles))));
        }

        private static GameObject BuildCarryable(GameObject source, ItemCollectionBuilder.Entry entry, Vector3 maximum)
        {
            var root = new GameObject(entry.displayName);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.SetActive(true);
                foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                foreach (var transform in model.GetComponentsInChildren<Transform>(true))
                {
                    transform.gameObject.layer = 0;
                    transform.gameObject.isStatic = false;
                }
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterials.Any(m => m == null))
                        throw new InvalidOperationException("Missing source material: " + entry.source);
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(ItemCollectionBuilder.CompatibleMaterial).ToArray();
                    if (entry.category == "christmas")
                        foreach (var material in renderer.sharedMaterials)
                        {
                            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                                Sources + "Christmas/Models/Textures/colormap.png"));
                            material.SetColor("_BaseColor", Color.white);
                            EditorUtility.SetDirty(material);
                        }
                }
                var bounds = BoundsOf(root);
                entry.originalSize = bounds.size;
                var size = bounds.size;
                var scale = Mathf.Min(1f, maximum.x / Mathf.Max(size.x, .00001f),
                    maximum.y / Mathf.Max(size.y, .00001f), maximum.z / Mathf.Max(size.z, .00001f));
                scale = Mathf.Max(scale, MinimumLongestAxis / Mathf.Max(size.x, size.y, size.z));
                if (entry.category == "christmas") scale *= .6f;
                model.transform.localScale *= scale;
                bounds = BoundsOf(root);
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = BoundsOf(root);
                if (bounds.size.x > maximum.x + .001f || bounds.size.y > maximum.y + .001f || bounds.size.z > maximum.z + .001f)
                    throw new InvalidOperationException("Minimum item size exceeds character limits: " + entry.id);
                entry.finalSize = bounds.size;
                entry.scale = scale;
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * .01f);
                root.AddComponent<Rigidbody>().isKinematic = true;
                var carry = root.AddComponent<CarryableItem>();
                var serialized = new SerializedObject(carry);
                serialized.FindProperty("displayName").stringValue = entry.displayName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No visible geometry: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.sqrMagnitude < .0000001f) throw new InvalidOperationException("Empty bounds: " + root.name);
            return bounds;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
