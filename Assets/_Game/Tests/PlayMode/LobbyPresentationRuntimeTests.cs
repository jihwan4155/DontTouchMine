using System.Collections;
using Game.Bootstrap;
using Game.Client.Lobby;
using Game.Core.Lobby;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Game.Tests.PlayMode
{
    public sealed class LobbyPresentationRuntimeTests
    {
        [UnityTest]
        public IEnumerator RealLobbyScope_UpdatesTheMapCardThroughTheRegisteredPresenter()
        {
            var load = SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;

            var scope = Object.FindAnyObjectByType<LobbyLifetimeScope>();
            Assert.That(scope, Is.Not.Null);
            var hud = scope.Container.Resolve<LobbyHudView>();
            Assert.That(scope.Container.Resolve<ILobbyMatchInfoView>(), Is.SameAs(hud));
            var session = scope.Container.Resolve<ILobbyHostSession>();
            var locale = scope.Container.Resolve<UiLocale>();
            var card = hud.GetComponentInChildren<LobbyMatchInfoView>(true);
            Assert.That(card, Is.Not.Null);

            session.ReplaceSettings(new PlaySettingsDraft("runtime", "TEST", false, null, 6, 3, "supermarket"));
            Assert.That(card.MapLabel, Is.EqualTo(PlaySettingsMapCatalog.LabelOf("supermarket", locale.LanguageCode)));
            session.ReplaceSettings(new PlaySettingsDraft("runtime", "TEST", false, null, 6, 3, "mansion"));
            Assert.That(card.MapLabel, Is.EqualTo(PlaySettingsMapCatalog.LabelOf("mansion", locale.LanguageCode)));
            session.ReplaceSettings(new PlaySettingsDraft("runtime", "TEST", false, null, 6, 3, string.Empty));
            Assert.That(card.MapLabel, Is.EqualTo(PlaySettingsMapCatalog.LabelOf(string.Empty, locale.LanguageCode)));
        }
    }
}
