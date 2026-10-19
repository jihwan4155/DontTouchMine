using System;
using System.IO;
using Game.Backend;
using Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class DeploymentSettingsTests
    {
        [Serializable] private sealed class Settings { public string backendUrl; }

        [Test]
        public void PreparedResourcePreservesPrivateDeploymentAddress()
        {
            var previous = Environment.GetEnvironmentVariable("GAME_BACKEND_URL");
            try
            {
                Environment.SetEnvironmentVariable("GAME_BACKEND_URL", null);
                var local = Path.Combine(Path.GetDirectoryName(Application.dataPath), ".local/settings.json");
                if (!File.Exists(local)) Assert.Ignore("Private deployment settings are not installed on this machine.");
                DeploymentSettingsPreparation.Prepare();
                var expected = JsonUtility.FromJson<Settings>(File.ReadAllText(local)).backendUrl;
                Assert.That(new BackendEndpoint().BaseUrl, Is.EqualTo(expected.TrimEnd('/')));
            }
            finally { Environment.SetEnvironmentVariable("GAME_BACKEND_URL", previous); }
        }

        [Test]
        public void EditorSettingsTakePrecedenceOverLastBuildResources()
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            var local = Path.Combine(root, ".local/settings.json");
            var resource = Path.Combine(Application.dataPath, "_Game/Backend/Resources/DeploymentSettings.json");
            if (!File.Exists(local) || !File.Exists(resource)) Assert.Ignore("Private deployment settings are not installed on this machine.");
            var expected = JsonUtility.FromJson<Settings>(File.ReadAllText(local)).backendUrl;
            var original = File.ReadAllBytes(resource);
            var previous = Environment.GetEnvironmentVariable("GAME_BACKEND_URL");
            try
            {
                Environment.SetEnvironmentVariable("GAME_BACKEND_URL", null);
                File.WriteAllText(resource, "{\"backendUrl\":\"https://last-build.example.test\"}");
                AssetDatabase.ImportAsset("Assets/_Game/Backend/Resources/DeploymentSettings.json", ImportAssetOptions.ForceUpdate);
                Assert.That(new BackendEndpoint().BaseUrl, Is.EqualTo(expected.TrimEnd('/')));
            }
            finally
            {
                File.WriteAllBytes(resource, original);
                AssetDatabase.ImportAsset("Assets/_Game/Backend/Resources/DeploymentSettings.json", ImportAssetOptions.ForceUpdate);
                Environment.SetEnvironmentVariable("GAME_BACKEND_URL", previous);
            }
        }

        [Test]
        public void ExplicitEndpointAndEnvironmentOverrideKeepUrlSemantics()
        {
            var previous = Environment.GetEnvironmentVariable("GAME_BACKEND_URL");
            try
            {
                Environment.SetEnvironmentVariable("GAME_BACKEND_URL", "https://backend.example.test/");
                Assert.That(new BackendEndpoint().Url("api/v1/accounts"), Is.EqualTo("https://backend.example.test/api/v1/accounts"));
                Assert.That(new BackendEndpoint(" http://localhost:8080/ ", 15).Url("/api/v1/accounts"), Is.EqualTo("http://localhost:8080/api/v1/accounts"));
                Assert.That(new BackendEndpoint("http://localhost", 15).TimeoutSeconds, Is.EqualTo(15));
            }
            finally { Environment.SetEnvironmentVariable("GAME_BACKEND_URL", previous); }
        }

        [Test]
        public void SharedDefaultIsAnOriginThatNeedsNoSetup()
        {
            Assert.That(BackendEndpoint.NormalizeBaseUrl(BackendEndpoint.DefaultBackendUrl),
                Is.EqualTo(BackendEndpoint.DefaultBackendUrl));
        }

        [Test]
        public void WithoutAnySettingTheSharedDefaultIsUsed()
        {
            var local = Path.Combine(Path.GetDirectoryName(Application.dataPath), ".local/settings.json");
            if (File.Exists(local) || Resources.Load<TextAsset>("DeploymentSettings") != null)
                Assert.Ignore("This machine has its own deployment settings.");
            var previous = Environment.GetEnvironmentVariable("GAME_BACKEND_URL");
            try
            {
                Environment.SetEnvironmentVariable("GAME_BACKEND_URL", null);
                Assert.That(new BackendEndpoint().BaseUrl, Is.EqualTo(BackendEndpoint.DefaultBackendUrl));
            }
            finally { Environment.SetEnvironmentVariable("GAME_BACKEND_URL", previous); }
        }

        [TestCase(" https://backend.example.test/ ", "https://backend.example.test")]
        [TestCase("http://localhost:8080", "http://localhost:8080")]
        public void BackendOriginIsNormalized(string value, string expected)
        {
            Assert.That(BackendEndpoint.NormalizeBaseUrl(value), Is.EqualTo(expected));
            Assert.That(new BackendEndpoint(value).BaseUrl, Is.EqualTo(expected));
        }

        [TestCase("ftp://backend.example.test")]
        [TestCase("https://user:password@backend.example.test")]
        [TestCase("https://backend.example.test/api")]
        [TestCase("https://backend.example.test?key=value")]
        [TestCase("https://backend.example.test#section")]
        [TestCase("not-a-url")]
        [TestCase("https://backend.example.test:99999")]
        public void InvalidBackendOriginIsRejected(string value)
        {
            Assert.Throws<ArgumentException>(() => new BackendEndpoint(value));
        }
    }
}
