using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Editor
{
    public static class StunDropTestSetup
    {
        public const string ScenePath = "Assets/_Game/Tests/PlayMode/StunDropCaptureScene.unity";
        public static void Prepare()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STUN_DROP_CAPTURE_DIR")))
                throw new InvalidOperationException("STUN_DROP_CAPTURE_DIR is required.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
