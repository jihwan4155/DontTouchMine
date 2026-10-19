using System;
using System.Linq;
using Game.Client.Interactions;
using Game.Client.Lobby;
using Game.Editor;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class AdditionalCategoriesTests
    {
        [TestCase("music", 20, "악기·음악용품", "Music & Instruments")]
        [TestCase("art", 13, "문구·미술용품", "Stationery & Art")]
        [TestCase("christmas", 20, "크리스마스", "Christmas")]
        [TestCase("fashion", 20, "패션·잡화", "Fashion & Accessories")]
        public void AddedCategories_ArePlayableLocalizedAndCarryable(string id, int count, string ko, string en)
        {
            var catalog = ItemCatalogSO.Load();
            var category = catalog.categories.Single(c => c.id == id);
            Assert.That(category.enabled, Is.True);
            Assert.That(category.items.Count(i => i.enabled), Is.EqualTo(count));
            Assert.That(PlaySettingsCategoryCatalog.Contains(id), Is.True);
            Assert.That(PlaySettingsCategoryCatalog.LabelOf(id, "ko"), Is.EqualTo(ko));
            Assert.That(PlaySettingsCategoryCatalog.LabelOf(id, "en"), Is.EqualTo(en));
            foreach (var item in category.items)
            {
                Assert.That(item.prefab.GetComponent<CarryableItem>(), Is.Not.Null, item.id);
                Assert.That(item.prefab.GetComponent<CarryableItem>().DisplayName, Is.EqualTo(item.displayName), item.id);
                Assert.That(item.prefab.GetComponent<Rigidbody>(), Is.Not.Null, item.id);
                var box = item.prefab.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null, item.id);
                Assert.That(box.isTrigger, Is.False, item.id);
                var renderers = item.prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, item.id);
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, item.id);
                        Assert.That(material.shader, Is.Not.Null, item.id);
                        Assert.That(material.shader.name, Does.Not.Contain("InternalErrorShader"), item.id);
                    }
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.GreaterThanOrEqualTo(.162f * (id == "christmas" ? .6f : 1f)), item.id);
                var colliderBounds = new Bounds(box.center, box.size + Vector3.one * .002f);
                Assert.That(colliderBounds.Contains(bounds.min) && colliderBounds.Contains(bounds.max), Is.True, item.id);
            }
        }

        [Test]
        public void NewSourcesAndGeneratedPrefabs_DoNotReuseMapGeometry()
        {
            var maps = AdditionalCategoriesImporter.CollectMapGeometryAssets();
            Assert.That(maps.Any(p => p.EndsWith("/Supermarket.unity")), Is.True);
            Assert.That(maps.Any(p => p.EndsWith("/Mansion.unity")), Is.True);
            var fingerprints = AdditionalCategoriesImporter.MapMeshFingerprints(maps);
            var catalog = ItemCatalogSO.Load();
            foreach (var entry in AdditionalCategoriesImporter.ReadSelection())
            {
                Assert.DoesNotThrow(() => AdditionalCategoriesImporter.ValidateSource(entry.source, maps), entry.source);
                Assert.DoesNotThrow(() => AdditionalCategoriesImporter.ValidateGeometryCopy(entry.source, fingerprints), entry.source);
                var category = catalog.categories.Single(c => c.id == entry.category);
                var item = category.items.Single(i => i.id == "i" + entry.id.Substring(entry.id.LastIndexOf('_') + 1));
                var overlap = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(item.prefab)).Where(maps.Contains);
                Assert.That(overlap, Is.Empty, item.id);
            }
        }

        [Test]
        public void GallerySupportsEveryMergedManifestCategory()
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(ItemCollectionBuilder);
            var ids = (string[])type.GetField("Categories", flags).GetValue(null);
            var names = (string[])type.GetField("Names", flags).GetValue(null);
            var colors = (Color[])type.GetField("Colors", flags).GetValue(null);
            var manifest = JsonUtility.FromJson<ItemCollectionBuilder.Manifest>(System.IO.File.ReadAllText(ItemCollectionBuilder.Folder + "/ItemCollection.json"));
            Assert.That(manifest.items.Select(e => e.category).Except(ids), Is.Empty);
            Assert.That(names.Length, Is.EqualTo(ids.Length));
            Assert.That(colors.Length, Is.EqualTo(ids.Length));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        [Test]
        public void GeometryFingerprint_DetectsReorderedTranslatedAndScaledCopy()
        {
            var original = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            var copy = new Mesh { vertices = new[] { Vector3.up * 3 + Vector3.one, Vector3.one, Vector3.right * 3 + Vector3.one }, triangles = new[] { 0, 2, 1 } };
            try { Assert.That(AdditionalCategoriesImporter.MeshFingerprint(copy), Is.EqualTo(AdditionalCategoriesImporter.MeshFingerprint(original))); }
            finally { UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void GeometryGuard_RejectsCopiedGeometryEvenWithDifferentAssetPath()
        {
            var entry = AdditionalCategoriesImporter.ReadSelection()[0];
            var mesh = AssetDatabase.GetDependencies(entry.source).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Mesh>().First();
            var signatures = new System.Collections.Generic.Dictionary<string, string>
            { [AdditionalCategoriesImporter.MeshFingerprint(mesh)] = "Assets/UnrelatedMapCopy.fbx" };
            Assert.Throws<InvalidOperationException>(() => AdditionalCategoriesImporter.ValidateGeometryCopy(entry.source, signatures));
        }

        [Test]
        public void ManifestMerge_PreservesExistingEntriesAndUnknownMetadataAndIsIdempotent()
        {
            var path = "Temp/AdditionalCategoriesTests-manifest.json";
            const string original = "{\"futureMetadata\":{\"keep\":true},\"items\":[{\"id\":\"legacy\",\"custom\":42}]}";
            System.IO.File.WriteAllText(path, original);
            try
            {
                var entries = AdditionalCategoriesImporter.ReadSelection();
                AdditionalCategoriesImporter.MergeManifest(entries, path);
                var once = System.IO.File.ReadAllText(path);
                AdditionalCategoriesImporter.MergeManifest(entries, path);
                Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo(once));
                var json = Newtonsoft.Json.Linq.JObject.Parse(once);
                Assert.That((bool)json["futureMetadata"]["keep"], Is.True);
                Assert.That((int)json["items"][0]["custom"], Is.EqualTo(42));
                Assert.That(json["items"].Count(), Is.EqualTo(entries.Length + 1));
            }
            finally { System.IO.File.Delete(path); }
        }

        [Test]
        public void SourceGuard_RejectsASourceWhenItsGeometryIsUsedByAMap()
        {
            var entry = AdditionalCategoriesImporter.ReadSelection()[0];
            var dependencies = AssetDatabase.GetDependencies(entry.source);
            var meshPath = dependencies.First(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
            Assert.Throws<InvalidOperationException>(() => AdditionalCategoriesImporter.ValidateSource(entry.source,
                new System.Collections.Generic.HashSet<string> { meshPath }));
        }
    }
}
