using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Client.Common;
using Game.Client.Home;
using Game.Client.Rooms;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode
{
    public sealed class RoomScreenPresenterTests
    {
        [Test]
        public void OpenRoom_ForwardsTheSameRoomWithoutAPassword_AndUnsubscribesOnDispose()
        {
            using var rooms = new RoomBrowserSystem();
            rooms.SetRooms(new[] { Room(false) });
            var view = new BrowserView();
            var host = new Host();
            var presenter = new RoomScreenPresenter(view, rooms, host, new AppFlowSystem(), null, null, CancellationToken.None);
            var calls = 0;
            presenter.RoomJoinRequested += (id, password) =>
            {
                calls++;
                Assert.That(id, Is.EqualTo(new RoomId("AB12CD")));
                Assert.That(password, Is.Null);
            };
            view.Select("AB12CD");
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(host.LobbyCalls, Is.Zero, "A request alone must not navigate.");
            presenter.Dispose();
            view.Select("AB12CD");
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void LockedRoom_OpensTheSameModal_AndForwardsTheEnteredPassword()
        {
            using var rooms = new RoomBrowserSystem();
            rooms.SetRooms(new[] { Room(true) });
            var view = new BrowserView();
            var modal = new PasswordView();
            using var presenter = new RoomScreenPresenter(view, rooms, new Host(), new AppFlowSystem(), null, modal, CancellationToken.None);
            string submitted = null;
            presenter.RoomJoinRequested += (id, password) => submitted = password;
            view.Select("AB12CD");
            Assert.That(modal.Title, Is.EqualTo("Same room"));
            Assert.That(submitted, Is.Null);
            modal.Submit("1234");
            Assert.That(submitted, Is.EqualTo("1234"));
            Assert.That(modal.Busy, Is.True);
        }

        [Test]
        public void CodeEntry_PreservesNormalization_AndDoesNotRequireAListedRoom()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new BrowserView();
            using var presenter = new RoomScreenPresenter(view, rooms, new Host(), new AppFlowSystem(), null, null, CancellationToken.None);
            string requested = null;
            presenter.RoomCodeEntryRequested += code => requested = code;
            view.EnterCode("invalid");
            Assert.That(requested, Is.Null);
            view.EnterCode(" ab12cd ");
            Assert.That(requested, Is.EqualTo("AB12CD"));
        }

        [Test]
        public void Scene_KeepsTheExistingBindingGuidAndModalReferences()
        {
            Assert.That(typeof(RoomScreenPresenter).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/_Game/Bootstrap/RoomScreenBindings.cs"), Is.EqualTo("f1fbfa7b7b954ae7b898545f1fe3ebbe"));
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Room.unity", OpenSceneMode.Additive);
            try
            {
                RoomScreenBindings bindings = null;
                foreach (var root in scene.GetRootGameObjects())
                    bindings = bindings != null ? bindings : root.GetComponentInChildren<RoomScreenBindings>(true);
                Assert.That(bindings, Is.Not.Null);
                Assert.That(bindings.PasswordModalPrefab, Is.Not.Null);
                Assert.That(bindings.ModalParent, Is.Not.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(bindings.PasswordModalPrefab)), Is.EqualTo("e166a86f55e146dd837f4bdd27f610f9"));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static RoomSummary Room(bool locked) => new RoomSummary(new RoomId("AB12CD"), "Same room", "supermarket", 1, 6, locked, true);

        private sealed class PasswordView : IRoomPasswordModalView
        {
            public event Action CloseRequested;
            public event Action<string> SubmitRequested;
            public string Title;
            public bool Busy;
            public void Open(string title) => Title = title;
            public void Close() { }
            public void SetBusy(bool busy) => Busy = busy;
            public void ShowFailure(RoomEntryFailure failure) { }
            public void Submit(string password) => SubmitRequested?.Invoke(password);
        }

        private sealed class BrowserView : IRoomBrowserView
        {
            public event Action<string> SearchTextChanged;
            public event Action RefreshRequested;
            public event Action<string> RoomCodeEntered;
            public event Action BackRequested;
            public event Action<string> RoomSelected;
            public event Action DisconnectionAcknowledged;
            public void SetRooms(IReadOnlyList<RoomSummary> rooms) { }
            public void SetBusy(bool busy) { }
            public void SetEmptyMessage(string message) { }
            public void ShowEntryFailure(RoomEntryFailure failure) { }
            public void ShowDisconnection(string message) { }
            public void ShowChrome(UiLocale locale) { }
            public void Select(string id) => RoomSelected?.Invoke(id);
            public void EnterCode(string code) => RoomCodeEntered?.Invoke(code);
        }

        private sealed class Host : IHomeApplicationHost
        {
            public int LobbyCalls;
            public void Quit() { }
            public void OpenHome() { }
            public void OpenRoomBrowser() { }
            public void OpenCharacterCloset() { }
            public void OpenSettings() { }
            public void OpenTutorial() { }
            public void CreateRoom(string title, bool isPublic, int maxPlayers) { }
            public void JoinRoom(string roomCode) { }
            public void OpenLobby() => LobbyCalls++;
        }
    }
}
