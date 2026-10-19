using System;
using Game.Core.Lobby;
using Game.Core.Maps;
using Game.Core.Settings;
using R3;
using VContainer.Unity;

namespace Game.Client.Lobby
{
    public interface ILobbyMatchInfoView
    {
        void ShowChrome(UiLocale locale);
        void SetMatchInfo(string categoryLabel, string mapLabel, UnityEngine.Sprite mapPreview, bool randomMap);
    }

    /// <summary>
    /// Keeps the lobby's always-on category/map card in sync with room settings.
    /// </summary>
    public sealed class LobbyMatchInfoPresenter : IStartable, IDisposable
    {
        private readonly ILobbyHostSession hostSession;
        private readonly ILobbyMatchInfoView hud;
        private readonly UiLocale locale;
        private IDisposable settingsSubscription;

        public LobbyMatchInfoPresenter(ILobbyHostSession hostSession, ILobbyMatchInfoView hud)
            : this(hostSession, hud, null)
        {
        }

        [VContainer.Inject]
        public LobbyMatchInfoPresenter(
            ILobbyHostSession hostSession, ILobbyMatchInfoView hud, UiLocale locale = null)
        {
            this.hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.locale = locale;
        }

        public void Start()
        {
            if (locale != null)
            {
                locale.Changed += OnLocaleChanged;
            }

            hud.ShowChrome(locale);
            settingsSubscription = hostSession.Settings.Subscribe(Apply);
        }

        public void Dispose()
        {
            if (locale != null)
            {
                locale.Changed -= OnLocaleChanged;
            }

            settingsSubscription?.Dispose();
        }

        private void OnLocaleChanged()
        {
            hud.ShowChrome(locale);
            Apply(hostSession.Settings.CurrentValue);
        }

        private void Apply(PlaySettingsDraft draft)
        {
            hud.SetMatchInfo(
                CategoryLabel(draft),
                MapLabel(draft),
                MapPreviewSprites.For(draft.MapId),
                MapCatalog.IsRandom(draft.MapId));
        }

        private string Language =>
            locale != null ? locale.LanguageCode : UiLocale.AppliedLanguage;

        private string CategoryLabel(PlaySettingsDraft draft)
        {
            var index = PlaySettingsCategoryCatalog.IndexOf(draft.MatchRules.CategoryId);
            var option = PlaySettingsCategoryCatalog.GetOption(
                index < 0 ? PlaySettingsCategoryCatalog.DefaultIndex : index);
            return PlaySettingsCategoryCatalog.LabelOf(option.Id, Language);
        }

        private string MapLabel(PlaySettingsDraft draft) =>
            PlaySettingsMapCatalog.LabelOf(draft.MapId, Language);
    }
}
