using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Match;
using Game.Network.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    internal sealed class LobbyScenePresentation
    {
        private readonly Scene scene;
        private readonly GameObject[] sceneRoots;
        private readonly NetworkRunnerService runtimeNetwork;
        private readonly MatchChatView chatView;
        private NetworkRunnerService stagingNetwork;
        public bool IsHighlightStaging => highlightStaging;

        public LobbyScenePresentation(Scene scene, GameObject[] sceneRoots, NetworkRunnerService network, MatchChatView chatView)
        {
            this.scene = scene;
            this.sceneRoots = sceneRoots;
            runtimeNetwork = network;
            this.chatView = chatView;
        }

        private Renderer[] stagingRenderers = Array.Empty<Renderer>();
        private bool[] stagingRendererStates = Array.Empty<bool>();
        private Collider[] stagingColliders = Array.Empty<Collider>();
        private bool[] stagingColliderStates = Array.Empty<bool>();
        private Behaviour[] stagingBehaviours = Array.Empty<Behaviour>();
        private bool[] stagingBehaviourStates = Array.Empty<bool>();
        private Behaviour[] outgoingBehaviours = Array.Empty<Behaviour>();
        private bool[] outgoingBehaviourStates = Array.Empty<bool>();
        private Renderer[] outgoingRenderers = Array.Empty<Renderer>();
        private bool[] outgoingRendererStates = Array.Empty<bool>();
        private bool[] outgoingRendererEnabledStates = Array.Empty<bool>();
        private Collider[] outgoingColliders = Array.Empty<Collider>();
        private bool[] outgoingColliderStates = Array.Empty<bool>();
        private bool highlightStaging;
        private bool stagingVisible;
        private int diagnosticSamples;
        private float nextDiagnosticTime;

        public void Tick()
        {
            UpdateChatActivation();
            if (diagnosticSamples > 0 && Time.unscaledTime >= nextDiagnosticTime)
            {
                diagnosticSamples--;
                nextDiagnosticTime = Time.unscaledTime + 1f;
                MatchTransitionDiagnostics.Dump($"lobby-sample staging={highlightStaging} visible={stagingVisible} outgoing={outgoingRenderers.Length} highlight={stagingNetwork?.IsHighlightInProgress} localComplete={stagingNetwork?.IsLocalHighlightComplete}");
            }
            if (!highlightStaging || stagingNetwork == null) return;
            if (!stagingNetwork.IsHighlightInProgress)
            {
                // A skipped viewer already completed this handoff. Do not scan
                // every outgoing renderer/collider again at the shared boundary.
                if (!stagingVisible) SetStagingVisible(true);
                highlightStaging = false;
                return;
            }

            var visible = stagingNetwork.IsLocalHighlightComplete;
            if (visible != stagingVisible) SetStagingVisible(visible);
            // Apply outgoing visibility once at the handoff, after replay cleanup.
        }

        private void UpdateChatActivation()
        {
            if (chatView == null)
            {
                return;
            }

            chatView.SetAllowsActivation(MatchChatView.AllowsActivationOnScreen(
                runtimeNetwork != null && runtimeNetwork.IsHighlightInProgress,
                runtimeNetwork != null && runtimeNetwork.IsLocalHighlightComplete,
                runtimeNetwork != null && runtimeNetwork.IsResultSceneLoaded));
        }

        public void PrepareHighlightStaging(NetworkRunnerService network)
        {
            stagingNetwork = network;
            highlightStaging = true;
            MatchTransitionDiagnostics.Dump("lobby-staging-prepare");
        }

        public void CaptureStagingPresentation()
        {
            var renderers = new List<Renderer>();
            var colliders = new List<Collider>();
            var behaviours = new List<Behaviour>();
            foreach (var root in sceneRoots)
            {
                if (root == null) continue;
                renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
                colliders.AddRange(root.GetComponentsInChildren<Collider>(true));
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour is Camera or Canvas or AudioListener or AudioSource or
                        EventSystem or Light or HighlightTransitionView)
                        behaviours.Add(behaviour);
                }
            }

            stagingRenderers = renderers.ToArray();
            stagingRendererStates = new bool[stagingRenderers.Length];
            for (var index = 0; index < stagingRenderers.Length; index++)
                stagingRendererStates[index] = stagingRenderers[index].forceRenderingOff;
            stagingColliders = colliders.ToArray();
            stagingColliderStates = new bool[stagingColliders.Length];
            for (var index = 0; index < stagingColliders.Length; index++)
                stagingColliderStates[index] = stagingColliders[index].enabled;
            stagingBehaviours = behaviours.ToArray();
            stagingBehaviourStates = new bool[stagingBehaviours.Length];
            for (var index = 0; index < stagingBehaviours.Length; index++)
                stagingBehaviourStates[index] = stagingBehaviours[index].enabled;

            var playground = UnityEngine.Object.FindFirstObjectByType<PlaygroundLifetimeScope>(
                FindObjectsInactive.Include);
            if (playground == null) return;
            var outgoing = new List<Behaviour>();
            var outgoingMeshes = new List<Renderer>();
            var outgoingBodies = new List<Collider>();
            foreach (var root in playground.SceneRoots)
            {
                // Assigned items are roots too, and may have been destroyed during the match.
                // The gameplay rig is transferred to Lobby but remains in the old root snapshot.
                if (root == null || root.GetComponent<PlayerCameraController>() != null ||
                    (root.GetComponent<Camera>() != null && root.scene == scene)) continue;
                outgoingMeshes.AddRange(root.GetComponentsInChildren<Renderer>(true));
                outgoingBodies.AddRange(root.GetComponentsInChildren<Collider>(true));
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour is Camera or Canvas or AudioListener or AudioSource or
                        UnityEngine.Video.VideoPlayer or EventSystem or Light)
                        outgoing.Add(behaviour);
                }
            }
            outgoingBehaviours = outgoing.ToArray();
            outgoingBehaviourStates = new bool[outgoingBehaviours.Length];
            for (var index = 0; index < outgoingBehaviours.Length; index++)
                outgoingBehaviourStates[index] = outgoingBehaviours[index].enabled;
            outgoingRenderers = outgoingMeshes.ToArray();
            outgoingRendererStates = new bool[outgoingRenderers.Length];
            outgoingRendererEnabledStates = new bool[outgoingRenderers.Length];
            for (var index = 0; index < outgoingRenderers.Length; index++)
            {
                outgoingRendererStates[index] = outgoingRenderers[index].forceRenderingOff;
                outgoingRendererEnabledStates[index] = outgoingRenderers[index].enabled;
            }
            outgoingColliders = outgoingBodies.ToArray();
            outgoingColliderStates = new bool[outgoingColliders.Length];
            for (var index = 0; index < outgoingColliders.Length; index++)
                outgoingColliderStates[index] = outgoingColliders[index].enabled;
        }

        public void HideOutgoingGeometry()
        {
            for (var index = 0; index < outgoingRenderers.Length; index++)
                if (outgoingRenderers[index] != null)
                {
                    outgoingRenderers[index].forceRenderingOff = true;
                    outgoingRenderers[index].enabled = false;
                }
            for (var index = 0; index < outgoingColliders.Length; index++)
                if (outgoingColliders[index] != null)
                    outgoingColliders[index].enabled = false;
        }

        public void SetStagingVisible(bool visible)
        {
            if (visible && !stagingVisible)
            {
                diagnosticSamples = 3;
                nextDiagnosticTime = Time.unscaledTime;
            }
            stagingVisible = visible;
            for (var index = 0; index < stagingRenderers.Length; index++)
                if (stagingRenderers[index] != null)
                    stagingRenderers[index].forceRenderingOff =
                        !visible || stagingRendererStates[index];
            for (var index = 0; index < stagingColliders.Length; index++)
                if (stagingColliders[index] != null)
                    stagingColliders[index].enabled =
                        visible && stagingColliderStates[index];
            for (var index = 0; index < stagingBehaviours.Length; index++)
                if (stagingBehaviours[index] != null)
                    stagingBehaviours[index].enabled =
                        visible && stagingBehaviourStates[index];
            for (var index = 0; index < outgoingBehaviours.Length; index++)
                if (outgoingBehaviours[index] != null)
                    outgoingBehaviours[index].enabled =
                        !visible && outgoingBehaviourStates[index];
            if (visible && scene.isLoaded)
            {
                // The outgoing scene can stay loaded until every peer finishes.
                // Hide its geometry and collisions before revealing the lobby.
                HideOutgoingGeometry();
                // This peer has finished viewing; other clients keep their own map intact.
                UnityEngine.Object.FindFirstObjectByType<PlaygroundLifetimeScope>(FindObjectsInactive.Include)
                    ?.BeginRetiredMapCleanup();
                SceneManager.SetActiveScene(scene);
                foreach (var cover in UnityEngine.Object.FindObjectsByType<HighlightTransitionView>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                    if (cover.gameObject.name == "Highlight Transition")
                        cover.SetOpacity(0f);
            }
            else if (!visible)
            {
                for (var index = 0; index < outgoingRenderers.Length; index++)
                    if (outgoingRenderers[index] != null)
                    {
                        outgoingRenderers[index].forceRenderingOff = outgoingRendererStates[index];
                        outgoingRenderers[index].enabled = outgoingRendererEnabledStates[index];
                    }
                for (var index = 0; index < outgoingColliders.Length; index++)
                    if (outgoingColliders[index] != null)
                        outgoingColliders[index].enabled = outgoingColliderStates[index];
            }
        }

    }
}
