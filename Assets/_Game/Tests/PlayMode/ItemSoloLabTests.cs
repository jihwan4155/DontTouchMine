#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Game.Client.Interactions;
using Game.Client.Items;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class ItemSoloLabTests
    {
        private ItemSoloLab lab;
        [UnitySetUp]
        public IEnumerator OpenLab()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Content/Scenes/ItemLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            lab = Object.FindFirstObjectByType<ItemSoloLab>();
            Assert.That(lab, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AllPlayableCategoriesCanBeInspectedWithoutAMatch()
        {
            var catalog = ItemCatalogSO.Load(false);
            Assert.That(lab.CategoryCount, Is.EqualTo(catalog.categories.Count(c => c.enabled)));
            foreach (var category in catalog.categories.Where(c => c.enabled))
            {
                var item = category.items.First(i => i.enabled && i.prefab != null);
                Assert.That(lab.SelectItem(category.id, item.id), Is.True);
                Assert.That(lab.Current.DisplayName, Is.EqualTo(item.displayName));
                Assert.That(lab.Current.transform.localScale, Is.EqualTo(item.prefab.transform.localScale));
                yield return null;
            }
            Assert.That(GameObject.Find("Player size reference - 1x"), Is.Not.Null);
            Assert.That(lab.reviewFont, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PreviewChangesDoNotModifyPrefabAndPickupDropAndRecoveryWork()
        {
            var source = lab.catalog.categories.Single(c => c.id == "art").items.First(i => i.displayName.Contains("붓"));
            var savedScale = source.prefab.transform.localScale;
            var savedBox = source.prefab.GetComponent<BoxCollider>().size;
            Assert.That(lab.SelectItem("art", source.id), Is.True);
            lab.PreviewScale = 1.5f;
            lab.MinimumThickness = .06f;
            lab.SpawnSelection();
            var box = lab.Current.GetComponent<BoxCollider>();
            var size = Vector3.Scale(box.size, box.transform.lossyScale);
            Assert.That(Mathf.Min(size.x, size.y, size.z), Is.GreaterThanOrEqualTo(.05999f));
            Assert.That(source.prefab.transform.localScale, Is.EqualTo(savedScale));
            Assert.That(source.prefab.GetComponent<BoxCollider>().size, Is.EqualTo(savedBox));
            lab.Hold();
            Assert.That(lab.Current.IsCarried, Is.True);
            Assert.That(box.enabled, Is.False);
            lab.Drop();
            Assert.That(box.enabled, Is.True);
            for (var i = 0; i < 75; i++) yield return new WaitForFixedUpdate();
            Assert.That(lab.Current.transform.position.y, Is.GreaterThan(-.1f));
            Assert.That(lab.LastContact, Does.Contain("Test floor"));
            lab.Current.GetComponent<Rigidbody>().isKinematic = true;
            lab.Current.transform.position = Vector3.down * 3;
            yield return null;
            Assert.That(lab.Warning, Does.Contain("바닥 아래"));
            lab.SpawnSelection();
            Assert.That(lab.Warning, Is.Empty);
            Assert.That(lab.Current.transform.position.y, Is.GreaterThan(-.1f));
        }

        [UnityTest]
        public IEnumerator ThinItemHitsTwoCentimetreWallAndLabRenders()
        {
            var source = lab.catalog.categories.Single(c => c.id == "art").items.First(i => i.displayName.Contains("붓"));
            lab.SelectItem("art", source.id);
            lab.ThrowSpeed = 30;
            lab.ThrowAngle = 0;
            lab.Throw();
            Assert.That(lab.Current.GetComponent<Rigidbody>().collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            for (var i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(lab.Current.transform.position.z, Is.LessThan(4.05f));
            Assert.That(lab.LastContact, Does.Contain("Test wall"));
            lab.SelectItem("music", lab.catalog.categories.Single(c => c.id == "music").items.First().id);
            yield return null;
            Directory.CreateDirectory("Logs/item-lab");
            ScreenCapture.CaptureScreenshot("Logs/item-lab/lab-screen.png");
            for (var i = 0; i < 5; i++) yield return null;
        }

        [Test]
        public void EveryThinPlayablePrefabSurvivesFastFloorAndWallImpacts()
        {
            var scene = SceneManager.CreateScene("Isolated thin item physics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var physics = scene.GetPhysicsScene();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.name = "Probe floor";
            floor.transform.position = new Vector3(0, -.15f, 0);
            floor.transform.localScale = new Vector3(30, .3f, 30);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.position = new Vector3(0, 2, 4);
            wall.transform.localScale = new Vector3(10, 4, .02f);
            var checkedItems = 0;
            try
            {
                foreach (var category in lab.catalog.categories.Where(c => c.enabled))
                foreach (var entry in category.items.Where(i => i.enabled && i.prefab != null))
                {
                    var sourceBox = entry.prefab.GetComponent<BoxCollider>();
                    if (sourceBox == null) continue;
                    var size = Vector3.Scale(sourceBox.size, sourceBox.transform.lossyScale);
                    if (Mathf.Min(size.x, size.y, size.z) >= .03f) continue;
                    var instance = Object.Instantiate(entry.prefab);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    var carry = instance.GetComponent<CarryableItem>();
                    carry.SetOwningScene(scene);
                    var body = instance.GetComponent<Rigidbody>();
                    try
                    {
                        carry.OnReleased(new Pose(new Vector3(0, 3, 0), Quaternion.identity), Vector3.down * 30);
                        Physics.SyncTransforms();
                        for (var step = 0; step < 100; step++) physics.Simulate(.02f);
                        Assert.That(body.position.y, Is.GreaterThan(-.2f), entry.displayName + " crossed floor");
                        carry.OnReleased(new Pose(new Vector3(0, 1.5f, 0), Quaternion.identity), Vector3.forward * 30);
                        Physics.SyncTransforms();
                        for (var step = 0; step < 20; step++) physics.Simulate(.02f);
                        Assert.That(body.position.z, Is.LessThan(4.1f), entry.displayName + " crossed thin wall");
                        checkedItems++;
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                Assert.That(checkedItems, Is.GreaterThan(5));
                Directory.CreateDirectory("Logs/item-lab");
                File.WriteAllText("Logs/item-lab/thin-physics-result.txt", $"PASS: {checkedItems} thin playable prefabs; 30 m/s floor and 2 cm wall impacts; 0.02 s physics step.");
            }
            finally
            {
                Object.DestroyImmediate(floor); Object.DestroyImmediate(wall);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
