#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Fusion;
using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Core.Items;
using Game.Core.Match;
using Game.Network.Match;
using Game.Network.Players;
using Game.Server.Items;
using Game.Server.Match;
using Game.Server.Players;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Game.Tests.PlayMode
{
    // Opt-in live Photon test. Captures an isolated arena, not a production match/map.
    public sealed class StunItemDropMultiplayerTests
    {
        NetworkRunner host, client;
        MatchState domainState;
        MatchRulesSO rules;
        readonly List<GameObject> scenery = new();
        readonly List<Material> materials = new();
        string output;
        NetworkProjectConfig.PeerModes originalPeerMode;
        [Serializable] sealed class Account { public string userId, photonToken; }
        [Serializable] sealed class Device { public string deviceId; }
        sealed class Placement : IPlacementValidator { public bool IsValid(string id, Pose pose) => true; }

        [UnityTest]
        public IEnumerator TwoConnectedPlayers_StunDropsItemAndPeerSeesLanding()
        {
            output = Environment.GetEnvironmentVariable("STUN_DROP_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output)) Assert.Ignore("Set STUN_DROP_CAPTURE_DIR to run the live two-player capture.");
            Directory.CreateDirectory(output);
            originalPeerMode = NetworkProjectConfig.Global.PeerMode;
            var room = "stun-drop-qa-" + Guid.NewGuid().ToString("N");
            host = NewRunner("Stun drop host");
            client = NewRunner("Stun drop client");
            yield return Connect(host, GameMode.Host, room, "host");
            yield return Connect(client, GameMode.Client, room, "client");
            var deadline = Time.realtimeSinceStartup + 20f;
            while (host.ActivePlayers.Count() != 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(host.ActivePlayers.Count(), Is.EqualTo(2), "Both real peers must be connected.");

            var refs = new[] { host.LocalPlayer, client.LocalPlayer };
            var poses = new[] { new Pose(new Vector3(-1.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)), new Pose(Vector3.zero, Quaternion.identity) };
            var ids = refs.Select(PlayerRegistry.IdOf).ToArray();
            var roster = host.gameObject.GetComponent<PlayerRoster>();
            var starter = host.gameObject.GetComponent<MatchStarter>();
            starter.Bind(null, roster, null);
            var networkState = host.Spawn(AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/_Game/Content/Prefabs/MatchSession.prefab")).GetComponent<MatchSessionState>();
            networkState.Confirm(ids);
            starter.Publish(networkState);
            rules = ScriptableObject.CreateInstance<MatchRulesSO>();
            typeof(MatchRulesSO).GetField("hidingTurnDurationSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rules, .1f);
            domainState = new MatchState();
            var session = new MatchSessionCoordinator(rules, domainState,
                new MatchFlow(rules, domainState, 2), new PlayerInteractionSystem(rules, 2), ids,
                new Placement(), poses, new[] { new ItemDefinition("qa-one", "test"), new ItemDefinition("qa-two", "test") },
                new System.Random(1), new[] { new WorldObjectState("qa-box", new Pose(Vector3.up, Quaternion.identity)) });
            starter.BindSession(session, new[] { Pose.identity });
            var now = host.SimulationTime;
            session.Start(0d);
            session.AdvanceTime(now, poses.Select(p => p.position).ToArray());
            Assert.That(session.CurrentPhase, Is.EqualTo(MatchPhase.Searching));
            networkState.TrySetSnapshot(new MatchStateSnapshot(MatchPhase.Searching, now + 120d));
            Assert.That(networkState.TryResetWorldObjects(new[] { new WorldObjectState("qa-box", new Pose(Vector3.up, Quaternion.identity)) }), Is.True);

            var floor = Primitive("Floor", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 10f), new Color(.16f, .2f, .25f));
            host.MoveToRunnerScene(floor);
            var light = new GameObject("Capture light"); scenery.Add(light);
            var illumination = light.AddComponent<Light>(); illumination.type = LightType.Directional; illumination.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var players = new PlayerAvatar[2];
            for (var i = 0; i < 2; i++)
            {
                var seat = i;
                players[i] = host.Spawn(AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab"),
                    poses[i].position, poses[i].rotation, refs[i], (_, obj) => obj.GetComponent<PlayerAvatar>().Seat = seat).GetComponent<PlayerAvatar>();
                players[i].GetComponent<PlayerInteractor>().enabled = false;
                players[i].GetComponent<PlayerCombatant>().ConfigureNetworkPlayer(i, false);
                foreach (var camera in players[i].GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var renderer in players[i].GetComponentsInChildren<Renderer>()) renderer.material.color = i == 0 ? new Color(.3f, .65f, 1f) : new Color(1f, .55f, .45f);
            }
            yield return new WaitForSeconds(.7f);
            var box = Primitive("Dropped item", PrimitiveType.Cube, new Vector3(0f, 1.3f, .7f), Vector3.one * .3f, new Color(1f, .8f, .15f));
            host.MoveToRunnerScene(box);
            var item = box.AddComponent<CarryableItem>();
            item.OnPickedUp(players[1].GetComponent<PlayerInteractor>().HoldPoint);
            Assert.That(session.TryHoldObject(1, "qa-box", host.SimulationTime), Is.True);
            Assert.That(networkState.TrySetObjectHeld("qa-box", 1), Is.True);
            var views = new MatchObjectStateSnapshot[0];
            client.gameObject.GetComponent<MatchStarter>().ObjectStatesReceived += snapshots => views = snapshots.ToArray();
            var cameraObject = new GameObject("Stun drop observer"); scenery.Add(cameraObject);
            var observer = cameraObject.AddComponent<Camera>(); observer.enabled = false;
            observer.transform.position = new Vector3(4f, 3f, -5f);
            observer.transform.LookAt(new Vector3(.4f, .7f, 0f)); observer.fieldOfView = 48f;
            observer.clearFlags = CameraClearFlags.SolidColor; observer.backgroundColor = new Color(.07f, .09f, .14f);
            HideClientCopies();
            Capture(observer, "00-held");
            for (var hit = 0; hit < 3; hit++)
            {
                Assert.That(starter.TryHitPlayer(host.LocalPlayer, 1), Is.True, "Production hit authority must accept the hit.");
                if (hit < 2) yield return new WaitForSeconds(.15f);
            }
            Assert.That(session.IsPlayerStunned(1, host.SimulationTime), Is.True);
            Assert.That(session.TryGetHeldObjectId(1, out _), Is.False);
            var snapshotsMethod = typeof(MatchSessionState).GetMethod("PublishObjectStates", BindingFlags.Instance | BindingFlags.NonPublic);
            MatchObjectStateSnapshot released = default;
            starter.ObjectStatesReceived += snapshots => released = snapshots.Single(s => s.ObjectId == "qa-box");
            snapshotsMethod.Invoke(networkState, null);
            Assert.That(released.IsPhysicsActive && released.HolderPlayerIndex == -1, Is.True);
            Assert.That(released.InitialVelocity.x, Is.GreaterThan(1f));
            item.OnReleased(released.Pose, released.InitialVelocity);
            players[1].GetComponent<PlayerCombatant>().SetNetworkHitCount(0);
            players[1].GetComponent<PlayerCombatant>().SetNetworkStunned(true);
            starter.TrySetPlayerControls(1, false);
            var body = item.GetComponent<Rigidbody>();
            var samples = new List<string> { "time,x,y,z" };
            var start = Time.time;
            var captureIndex = 1;
            var nextCapture = .04f;
            while (Time.time - start < 2.4f)
            {
                yield return new WaitForFixedUpdate();
                HideClientCopies();
                var elapsed = Time.time - start;
                var pose = new Pose(item.transform.position, item.transform.rotation);
                Assert.That(starter.TryConfirmObjectPhysicsPose("qa-box", pose, body.linearVelocity, true, released.Version), Is.True);
                snapshotsMethod.Invoke(networkState, null);
                samples.Add(FormattableString.Invariant($"{elapsed:F3},{pose.position.x:F3},{pose.position.y:F3},{pose.position.z:F3}"));
                if (elapsed >= nextCapture)
                {
                    Capture(observer, $"{captureIndex++:00}-t{elapsed:F2}");
                    nextCapture += .04f;
                }
            }
            File.WriteAllLines(Path.Combine(output, "trajectory.csv"), samples);
            Assert.That(item.transform.position.x, Is.GreaterThan(1.2f), "The landed item must clear the stunned body.");
            Assert.That(item.transform.position.y, Is.LessThan(.4f));
            deadline = Time.realtimeSinceStartup + 5f;
            while ((!views.Any(s => s.ObjectId == "qa-box") || Vector3.Distance(views.Single(s => s.ObjectId == "qa-box").Pose.position, item.transform.position) > .2f) && Time.realtimeSinceStartup < deadline) yield return null;
            var peer = views.Single(s => s.ObjectId == "qa-box");
            Assert.That(peer.HolderPlayerIndex, Is.EqualTo(-1));
            Assert.That(Vector3.Distance(peer.Pose.position, item.transform.position), Is.LessThan(.2f), "Real client sees the authoritative landing.");
            var teleport = typeof(NetworkPlayerMotor).GetMethod("TryTeleport", BindingFlags.Instance | BindingFlags.NonPublic);
            var landing = item.transform.position;
            Assert.That((bool)teleport.Invoke(players[0].GetComponent<NetworkPlayerMotor>(),
                new object[] { new Pose(item.transform.position + Vector3.back, Quaternion.identity) }), Is.True);
            deadline = Time.realtimeSinceStartup + 5f;
            while ((!roster.TryGetPose(ids[0], out var pickupPose) || Vector3.Distance(pickupPose.position, landing) > 1.5f) && Time.realtimeSinceStartup < deadline)
                yield return null; // Teleports apply on Fusion's next forward tick, after Unity's FixedUpdate.
            Assert.That(starter.TryHoldObject(host.LocalPlayer, "qa-box"), Is.True, "Another real player can pick up through the production distance check.");
            item.OnPickedUp(players[0].GetComponent<PlayerInteractor>().HoldPoint);
            yield return new WaitForSeconds(.15f);
            Capture(observer, "61-picked-up");
            Assert.That(views.Single(s => s.ObjectId == "qa-box").HolderPlayerIndex, Is.EqualTo(0));
            File.WriteAllText(Path.Combine(output, "result.txt"), $"PASS: Host + Client, 2 connected players, production 3-hit stun release, real Rigidbody arc, peer landing replication, production distance-checked pickup replicated to client.\nLanding: {landing}\n");
        }

        NetworkRunner NewRunner(string name)
        {
            var runner = new GameObject(name).AddComponent<NetworkRunner>();
            runner.gameObject.AddComponent<PlayerRoster>(); runner.gameObject.AddComponent<MatchStarter>();
            runner.gameObject.AddComponent<Photon.Voice.Unity.VoiceConnection>().enabled = false;
            return runner;
        }

        IEnumerator Connect(NetworkRunner runner, GameMode mode, string room, string role)
        {
            using var request = UnityWebRequest.Post(Game.Backend.BackendEndpoint.Deployed + "/api/v1/accounts",
                JsonUtility.ToJson(new Device { deviceId = "stun-drop-qa-" + role }), "application/json");
            request.timeout = 20;
            yield return request.SendWebRequest();
            Assert.That(request.result, Is.EqualTo(UnityWebRequest.Result.Success), "QA account sign-in");
            var account = JsonUtility.FromJson<Account>(request.downloadHandler.text);
            var auth = new Photon.Realtime.AuthenticationValues { AuthType = Photon.Realtime.CustomAuthenticationType.Custom, UserId = account.userId };
            auth.AddAuthParameter("userId", account.userId); auth.AddAuthParameter("token", account.photonToken);
            var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.FixedRegion = "kr"; settings.AppVersion = "stun-drop-isolated-qa-v1";
            var config = NetworkProjectConfig.Global;
            config.PeerMode = NetworkProjectConfig.PeerModes.Multiple;
            var sceneIndex = Array.FindIndex(EditorBuildSettings.scenes, s => s.path == "Assets/_Game/Tests/PlayMode/StunDropCaptureScene.unity");
            Assert.That(sceneIndex, Is.GreaterThanOrEqualTo(0), "Run Game.Editor.StunDropTestSetup.Prepare before this test.");
            var start = runner.StartGame(new StartGameArgs { GameMode = mode, SessionName = room, PlayerCount = 2,
                IsVisible = false, CustomPhotonAppSettings = settings, AuthValues = auth, Config = config,
                Scene = SceneRef.FromIndex(sceneIndex), SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>() });
            var deadline = Time.realtimeSinceStartup + 40f;
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(start.IsCompleted && start.Result.Ok, Is.True, "Real Photon peer must connect.");
            deadline = Time.realtimeSinceStartup + 20f;
            while (runner.SceneManager.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(runner.SceneManager.IsBusy, Is.False, "The peer scene must finish loading before spawning.");
        }

        void HideClientCopies()
        {
            foreach (var avatar in Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
                if (avatar.Runner == client)
                {
                    foreach (var renderer in avatar.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    foreach (var collider in avatar.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    foreach (var camera in avatar.GetComponentsInChildren<Camera>()) camera.enabled = false;
                    avatar.GetComponent<PlayerInteractor>().enabled = false;
                }
        }

        GameObject Primitive(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(shape); obj.name = name; obj.transform.position = position; obj.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            obj.GetComponent<Renderer>().sharedMaterial = material; materials.Add(material); scenery.Add(obj); return obj;
        }

        void Capture(Camera camera, string name)
        {
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (string.IsNullOrEmpty(output)) yield break;
            foreach (var runner in new[] { client, host })
                if (runner != null) { var stop = runner.Shutdown(); while (!stop.IsCompleted) yield return null; }
            NetworkProjectConfig.Global.PeerMode = originalPeerMode;
            foreach (var obj in scenery) if (obj != null) Object.Destroy(obj);
            foreach (var material in materials) Object.Destroy(material);
            domainState?.Dispose(); if (rules != null) Object.Destroy(rules);
            const string captureScene = "Assets/_Game/Tests/PlayMode/StunDropCaptureScene.unity";
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != captureScene).ToArray();
            AssetDatabase.DeleteAsset(captureScene);
        }
    }
}
#endif
