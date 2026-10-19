#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Client.Interactions;
using Game.Client.Lobby;
using Game.Core.Items;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Items
{
    /// <summary>Opt-in audit of the actual built player's resources and assignment definitions.</summary>
    public sealed class ItemPlayerVerification : MonoBehaviour
    {
        [Serializable] public sealed class ItemRow
        {
            public string id, name, prefabName, assignmentName;
            public bool enabled;
        }
        [Serializable] public sealed class CategoryRow
        {
            public string id, label, pickerLabel, hudLabel;
            public bool enabled;
            public ItemRow[] items;
        }
        [Serializable] public sealed class Report
        {
            public string result, error, version;
            public int assignmentChecks;
            public CategoryRow[] categories;
            public string[] screenshots;
        }
        private static string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--item-review");
            if (index < 0) return;
            output = index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "ItemReview");
            Directory.CreateDirectory(output);
            SceneManager.sceneLoaded += EnterLab;
        }

        private static void EnterLab(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= EnterLab;
            // Skip normal login flow only when explicitly launched in verification mode.
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
            var runner = new GameObject("Built player item verification").AddComponent<ItemPlayerVerification>();
            DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("ItemLab", LoadSceneMode.Single);
            yield return null;
            var report = new Report { version = Application.version, result = "Failed" };
            ItemCatalogSO catalog = null;
            try
            {
                // Inspect definitions initialized from shipped metadata before loading/applying full catalog.
                catalog = ItemCatalogSO.Load(false);
                catalog.Validate();
                var metadata = ItemCatalogSO.LoadMetadata();
                var picker = PlaySettingsCategoryCatalog.All;
                report.categories = catalog.categories.Select(c => new CategoryRow
                {
                    id = c.id, label = c.label, enabled = c.enabled,
                    pickerLabel = picker.FirstOrDefault(p => p.Id == c.id).Label,
                    hudLabel = c.enabled ? PlaySettingsCategoryCatalog.LabelOf(c.id, "ko") : "",
                    items = c.items.Select(i => new ItemRow
                    {
                        id = i.id, name = i.displayName, enabled = i.enabled,
                        prefabName = i.prefab != null ? i.prefab.GetComponent<CarryableItem>()?.DisplayName : null,
                        assignmentName = c.enabled && i.enabled ? ItemCatalog.DisplayNameOf(i.id) : ""
                    }).ToArray()
                }).ToArray();
                foreach (var c in catalog.categories)
                {
                    var mc = metadata.categories.Single(m => m.id == c.id);
                    Require(mc.enabled == c.enabled && mc.label == c.label && mc.items.Count == c.items.Count, "Metadata category: " + c.id);
                    foreach (var i in c.items)
                    {
                        var mi = mc.items.Single(m => m.id == i.id);
                        Require(mi.displayName == i.displayName && mi.enabled == i.enabled, "Metadata item: " + i.id);
                        Require(i.prefab.GetComponent<CarryableItem>().DisplayName == i.displayName, "Prefab name: " + i.id);
                        if (c.enabled && i.enabled) Require(ItemCatalog.DisplayNameOf(i.id) == i.displayName, "Assignment name: " + i.id);
                    }
                    if (!c.enabled) continue;
                    Require(picker.Any(p => p.Id == c.id && p.Label == c.label), "Picker: " + c.id);
                    Require(PlaySettingsCategoryCatalog.LabelOf(c.id, "ko") == c.label, "HUD category: " + c.id);
                    for (var count = 1; count <= MatchRulesSO.MaxPlayerCount; count++)
                    for (var seed = 0; seed < 50; seed++)
                    {
                        var assigned = ItemAssignmentSystem.Assign(ItemCatalog.Definitions, count, new System.Random(seed), c.id);
                        Require(assigned.Select(a => a.Item.ItemId).Distinct().Count() == count && assigned.All(a => a.Item.Category == c.id), "Assignment: " + c.id);
                        report.assignmentChecks++;
                    }
                }
                var reached = new HashSet<string>();
                for (var seed = 0; seed < 300; seed++) reached.Add(ItemAssignmentSystem.Assign(6, new System.Random(seed))[0].Item.Category);
                Require(reached.SetEquals(catalog.categories.Where(c => c.enabled).Select(c => c.id)), "Random category reachability");
            }
            catch (Exception ex)
            {
                report.error = ex.ToString();
                File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
                Debug.LogException(ex);
                yield break;
            }
            var lab = FindFirstObjectByType<ItemSoloLab>();
            var screenshots = new List<string>();
            foreach (var c in catalog.categories.Where(c => c.enabled))
            {
                var item = c.items.First(i => i.enabled);
                lab.SelectItem(c.id, item.id);
                yield return new WaitForEndOfFrame();
                var file = c.id + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(output, file));
                screenshots.Add(file);
                yield return null;
            }
            foreach (var name in new[] { "고칼슘우유", "철가방", "귀여운 선인장", "파란자루 망치", "돼지 저금통" })
            {
                var c = catalog.categories.First(x => x.items.Any(i => i.displayName == name));
                var item = c.items.First(i => i.displayName == name);
                lab.SelectItem(c.id, item.id);
                yield return new WaitForEndOfFrame();
                var file = item.id + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(output, file));
                screenshots.Add(file);
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            report.screenshots = screenshots.ToArray();
            report.result = screenshots.All(f => File.Exists(Path.Combine(output, f))) ? "Passed" : "Failed";
            if (report.result != "Passed") report.error = "Missing player screenshot";
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[ItemPlayerVerification] " + report.result);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
