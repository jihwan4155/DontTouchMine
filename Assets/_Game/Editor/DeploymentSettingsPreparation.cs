using System;
using System.IO;
using Game.Backend;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Copy private deployment configuration into the player resources.</summary>
    public sealed class DeploymentSettingsPreparation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;

        public void OnPreprocessBuild(BuildReport report) => Prepare();

        public static void Prepare()
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            var local = Path.Combine(root, ".local", "settings.json");
            var destination = Path.Combine(Application.dataPath, "_Game/Backend/Resources/DeploymentSettings.json");
            var url = Environment.GetEnvironmentVariable("GAME_BACKEND_URL");
            if (string.IsNullOrWhiteSpace(url) && File.Exists(local))
                url = JsonUtility.FromJson<Settings>(File.ReadAllText(local)).backendUrl;
            if (string.IsNullOrWhiteSpace(url) && File.Exists(destination))
                url = JsonUtility.FromJson<Settings>(File.ReadAllText(destination)).backendUrl;
            if (string.IsNullOrWhiteSpace(url))
                url = BackendEndpoint.DefaultBackendUrl;
            try { url = BackendEndpoint.NormalizeBaseUrl(url); }
            catch (ArgumentException failure)
            {
                throw new BuildFailedException("Set GAME_BACKEND_URL or backendUrl in .local/settings.json. " + failure.Message);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var json = JsonUtility.ToJson(new Settings { backendUrl = url });
            if (!File.Exists(destination) || File.ReadAllText(destination) != json)
                File.WriteAllText(destination, json);
            AssetDatabase.ImportAsset("Assets/_Game/Backend/Resources/DeploymentSettings.json");
        }

        [Serializable]
        private sealed class Settings { public string backendUrl; }
    }
}
