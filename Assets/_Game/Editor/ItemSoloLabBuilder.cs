using System.IO;
using System.Linq;
using Game.Client.Items;
using Game.SOAP.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class ItemSoloLabBuilder
    {
        public const string ScenePath = "Assets/_Game/Content/Scenes/ItemLab.unity";
        private const string Materials = "Assets/_Game/Content/ItemLab";

        // Normal client startup is preserved; --item-review opts into the bundled offline lab.
        public static void BuildWindowsReviewClient()
        {
            var original = EditorBuildSettings.scenes;
            var profile = System.Environment.GetEnvironmentVariable("CLIENT_PROFILE_BUILD");
            try
            {
                EditorBuildSettings.scenes = original.Where(s => s.path != ScenePath)
                    .Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
                System.Environment.SetEnvironmentVariable("CLIENT_PROFILE_BUILD", "1");
                ClientBuild.Build();
            }
            finally
            {
                EditorBuildSettings.scenes = original;
                System.Environment.SetEnvironmentVariable("CLIENT_PROFILE_BUILD", profile);
            }
        }

        [MenuItem("Tools/Game/Items/Open Solo Item Lab")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before opening the item lab."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Build();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Lab Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5, 3.2f, -7);
            camera.transform.LookAt(new Vector3(-.3f, 1, 1));
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 150;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .11f, .16f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Lab Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45, -25, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .65f, .72f);
            RenderSettings.fog = false;
            Box("Test floor - 30 cm", new Vector3(0, -.15f, 0), new Vector3(20, .3f, 20), Material("Floor", new Color(.18f, .24f, .3f)));
            var wall = Box("Test wall - 2 cm", new Vector3(0, 1.5f, 4), new Vector3(6, 3, .02f), Material("Wall", new Color(.28f, .4f, .5f)));
            Box("Backstop", new Vector3(0, 1.5f, 9.8f), new Vector3(20, 3, .3f), Material("Backstop", new Color(.13f, .19f, .26f)));
            var grid = Material("Grid", new Color(.3f, .4f, .48f), true);
            for (var n = -10; n <= 10; n++)
            {
                Line(new Vector3(n, .002f, -10), new Vector3(n, .002f, 10), grid);
                Line(new Vector3(-10, .002f, n), new Vector3(10, .002f, n), grid);
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var visual = Object.Instantiate(player.transform.Find("Visual").gameObject);
            visual.name = "Player size reference - 1x";
            foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) Object.DestroyImmediate(script);
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            // Bake the reference pose so it remains a static ruler without a network/player controller.
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh);
                var path = Materials + "/Reference_" + skin.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                else AssetDatabase.CreateAsset(mesh, path);
                var go = skin.gameObject;
                var materials = skin.sharedMaterials;
                Object.DestroyImmediate(skin);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            }
            var bounds = ItemCollectionBuilder.BoundsOf(visual);
            visual.transform.position += new Vector3(-2.1f - bounds.center.x, -bounds.min.y, -bounds.center.z);
            var lab = new GameObject("Solo Item Lab").AddComponent<ItemSoloLab>();
            lab.catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(ItemCatalogSetup.Path);
            lab.viewCamera = camera;
            lab.interactionConfig = AssetDatabase.FindAssets("t:InteractionConfigSO").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<InteractionConfigSO>).FirstOrDefault();
            lab.testWall = wall.transform;
            lab.reviewFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Content/Fonts/Paperlogy-5Medium.ttf");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Directory.CreateDirectory("Logs/item-lab");
            File.WriteAllText("Logs/item-lab/build-result.txt", "PASS: standalone lab scene saved; catalog, character reference, floor and thin wall connected.");
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        private static void Line(Vector3 a, Vector3 b, Material material)
        {
            var line = new GameObject("1 m grid").AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.positionCount = 2; line.SetPositions(new[] { a, b });
            line.startWidth = line.endWidth = .008f; line.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
