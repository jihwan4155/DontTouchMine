using TMPro;
using Game.Client.Home;
using static Game.Client.Match.MatchChatView;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    internal static class MatchChatLayout
    {
        public static RectTransform CreatePanel(Transform parent, string name)
        {
            var panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.sprite = HomeUiFonts.Rounded(PanelRadius);
            image.type = Image.Type.Sliced;
            image.color = PanelColor;
            image.raycastTarget = true;
            return panel.GetComponent<RectTransform>();
        }

        public static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = true;
            return image;
        }

        public static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = MatchChatView.ChatFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.raycastTarget = false;
            text.richText = false;
            var layout = gameObject.GetComponent<LayoutElement>();
            layout.minHeight = fontSize + 4f;
            layout.preferredHeight = -1f;
            layout.flexibleWidth = 1f;
            return text;
        }

        public static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static void FitScrollingInputText(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(2048f, 0f);
            var text = rect.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.margin = Vector4.zero;
                text.extraPadding = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
            }

            var layout = rect.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
        }

        public static void FitInputLabel(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Stretch(rect);
            var text = rect.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.margin = Vector4.zero;
                text.extraPadding = false;
            }

            var layout = rect.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
