using System;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LobbyMatchInfoPresenterTests
    {
        [Test]
        public void Start_WritesCurrentSessionCategoryAndMap()
        {
            using var session = new HostSession();
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                using var presenter = new LobbyMatchInfoPresenter(session, hud);
                presenter.Start();

                var info = canvas.transform.Find(LobbyMatchInfoView.RootName)
                    .GetComponent<LobbyMatchInfoView>();
                Assert.That(info.CategoryLabel, Is.EqualTo(PlaySettingsCategoryCatalog.Default.Label));
                Assert.That(info.MapLabel, Is.EqualTo(PlaySettingsMapCatalog.LabelOf("supermarket")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SettingsChange_KeepsSelectedMapLabelAndMapsEmptyToRandom()
        {
            using var session = new HostSession();
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                using var presenter = new LobbyMatchInfoPresenter(session, hud);
                presenter.Start();

                Assert.That(
                    MatchRuleSettings.TryCreate(30, 5, 1f, 3, "fruit", out var fruit, out _),
                    Is.True);
                session.ReplaceSettings(new PlaySettingsDraft(
                    "방",
                    "CODE",
                    false,
                    null,
                    6,
                    3,
                    "supermarket",
                    fruit));

                var info = canvas.transform.Find(LobbyMatchInfoView.RootName)
                    .GetComponent<LobbyMatchInfoView>();
                Assert.That(info.CategoryLabel, Is.EqualTo(PlaySettingsCategoryCatalog.Default.Label));
                Assert.That(info.MapLabel, Is.EqualTo(PlaySettingsMapCatalog.LabelOf("supermarket")));

                session.ReplaceSettings(new PlaySettingsDraft(
                    "방",
                    "CODE",
                    false,
                    null,
                    6,
                    3,
                    string.Empty,
                    MatchRuleSettings.Default));
                Assert.That(info.CategoryLabel, Is.EqualTo("랜덤"));
                Assert.That(info.MapLabel, Is.EqualTo(PlaySettingsMapCatalog.RandomLabel));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Dispose_StopsUpdatingTheViewWhenRoomSettingsChange()
        {
            using var session = new HostSession();
            var view = new RecordingView();
            var presenter = new LobbyMatchInfoPresenter(session, view);
            presenter.Start();
            Assert.That(view.Updates, Is.EqualTo(1));
            presenter.Dispose();
            session.ReplaceSettings(new PlaySettingsDraft("other", "NEXT", false, null, 4, 2, "mansion"));
            Assert.That(view.Updates, Is.EqualTo(1));
        }

        private sealed class RecordingView : ILobbyMatchInfoView
        {
            public int Updates { get; private set; }
            public void ShowChrome(Game.Core.Settings.UiLocale locale) { }
            public void SetMatchInfo(string categoryLabel, string mapLabel, Sprite preview, bool randomMap) => Updates++;
        }

        private sealed class HostSession : ILobbyHostSession, IDisposable
        {
            private readonly ReactiveProperty<bool> host = new(false);
            private readonly ReactiveProperty<PlaySettingsDraft> settings = new(
                new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "supermarket"));

            public string LocalPlayerId => "local";
            public ReadOnlyReactiveProperty<bool> IsLocalHost => host;
            public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;
            public event Action StartRequested { add { } remove { } }
            public event Action<string> KickRequested { add { } remove { } }
            public event Action<string> HostTransferRequested { add { } remove { } }
            public event Action<PlaySettingsDraft> SettingsApplyRequested { add { } remove { } }
            public void SetLocalHost(bool value) => host.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart() { }
            public void RequestKick(string id) { }
            public void RequestHostTransfer(string id) { }
            public void RequestApplySettings(PlaySettingsDraft value) => settings.Value = value;
            public void Dispose()
            {
                host.Dispose();
                settings.Dispose();
            }
        }
    }
}
