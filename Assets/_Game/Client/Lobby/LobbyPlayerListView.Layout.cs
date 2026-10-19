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
    public sealed partial class LobbyPlayerListView
    {
        private void BuildLayout()
        {
            var columns = FindOrCreateRect("Columns", transform);
            Stretch(columns, 0f, PanelPadding, 0f, -PanelPadding);
            var row = columns.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(row);
                }
                else
                {
                    DestroyImmediate(row);
                }
            }

            var participants = CreateColumn(columns, "Participants", ParticipantsTitle, out participantsTitle);
            participantRowRoot = participants;
            var friends = CreateColumn(columns, "Friends", FriendsTitle, out friendsTitle);
            friendRowRoot = friends;
            PlaceColumns();
        }

        private void PlaceColumns()
        {
            var columns = transform.Find("Columns") as RectTransform;
            if (columns == null)
            {
                return;
            }

            Stretch(
                columns,
                0f,
                matchLayout ? 0f : PanelPadding,
                0f,
                matchLayout ? 0f : -PanelPadding);
            var participants = columns.Find("Participants") as RectTransform;
            var friends = columns.Find("Friends") as RectTransform;
            if (matchLayout)
            {
                if (friends != null)
                {
                    friends.gameObject.SetActive(false);
                }

                PlaceFullColumn(participants);
                SetColumnOutlineVisible(participants, false);
                InsetColumnContent(participants);
                return;
            }

            if (friends != null)
            {
                friends.gameObject.SetActive(true);
            }

            PlaceColumn(participants, isLeft: true);
            PlaceColumn(friends, isLeft: false);
            SetColumnOutlineVisible(participants, true);
            SetColumnOutlineVisible(friends, true);
            InsetColumnContent(participants);
            InsetColumnContent(friends);
        }

        private static void SetColumnOutlineVisible(RectTransform column, bool visible)
        {
            if (column == null)
            {
                return;
            }

            var outline = column.GetComponent<Image>();
            if (outline != null)
            {
                outline.enabled = visible;
            }
        }

        private static void PlaceFullColumn(RectTransform column)
        {
            if (column == null)
            {
                return;
            }

            column.anchorMin = Vector2.zero;
            column.anchorMax = Vector2.one;
            column.pivot = new Vector2(0.5f, 0.5f);
            column.offsetMin = Vector2.zero;
            column.offsetMax = Vector2.zero;
            column.anchoredPosition = Vector2.zero;
            column.sizeDelta = Vector2.zero;
        }

        private static void PlaceColumn(RectTransform column, bool isLeft)
        {
            if (column == null)
            {
                return;
            }

            var side = (1f - (ColumnWidthRatio * 2f)) * 0.25f;
            var minX = isLeft ? side : 1f - side - ColumnWidthRatio;
            column.anchorMin = new Vector2(minX, 0f);
            column.anchorMax = new Vector2(minX + ColumnWidthRatio, 1f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.offsetMin = Vector2.zero;
            column.offsetMax = Vector2.zero;
            column.anchoredPosition = Vector2.zero;
            column.sizeDelta = Vector2.zero;
        }

        private static void InsetColumnContent(RectTransform column)
        {
            if (column == null)
            {
                return;
            }

            var pad = ColumnInnerPadding;
            var title = column.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.pivot = new Vector2(0.5f, 1f);
                title.offsetMin = new Vector2(TitleLeftPadding, -(pad + TitleHeight));
                title.offsetMax = new Vector2(-pad, -pad);
            }

            var scroll = column.Find("Scroll") as RectTransform;
            if (scroll != null)
            {
                scroll.anchorMin = Vector2.zero;
                scroll.anchorMax = Vector2.one;
                scroll.pivot = new Vector2(0.5f, 1f);
                scroll.offsetMin = new Vector2(pad, pad);
                scroll.offsetMax = new Vector2(-pad, -(pad + TitleHeight + TitleToRows));
            }
        }

        private RectTransform CreateColumn(
            Transform parent,
            string name,
            string title,
            out TextMeshProUGUI titleLabel)
        {
            var column = FindOrCreateRect(name, parent);
            var element = column.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.ignoreLayout = true;
            }

            var outline = column.GetComponent<Image>();
            if (outline == null)
            {
                outline = column.gameObject.AddComponent<Image>();
            }

            outline.sprite = HomeUiFonts.Outline(ColumnRadius);
            outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = 1f;
            outline.color = new Color(1f, 1f, 1f, 0.45f);
            outline.raycastTarget = false;

            var titleRect = FindOrCreateRect("Title", column);
            titleLabel = titleRect.GetComponent<TextMeshProUGUI>();
            if (titleLabel == null)
            {
                titleLabel = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            ApplyTitle(titleLabel, title);

            var scroll = FindOrCreateRect("Scroll", column);
            InsetColumnContent(column);
            if (scroll.GetComponent<RectMask2D>() == null)
            {
                scroll.gameObject.AddComponent<RectMask2D>();
            }

            var content = FindOrCreateRect("RowRoot", scroll);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            }

            scrollRect.content = content;
            scrollRect.viewport = scroll;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = RowHeight;
            if (name == "Friends" || name == "Participants")
            {
                scrollRect.verticalScrollbar = CreateScrollbar(scroll);
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }

            return content;
        }

        private static Scrollbar CreateScrollbar(RectTransform viewport)
        {
            var bar = FindOrCreateRect("Scrollbar", viewport);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(HomeStyle.Friends.ScrollbarHitWidth, 0f);
            bar.anchoredPosition = Vector2.zero;
            var hit = bar.GetComponent<Image>();
            if (hit == null)
            {
                hit = bar.gameObject.AddComponent<Image>();
            }

            hit.color = Color.clear;
            hit.raycastTarget = true;

            var sliding = FindOrCreateRect("Sliding Area", bar);
            Stretch(sliding, 0f, 0f, 0f, 0f);

            var handle = FindOrCreateRect("Handle", sliding);
            Stretch(handle, 0f, 0f, 0f, 0f);
            var grab = handle.GetComponent<Image>();
            if (grab == null)
            {
                grab = handle.gameObject.AddComponent<Image>();
            }

            grab.color = Color.clear;
            grab.raycastTarget = true;

            var visual = FindOrCreateRect("Visual", handle);
            visual.anchorMin = new Vector2(0.5f, 0f);
            visual.anchorMax = new Vector2(0.5f, 1f);
            visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(HomeStyle.Friends.ScrollbarWidth, 0f);
            visual.anchoredPosition = Vector2.zero;
            var paint = visual.GetComponent<Image>();
            if (paint == null)
            {
                paint = visual.gameObject.AddComponent<Image>();
            }

            paint.sprite = HomeUiFonts.Rounded(HomeStyle.Friends.ScrollbarRadius);
            paint.type = Image.Type.Sliced;
            paint.color = HomeStyle.Palette.ScrollHandle;
            paint.raycastTarget = false;

            var scrollbar = bar.GetComponent<Scrollbar>();
            if (scrollbar == null)
            {
                scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            }

            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = grab;
            scrollbar.transition = Selectable.Transition.None;
            return scrollbar;
        }

        private RectTransform CreateRow(
            RectTransform parent,
            List<GameObject> bucket,
            string name,
            string nickname,
            bool showLeader,
            bool showKick,
            bool showAdd,
            bool isSelf = false,
            bool isMuted = false,
            bool isTalking = false,
            bool showVoice = true,
            bool isListening = true,
            string playerId = null,
            string userId = null)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;
            element.flexibleWidth = 1f;

            CreateAvatar(row, isMuted, isListening, playerId, userId);
            var nameLabel = CreateNickname(row, nickname, isSelf);
            var afterName = showVoice
                ? CreateVoice(row, nameLabel, isMuted, isTalking)
                : nameLabel.rectTransform;
            CreateLeader(row, afterName, showLeader);
            if (showKick)
            {
                CreateKick(row);
            }

            if (showAdd)
            {
                CreateAddButton(row);
            }

            bucket.Add(row.gameObject);
            return row;
        }

        private void CreateInfoRow(RectTransform parent, List<GameObject> bucket, string message)
        {
            var row = new GameObject("InfoRow", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;
            var label = CreateLabel(row, "Label", message, HomeUiFonts.ApplyRegular(), NicknameFontSize);
            Stretch(label.rectTransform, AvatarLeft, 0f, -ActionRight, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            bucket.Add(row.gameObject);
        }

        private static void CreateAvatar(
            RectTransform parent,
            bool muted,
            bool listening,
            string playerId,
            string userId)
        {
            var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            avatar.SetParent(parent, false);
            PinLeft(avatar, AvatarLeft, new Vector2(AvatarSize, AvatarSize));
            var image = avatar.GetComponent<Image>();
            image.sprite = HomeUiFonts.CircleSprite;
            image.color = AvatarColor;
            image.raycastTarget = false;
            image.preserveAspect = true;
            AvatarFaceSlot.Attach(avatar).Follow(playerId, userId);
            var badge = LobbyPlayerListSprites.MuteOnProfile(!listening, muted);
            if (badge != null)
            {
                CreateMutedOverlay(avatar, badge);
            }
        }

        private static void CreateMutedOverlay(RectTransform avatar, Sprite badge)
        {
            var dim = new GameObject(
                    AvatarDimName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            dim.SetParent(avatar, false);
            Stretch(dim, 0f, 0f, 0f, 0f);
            var dimImage = dim.GetComponent<Image>();
            dimImage.sprite = HomeUiFonts.CircleSprite;
            dimImage.color = MutedAvatarDim;
            dimImage.raycastTarget = false;
            dimImage.preserveAspect = true;

            var mute = new GameObject(
                    MuteIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            mute.SetParent(avatar, false);
            mute.anchorMin = mute.anchorMax = new Vector2(0.5f, 0.5f);
            mute.pivot = new Vector2(0.5f, 0.5f);
            mute.anchoredPosition = Vector2.zero;
            mute.sizeDelta = new Vector2(MuteIconSize, MuteIconSize);
            var muteImage = mute.GetComponent<Image>();
            muteImage.sprite = badge;
            muteImage.color = Color.white;
            muteImage.preserveAspect = true;
            muteImage.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateNickname(RectTransform parent, string nickname, bool isSelf)
        {
            var name = CreateLabel(parent, "Name", nickname, HomeUiFonts.ApplyRegular(), NicknameFontSize);
            name.color = isSelf ? SelfNicknameColor : Color.white;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.ForceMeshUpdate();
            var size = new Vector2(
                Mathf.Max(name.preferredWidth, 1f),
                Mathf.Max(name.preferredHeight, NicknameFontSize));
            PinLeft(
                name.rectTransform,
                AvatarLeft + AvatarSize + NicknameLeft,
                size);
            return name;
        }

        private static RectTransform CreateVoice(
            RectTransform parent, TextMeshProUGUI name, bool muted, bool talking)
        {
            var voice = new GameObject(
                    VoiceIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            voice.SetParent(parent, false);
            var nameRight = name.rectTransform.anchoredPosition.x + name.rectTransform.sizeDelta.x;
            PinLeft(voice, nameRight + VoiceIconGap, new Vector2(VoiceIconSize, VoiceIconSize));
            var icon = voice.GetComponent<Image>();
            icon.sprite = LobbyPlayerListSprites.SoundOf(muted, talking);
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return voice;
        }

        private static void CreateLeader(RectTransform parent, RectTransform after, bool showLeader)
        {
            var leader = new GameObject("Leader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            leader.SetParent(parent, false);
            var afterRight = after.anchoredPosition.x + after.sizeDelta.x;
            PinLeft(leader, afterRight + LeaderIconGap, new Vector2(LeaderIconSize, LeaderIconSize));
            var icon = leader.GetComponent<Image>();
            icon.sprite = LobbyPlayerListSprites.Leader;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            leader.gameObject.SetActive(showLeader);
        }

        private static void CreateKick(RectTransform parent)
        {
            var kick = new GameObject("Kick", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                .GetComponent<RectTransform>();
            kick.SetParent(parent, false);
            var label = kick.GetComponent<TextMeshProUGUI>();
            ApplyBody(label, KickLabel);
            label.color = KickColor;
            label.raycastTarget = true;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.ForceMeshUpdate();
            PinRight(
                kick,
                ActionRight,
                new Vector2(
                    Mathf.Max(label.preferredWidth, 1f),
                    Mathf.Max(label.preferredHeight, KickFontSize)));
            var button = kick.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            button.transition = Selectable.Transition.None;
        }

        private static void CreateAddButton(RectTransform parent)
        {
            var add = new GameObject("Add", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            add.SetParent(parent, false);
            PinRight(add, ActionRight, new Vector2(AddButtonSize, AddButtonSize));
            var image = add.GetComponent<Image>();
            image.sprite = LobbyPlayerListSprites.Plus;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;
            var button = add.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
        }

        private static void PinLeft(RectTransform rect, float x, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = size;
        }

        private static void PinRight(RectTransform rect, float right, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-right, 0f);
            rect.sizeDelta = size;
        }

        private static void ApplyTitle(TextMeshProUGUI label, string text)
        {
            label.text = text;
            label.font = HomeUiFonts.ApplyBold();
            label.fontSize = TitleFontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
        }

        private static void ApplyBody(TextMeshProUGUI label, string text)
        {
            label.text = text;
            label.font = HomeUiFonts.ApplyRegular();
            label.fontSize = KickFontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Midline;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font != null ? font : HomeUiFonts.ApplyRegular();
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

    }
}
