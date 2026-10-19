using System;
using System.Collections.Generic;
using Game.Client.Character;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Home;
using Game.Core.Lobby;
using Game.Core.Settings;
using Game.Core.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Two-column 2-key modal: the live roster on the left and a friend
    /// invite list on the right. Friend actions are chrome only for now.
    /// </summary>
    public sealed partial class LobbyPlayerListView : MonoBehaviour, ILobbyPlayerListView
    {
        public static string ParticipantsTitle =>
            UiLocale.Applied(UiText.Lobby.Participants);

        public static string FriendsTitle =>
            UiLocale.Applied(UiText.Lobby.Friends);

        public static string OnlineSectionTitle =>
            UiLocale.Applied(UiText.Lobby.Online);

        public static string WaitingSectionTitle =>
            UiLocale.Applied(UiText.Lobby.Waiting);

        public static string InGameSectionTitle =>
            UiLocale.Applied(UiText.Lobby.InGame);
        public const string OnlineItemsName = "OnlineItems";
        public const string WaitingItemsName = "WaitingItems";
        public const string InGameItemsName = "InGameItems";
        public const float ModalScale = 1.3f;
        public const float TitleFontSize = 20f * ModalScale;
        public const float SectionFontSize = 16f * ModalScale;
        public const float SectionHeight = 28f * ModalScale;
        public const float NicknameFontSize = 18f * ModalScale;
        public const float KickFontSize = 16f * ModalScale;
        public const float ReportFontSize = 18f * ModalScale;
        public const float RowHeight = 42f * ModalScale;
        public const float AvatarSize = 32f * ModalScale;
        public const float AvatarLeft = 10f * ModalScale;
        public const float MuteIconSize = 16f * ModalScale;
        public const string AvatarDimName = "Dim";
        public const string MuteIconName = "Mute";
        public const string VoiceIconName = "Voice";
        public const float NicknameLeft = 10f * ModalScale;
        public const float VoiceIconGap = 6f * ModalScale;
        public const float VoiceIconSize = 16f * ModalScale;
        public const float LeaderIconGap = 6f * ModalScale;
        public const float LeaderIconSize = 16f * ModalScale;
        public const float ActionRight = 16f * ModalScale;
        public const float AddButtonSize = 26f * ModalScale;
        public const float PanelPadding = 24f * ModalScale;
        public const float ColumnInnerPadding = 10f * ModalScale;
        public const float TitleLeftPadding = ColumnInnerPadding + AvatarLeft;
        public const float TitleHeight = 28f * ModalScale;
        public const float TitleToRows = 12f * ModalScale;
        public const float ModalWidth = 800f * ModalScale;
        public const float ModalHeight = 420f * ModalScale;
        public const float ColumnWidthRatio = 0.4f;
        public const float MatchModalWidth = ModalWidth * ColumnWidthRatio;
        public const float MatchModalHeight = ModalHeight;
        public const float MatchLeftMargin = 36f * ModalScale;
        public const string MatchRootName = "MatchParticipantList";
        public const int PanelRadius = 39;
        public const int ColumnRadius = 21;
        public const int ReportTooltipRadius = 13;
        public const float ReportTooltipGap = 8f * ModalScale;
        public const float ReportTooltipOverlap = 8f * ModalScale;
        public static string KickLabel =>
            UiLocale.Applied(UiText.Lobby.Kick);
        public static string ReportLabel =>
            UiLocale.Applied(UiText.Lobby.Report);
        public const string ReportBridgeName = "Bridge";
        public static string ReportConfirmLabel =>
            UiLocale.Applied(UiText.Lobby.Confirm);
        public const float InviteCooldownSeconds = 10f;

        public static readonly Color KickColor = new Color(177f / 255f, 177f / 255f, 177f / 255f, 1f);
        public static readonly Color SelfNicknameColor = new Color(1f, 0.54f, 0.24f, 1f);
        public static readonly Color RowHoverFill = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color ReportTooltipFill = Color.white;
        public static readonly Color ReportTooltipLabel = new Color(1f, 0f, 0f, 1f);
        public static readonly Color AvatarColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        public static readonly Color MutedAvatarDim = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color OnlineSectionColor = Color.white;
        public static readonly Color WaitingSectionColor = new Color(0.35f, 0.85f, 0.4f, 1f);
        public static readonly Color InGameSectionColor = new Color(1f, 0.28f, 0.28f, 1f);

        public static readonly Vector2 ModalSize = new Vector2(ModalWidth, ModalHeight);
        public static readonly Vector2 MatchModalSize = new Vector2(MatchModalWidth, MatchModalHeight);

        private RectTransform participantRowRoot;
        private RectTransform friendRowRoot;
        private RectTransform onlineSection;
        private RectTransform waitingSection;
        private RectTransform inGameSection;
        private RectTransform onlineItemsRoot;
        private RectTransform waitingItemsRoot;
        private RectTransform inGameItemsRoot;
        private TextMeshProUGUI participantsTitle;
        private TextMeshProUGUI friendsTitle;
        private readonly List<GameObject> participantRows = new();
        private readonly List<GameObject> friendRows = new();
        private readonly Dictionary<string, Button> inviteButtons = new();
        private readonly Dictionary<string, Image> inviteIcons = new();
        private readonly Dictionary<string, bool> inviteAllowed = new();
        private readonly Dictionary<string, float> inviteReadyAt = new();
        private Func<float> inviteClock = () => Time.unscaledTime;
        private Game.Core.Settings.InterfacePresentation presentation;
        private UiLocale chromeLocale;
        private IReadOnlyList<FriendSummary> lastFriends;
        private IReadOnlyList<LobbyParticipant> lastParticipants;
        private bool lastHost;
        private string lastLocal;
        private bool lastNamesReady = true;
        private bool matchLayout;
        private string rowLanguage;
        private bool rowMatchLayout;
        private VoicePreferences voicePreferences = new();

        [VContainer.Inject]
        public void BindVoicePreferences(VoicePreferences value)
        {
            voicePreferences = value ?? throw new ArgumentNullException(nameof(value));
            rowLanguage = null;
            SetParticipants(lastParticipants, lastHost, lastLocal, lastNamesReady);
        }

        /// <summary>
        /// Redraws the words the modal owns in <paramref name="locale"/>. Player
        /// names are what people typed and stay as they are.
        /// </summary>
        public void ShowChrome(UiLocale locale)
        {
            chromeLocale = locale;
            EnsureLayout();
            if (participantsTitle != null)
            {
                participantsTitle.text = Copy(UiText.Lobby.Participants);
            }

            if (friendsTitle != null)
            {
                friendsTitle.text = Copy(UiText.Lobby.Friends);
            }

            // The rows carry words too — 강퇴, 신고, the empty-roster line — and
            // they are only written while a row is built.
            SetParticipants(lastParticipants, lastHost, lastLocal, lastNamesReady);
            if (lastFriends != null)
            {
                SetFriends(lastFriends);
            }

            SetSectionTitle(onlineSection, UiText.Lobby.Online);
            SetSectionTitle(waitingSection, UiText.Lobby.Waiting);
            SetSectionTitle(inGameSection, UiText.Lobby.InGame);
        }

        private string Copy(string key) =>
            chromeLocale != null
                ? chromeLocale.Get(key)
                : UiLocale.Applied(key);

        private void SetSectionTitle(RectTransform section, string key)
        {
            var text = section != null ? section.GetComponent<TextMeshProUGUI>() : null;
            if (text != null)
            {
                text.text = Copy(key);
            }
        }

        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value)
        {
            UnsubscribePresentation();
            presentation = value;
            if (isActiveAndEnabled)
            {
                SubscribePresentation();
            }
        }

        private void RefreshPresentation()
        {
            if (this == null)
            {
                return;
            }

            SetParticipants(lastParticipants, lastHost, lastLocal, lastNamesReady);
        }

        private void OnEnable()
        {
            SubscribePresentation();
        }

        private void OnDisable()
        {
            UnsubscribePresentation();
        }

        private void OnDestroy()
        {
            UnsubscribePresentation();
        }

        private void SubscribePresentation()
        {
            if (presentation == null)
            {
                return;
            }

            presentation.Changed -= RefreshPresentation;
            presentation.Changed += RefreshPresentation;
        }

        private void UnsubscribePresentation()
        {
            if (presentation == null)
            {
                return;
            }

            presentation.Changed -= RefreshPresentation;
        }

        public event Action<string, string> KickClicked;
        public event Action<string, string> InviteClicked;
        public event Action<string, string> ReportClicked;

        public static string FormatReportTitle(string displayName) =>
            FormatReportTitle(displayName, UiLocale.AppliedLanguage);

        public static string FormatReportTitle(string displayName, string language) =>
            string.Format(UiTextCatalog.Shipped.Get(UiText.Lobby.ReportTitle, language), displayName);

        public string ParticipantsTitleText =>
            participantsTitle != null ? participantsTitle.text : string.Empty;

        public string FriendsTitleText =>
            friendsTitle != null ? friendsTitle.text : string.Empty;

        public bool IsMatchLayout => matchLayout;

        public static LobbyPlayerListView CreateMatchList(Transform parent)
        {
            var root = new GameObject(MatchRootName, typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var view = root.AddComponent<LobbyPlayerListView>();
            view.ConfigureForMatch();
            root.SetActive(false);
            return view;
        }

        /// <summary>
        /// In-match 2-key roster: the left column only, pinned left-centre.
        /// Kick, report and the friend invite list stay on the lobby modal.
        /// </summary>
        public void ConfigureForMatch()
        {
            matchLayout = true;
            EnsureLayout();
        }

        private void Awake()
        {
            EnsureLayout();
        }

        private void Update()
        {
            RefreshInviteCooldowns();
        }

        public void SetParticipants(
            IReadOnlyList<LobbyParticipant> participants,
            bool localIsHost,
            string localPlayerId,
            bool namesReady = true)
        {
            if (this == null)
            {
                return;
            }

            var reuse = CanReuseParticipantRows(participants, localIsHost, localPlayerId, namesReady);
            lastParticipants = participants;
            lastHost = localIsHost;
            lastLocal = localPlayerId;
            lastNamesReady = namesReady;
            EnsureLayout();
            if (reuse)
            {
                var rowIndex = 0;
                foreach (var person in participants)
                {
                    if (!CanShowParticipant(person.Id, person.Id == localPlayerId)) continue;
                    UpdateParticipantVoice(participantRows[rowIndex++].transform, person);
                }
                return;
            }
            rowMatchLayout = matchLayout;
            rowLanguage = chromeLocale != null ? chromeLocale.LanguageCode : UiLocale.AppliedLanguage;
            ClearRows(participantRows);

            if (!namesReady)
            {
                return;
            }

            if (participants == null || participants.Count == 0)
            {
                CreateInfoRow(participantRowRoot, participantRows, Copy(UiText.Lobby.NoParticipants));
                return;
            }

            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                var isSelf = string.Equals(participant.Id, localPlayerId, StringComparison.Ordinal);
                if (!CanShowParticipant(participant.Id, isSelf))
                {
                    continue;
                }

                var canKick = !matchLayout && localIsHost && !isSelf;
                var shownName = ShownParticipantName(participant.Id, participant.DisplayName);
                var row = CreateRow(
                    participantRowRoot,
                    participantRows,
                    $"Row_{participant.Id}",
                    shownName,
                    participant.IsHost,
                    canKick,
                    showAdd: false,
                    isSelf: isSelf,
                    isMuted: participant.IsMuted,
                    isTalking: participant.IsTalking,
                    showVoice: false,
                    isListening: participant.IsListening,
                    playerId: participant.Id,
                    userId: participant.UserId);
                if (!isSelf) CreatePlayerVolumeSlider(row, participant);
                var playerId = participant.Id;
                var rosterName = participant.DisplayName;
                var displayName = shownName;

                // Reporting names the backend account, kicking names the Photon
                // player. They are different identifiers with different readers,
                // and passing the Photon one to the report API is what made every
                // report 404 with nobody noticing (GAME-926).
                //
                // Anyone but yourself, host included. No account, no report: a
                // participant who joined without one has nothing the server can
                // be told about, and a blank id would only turn the 404 into a
                // 400.
                if (!isSelf && !string.IsNullOrWhiteSpace(participant.UserId))
                {
                    BindReport(row, participant.UserId, displayName, below: participant.IsHost);
                }

                var kick = row.Find("Kick")?.GetComponent<Button>();
                if (kick != null)
                {
                    kick.onClick.AddListener(() => KickClicked?.Invoke(
                        playerId,
                        ShownParticipantName(playerId, rosterName)));
                }
            }
        }

        /// <summary>
        /// Other people stay off the list until they have said whether the
        /// room may use their own name. Showing the roster name first would
        /// give away a streamer before their 스트리머 모드 설정 arrived.
        /// </summary>
        private bool CanShowParticipant(string playerId, bool isSelf) =>
            presentation == null
            || isSelf
            || presentation.HasPublishedName(playerId);

        /// <summary>
        /// Fail closed: an unpublished name is nothing, not the roster name.
        /// </summary>
        private string ShownParticipantName(string playerId, string displayName) =>
            presentation == null
                ? displayName
                : presentation.Name(playerId, displayName);

        public void SetInviteClock(Func<float> clock)
        {
            inviteClock = clock ?? (() => Time.unscaledTime);
        }

        public void RefreshInviteCooldowns()
        {
            var now = inviteClock();
            var expired = new List<string>();
            foreach (var entry in inviteReadyAt)
            {
                if (entry.Value <= now)
                {
                    expired.Add(entry.Key);
                }
            }

            for (var index = 0; index < expired.Count; index++)
            {
                inviteReadyAt.Remove(expired[index]);
                ApplyInviteButton(expired[index]);
            }
        }

        public void SetFriends(IReadOnlyList<FriendSummary> friends)
        {
            lastFriends = friends;
            EnsureLayout();
            EnsureFriendSections();
            inviteButtons.Clear();
            inviteIcons.Clear();
            inviteAllowed.Clear();
            ClearRows(friendRows);

            var online = new List<FriendSummary>();
            var waiting = new List<FriendSummary>();
            var playing = new List<FriendSummary>();
            if (friends != null)
            {
                for (var index = 0; index < friends.Count; index++)
                {
                    var friend = friends[index];
                    switch (friend.Presence)
                    {
                        case FriendPresence.Online:
                            online.Add(friend);
                            break;
                        case FriendPresence.InLobby:
                            waiting.Add(friend);
                            break;
                        case FriendPresence.InGame:
                            playing.Add(friend);
                            break;
                    }
                }
            }

            AppendFriends(onlineItemsRoot, online, canInvite: true);
            AppendFriends(waitingItemsRoot, waiting, canInvite: false);
            AppendFriends(inGameItemsRoot, playing, canInvite: false);
            SetSectionVisible(onlineSection, onlineItemsRoot, online.Count > 0);
            SetSectionVisible(waitingSection, waitingItemsRoot, waiting.Count > 0);
            SetSectionVisible(inGameSection, inGameItemsRoot, playing.Count > 0);
        }

        private void EnsureFriendSections()
        {
            if (friendRowRoot == null || onlineItemsRoot != null)
            {
                return;
            }

            onlineSection = CreateSectionTitle(
                friendRowRoot, "OnlineSection", OnlineSectionTitle, OnlineSectionColor);
            onlineItemsRoot = CreateItemGroup(friendRowRoot, OnlineItemsName);
            waitingSection = CreateSectionTitle(
                friendRowRoot, "WaitingSection", WaitingSectionTitle, WaitingSectionColor);
            waitingItemsRoot = CreateItemGroup(friendRowRoot, WaitingItemsName);
            inGameSection = CreateSectionTitle(
                friendRowRoot, "InGameSection", InGameSectionTitle, InGameSectionColor);
            inGameItemsRoot = CreateItemGroup(friendRowRoot, InGameItemsName);
        }

        private static void SetSectionVisible(
            RectTransform title, RectTransform items, bool visible)
        {
            if (title != null)
            {
                title.gameObject.SetActive(visible);
            }

            if (items != null)
            {
                items.gameObject.SetActive(visible);
            }
        }

        private static RectTransform CreateSectionTitle(
            RectTransform parent, string name, string label, Color color)
        {
            var rect = FindOrCreateRect(name, parent);
            var element = rect.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = rect.gameObject.AddComponent<LayoutElement>();
            }

            element.preferredHeight = SectionHeight;
            element.minHeight = SectionHeight;
            element.flexibleWidth = 1f;

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = label;
            text.font = HomeUiFonts.ApplyRegular();
            text.fontSize = SectionFontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return rect;
        }

        private static RectTransform CreateItemGroup(RectTransform parent, string name)
        {
            var group = FindOrCreateRect(name, parent);
            var layout = group.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;

            var fitter = group.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = group.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return group;
        }

        private void AppendFriends(
            RectTransform parent, IReadOnlyList<FriendSummary> friends, bool canInvite)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = 0; index < friends.Count; index++)
            {
                var friend = friends[index];
                var row = CreateRow(
                    parent,
                    friendRows,
                    $"Friend_{friend.PlayerId}",
                    friend.Nickname,
                    showLeader: false,
                    showKick: false,
                    showAdd: true,
                    showVoice: false,
                    playerId: friend.PlayerId);
                BindInvite(row, friend.PlayerId, friend.Nickname, canInvite);
            }
        }

        public void EnsureLayout()
        {
            if (this == null)
            {
                return;
            }

            HideLegacyChrome();
            ApplyPanelChrome();
            if (participantsTitle != null &&
                friendsTitle != null &&
                participantRowRoot != null &&
                friendRowRoot != null)
            {
                PlaceColumns();
                return;
            }

            BuildLayout();
        }

        private void HideLegacyChrome()
        {
            HideChild("Title");
            HideChild("BodyText");
            HideChild("Label");
            var leftoverRows = transform.Find("RowRoot");
            if (leftoverRows != null && leftoverRows.Find("Columns") == null)
            {
                leftoverRows.gameObject.SetActive(false);
            }
        }

        private void HideChild(string name)
        {
            var child = transform.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void ApplyPanelChrome()
        {
            var rect = (RectTransform)transform;
            if (matchLayout)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(MatchLeftMargin, 0f);
                rect.sizeDelta = MatchModalSize;
            }
            else
            {
                rect.sizeDelta = ModalSize;
            }

            var fill = GetComponent<Image>();
            if (fill == null)
            {
                fill = gameObject.AddComponent<Image>();
            }

            fill.sprite = HomeUiFonts.Rounded(matchLayout ? ColumnRadius : PanelRadius);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;
            fill.color = PlaySettingsStyle.Palette.PanelFill;
            fill.raycastTarget = true;
            ApplyMatchOutline();
        }

        private void ApplyMatchOutline()
        {
            var outline = transform.Find("Outline") as RectTransform;
            if (!matchLayout)
            {
                if (outline != null)
                {
                    outline.gameObject.SetActive(false);
                }

                return;
            }

            if (outline == null)
            {
                var go = new GameObject(
                    "Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                outline = go.GetComponent<RectTransform>();
            }

            outline.gameObject.SetActive(true);
            Stretch(outline, 0f, 0f, 0f, 0f);
            outline.SetAsFirstSibling();
            var image = outline.GetComponent<Image>();
            image.sprite = HomeUiFonts.Outline(ColumnRadius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = new Color(1f, 1f, 1f, 0.45f);
            image.raycastTarget = false;
        }

        private void BindInvite(RectTransform row, string playerId, string nickname, bool canInvite)
        {
            var add = row.Find("Add");
            if (add == null)
            {
                return;
            }

            var button = add.GetComponent<Button>();
            var icon = add.GetComponent<Image>();
            if (button == null || icon == null)
            {
                return;
            }

            inviteButtons[playerId] = button;
            inviteIcons[playerId] = icon;
            inviteAllowed[playerId] = canInvite;
            ApplyInviteButton(playerId);
            button.onClick.AddListener(() => TryInvite(playerId, nickname));
        }

        private void TryInvite(string playerId, string nickname)
        {
            if (!CanInvite(playerId) || IsInviteCooling(playerId))
            {
                return;
            }

            inviteReadyAt[playerId] = inviteClock() + InviteCooldownSeconds;
            ApplyInviteButton(playerId);
            InviteClicked?.Invoke(playerId, nickname);
        }

        private void ApplyInviteButton(string playerId)
        {
            var locked = !CanInvite(playerId) || IsInviteCooling(playerId);
            if (inviteButtons.TryGetValue(playerId, out var button) && button != null)
            {
                button.interactable = !locked;
            }

            if (inviteIcons.TryGetValue(playerId, out var icon) && icon != null)
            {
                icon.sprite = locked
                    ? LobbyPlayerListSprites.PlusGray
                    : LobbyPlayerListSprites.Plus;
            }
        }

        private bool CanInvite(string playerId)
        {
            return inviteAllowed.TryGetValue(playerId, out var allowed) && allowed;
        }

        private bool IsInviteCooling(string playerId)
        {
            return inviteReadyAt.TryGetValue(playerId, out var readyAt)
                && readyAt > inviteClock();
        }

        /// <param name="userId">
        /// The backend account of the player this row shows, never the Photon
        /// player id. The report API looks the value up in users.public_id.
        /// </param>
        private void BindReport(RectTransform row, string userId, string displayName, bool below)
        {
            var fill = row.GetComponent<Image>();
            if (fill == null)
            {
                fill = row.gameObject.AddComponent<Image>();
            }

            fill.sprite = HomeUiFonts.Rounded(ColumnRadius);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;
            fill.color = Color.clear;
            fill.raycastTarget = true;

            var tooltip = CreateReportTooltip(row, below);
            var hover = row.gameObject.AddComponent<LobbyReportHover>();
            hover.Bind(fill, tooltip.gameObject, RowHoverFill);

            var button = tooltip.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                hover.HideTooltip();
                ReportClicked?.Invoke(userId, displayName);
            });
        }

        private static RectTransform CreateReportTooltip(RectTransform parent, bool below)
        {
            var tooltip = new GameObject(
                    "Report",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button))
                .GetComponent<RectTransform>();
            tooltip.SetParent(parent, false);

            var label = CreateLabel(
                tooltip, "Label", ReportLabel, HomeUiFonts.ApplyRegular(), ReportFontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.color = ReportTooltipLabel;
            label.raycastTarget = false;
            label.ForceMeshUpdate();

            const float padX = 14f;
            const float padY = 6f;
            var size = new Vector2(
                Mathf.Max(label.preferredWidth + (padX * 2f), 1f),
                Mathf.Max(label.preferredHeight + (padY * 2f), ReportFontSize + (padY * 2f)));

            if (below)
            {
                tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0f);
                tooltip.pivot = new Vector2(0.5f, 1f);
                tooltip.anchoredPosition = new Vector2(0f, -ReportTooltipGap);
            }
            else
            {
                tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 1f);
                tooltip.pivot = new Vector2(0.5f, 0f);
                tooltip.anchoredPosition = new Vector2(0f, ReportTooltipGap);
            }

            tooltip.sizeDelta = size;
            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

            var fill = tooltip.GetComponent<Image>();
            fill.sprite = HomeUiFonts.Rounded(ReportTooltipRadius);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;
            fill.color = ReportTooltipFill;
            fill.raycastTarget = true;

            var canvas = tooltip.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            var parentCanvas = parent.GetComponentInParent<Canvas>();
            canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 1;
            Game.Client.Common.HudScreenScale.Ensure(tooltip.gameObject);
            tooltip.gameObject.AddComponent<GraphicRaycaster>();

            var button = tooltip.GetComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            CreateReportBridge(tooltip, below);
            tooltip.gameObject.SetActive(false);
            return tooltip;
        }

        private static void CreateReportBridge(RectTransform tooltip, bool below)
        {
            var bridge = new GameObject(
                    ReportBridgeName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image))
                .GetComponent<RectTransform>();
            bridge.SetParent(tooltip, false);
            if (below)
            {
                bridge.anchorMin = new Vector2(0f, 1f);
                bridge.anchorMax = new Vector2(1f, 1f);
                bridge.pivot = new Vector2(0.5f, 0f);
            }
            else
            {
                bridge.anchorMin = new Vector2(0f, 0f);
                bridge.anchorMax = new Vector2(1f, 0f);
                bridge.pivot = new Vector2(0.5f, 1f);
            }

            bridge.anchoredPosition = Vector2.zero;
            bridge.sizeDelta = new Vector2(0f, ReportTooltipGap + ReportTooltipOverlap);

            var hit = bridge.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
        }

        private bool CanReuseParticipantRows(IReadOnlyList<LobbyParticipant> next, bool host, string local, bool ready)
        {
            if (!ready || !lastNamesReady || next == null || lastParticipants == null
                || next.Count != lastParticipants.Count || host != lastHost || local != lastLocal || rowMatchLayout != matchLayout
                || rowLanguage != (chromeLocale != null ? chromeLocale.LanguageCode : UiLocale.AppliedLanguage)) return false;
            var rowIndex = 0;
            for (var i = 0; i < next.Count; i++)
            {
                var person = next[i];
                var previous = lastParticipants[i];
                if (person.Id != previous.Id || person.UserId != previous.UserId || person.IsHost != previous.IsHost) return false;
                if (!CanShowParticipant(person.Id, person.Id == local)) continue;
                if (rowIndex >= participantRows.Count) return false;
                var row = participantRows[rowIndex++];
                if (row == null || row.name != "Row_" + person.Id
                    || row.transform.Find("Name").GetComponent<TextMeshProUGUI>().text != ShownParticipantName(person.Id, person.DisplayName)) return false;
            }
            return rowIndex > 0 && rowIndex == participantRows.Count;
        }

        private static void UpdateParticipantVoice(Transform row, LobbyParticipant person)
        {
            var avatar = (RectTransform)row.Find("Avatar");
            var dim = avatar.Find(AvatarDimName);
            var mute = avatar.Find(MuteIconName);
            var badge = LobbyPlayerListSprites.MuteOnProfile(!person.IsListening, person.IsMuted);
            if (badge == null)
            {
                if (dim != null) dim.gameObject.SetActive(false);
                if (mute != null) mute.gameObject.SetActive(false);
                return;
            }
            if (dim == null) CreateMutedOverlay(avatar, badge);
            else
            {
                dim.gameObject.SetActive(true);
                mute.gameObject.SetActive(true);
                mute.GetComponent<Image>().sprite = badge;
            }
        }

        private void CreatePlayerVolumeSlider(RectTransform row, LobbyParticipant person)
        {
            const float extraHeight = 28f * ModalScale;
            var element = row.GetComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = RowHeight + extraHeight;
            // Keep the existing avatar/name/host/kick line above the volume control.
            foreach (RectTransform child in row)
                child.anchoredPosition += new Vector2(0f, extraHeight * .5f);

            var control = FindOrCreateRect("PlayerVoiceVolume", row);
            control.anchorMin = new Vector2(0f, 0f);
            control.anchorMax = new Vector2(1f, 0f);
            control.pivot = new Vector2(.5f, 0f);
            control.offsetMin = new Vector2(AvatarLeft + AvatarSize + NicknameLeft, 5f * ModalScale);
            control.offsetMax = new Vector2(-ActionRight, extraHeight);
            var icon = FindOrCreateRect("Icon", control);
            PinLeft(icon, 0f, new Vector2(16f, 16f) * ModalScale);
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = LobbyPlayerListSprites.SoundOf(false, false);
            image.preserveAspect = true;
            image.raycastTarget = false;

            var percent = CreateLabel(control, "Percent", "", HomeUiFonts.ApplyRegular(), 13f * ModalScale);
            percent.alignment = TextAlignmentOptions.MidlineRight;
            percent.rectTransform.anchorMin = new Vector2(1f, 0f);
            percent.rectTransform.anchorMax = Vector2.one;
            percent.rectTransform.offsetMin = new Vector2(-48f * ModalScale, 0f);
            percent.rectTransform.offsetMax = Vector2.zero;

            var sliderRect = FindOrCreateRect("Slider", control);
            Stretch(sliderRect, 24f * ModalScale, 0f, -56f * ModalScale, 0f);
            var hit = sliderRect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var track = FindOrCreateRect("Track", sliderRect);
            track.anchorMin = new Vector2(0f, .5f);
            track.anchorMax = new Vector2(1f, .5f);
            track.sizeDelta = new Vector2(0f, SettingsStyle.Slider.TrackSize.y * ModalScale);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = HomeUiFonts.Rounded(SettingsStyle.Slider.TrackRadius);
            trackImage.type = Image.Type.Sliced;
            trackImage.color = SettingsStyle.Palette.SliderTrack;
            trackImage.raycastTarget = false;
            var diameter = SettingsStyle.Slider.HandleDiameter * ModalScale;
            var fillArea = FindOrCreateRect("FillArea", sliderRect);
            fillArea.anchorMin = new Vector2(0f, .5f);
            fillArea.anchorMax = new Vector2(1f, .5f);
            fillArea.sizeDelta = new Vector2(-diameter, SettingsStyle.Slider.FillHeight * ModalScale);
            var fill = FindOrCreateRect("Fill", fillArea);
            Stretch(fill, -diameter * .5f - SettingsStyle.Slider.FillLeftOverhang * ModalScale, 0f, 0f, 0f);
            fill.gameObject.AddComponent<RectMask2D>();
            var graphic = FindOrCreateRect("Graphic", fill);
            graphic.anchorMin = graphic.anchorMax = new Vector2(0f, .5f);
            graphic.pivot = new Vector2(0f, .5f);
            graphic.sizeDelta = new Vector2(
                (SettingsStyle.Slider.TrackSize.x + SettingsStyle.Slider.FillLeftOverhang) * ModalScale,
                SettingsStyle.Slider.FillHeight * ModalScale);
            var fillImage = graphic.gameObject.AddComponent<Image>();
            fillImage.sprite = HomeUiFonts.Rounded(SettingsStyle.Slider.FillRadius);
            fillImage.type = Image.Type.Sliced;
            fillImage.color = SettingsStyle.Palette.SliderFill;
            fillImage.raycastTarget = false;
            var handleArea = FindOrCreateRect("HandleArea", sliderRect);
            handleArea.anchorMin = new Vector2(0f, .5f);
            handleArea.anchorMax = new Vector2(1f, .5f);
            handleArea.sizeDelta = new Vector2(-diameter, diameter);
            var handle = FindOrCreateRect("Handle", handleArea);
            handle.anchorMin = handle.anchorMax = new Vector2(.5f, .5f);
            handle.sizeDelta = new Vector2(diameter, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HomeUiFonts.CircleSprite;
            handleImage.color = SettingsStyle.Palette.SliderHandle;
            handleImage.raycastTarget = true;
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handleImage;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.minValue = 0f;
            slider.maxValue = VoicePreferences.MaxPlayerVolume;
            slider.wholeNumbers = true;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var initial = voicePreferences.GetPlayerVolume(person.Id, person.UserId);
            slider.SetValueWithoutNotify(initial);
            percent.text = initial + "%";
            image.sprite = LobbyPlayerListSprites.SoundOf(initial == 0, false);
            slider.onValueChanged.AddListener(value =>
            {
                var volume = Mathf.RoundToInt(value);
                voicePreferences.SetPlayerVolume(person.Id, person.UserId, volume);
                percent.text = volume + "%";
                image.sprite = LobbyPlayerListSprites.SoundOf(volume == 0, false);
            });
        }

        private static void ClearRows(List<GameObject> rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(rows[index]);
                }
                else
                {
                    DestroyImmediate(rows[index]);
                }
            }

            rows.Clear();
        }

        private static RectTransform FindOrCreateRect(string name, Transform parent)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
