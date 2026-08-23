using System;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public enum ButtonTone { Primary, Danger, Ghost }

    public readonly struct UiBar
    {
        public RectTransform Root { get; }
        public Image Fill { get; }

        public UiBar(RectTransform root, Image fill)
        {
            Root = root;
            Fill = fill;
        }

        public void SetAmount(float amount) =>
            Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
    }

    public readonly struct UiDialog
    {
        public RectTransform Holder { get; }
        public RectTransform Panel { get; }
        public RectTransform Body { get; }
        public Text Title { get; }
        public Text Subtitle { get; }
        public RectTransform HeaderActions { get; }
        public Image HeaderBar { get; }

        public UiDialog(RectTransform holder, RectTransform panel, RectTransform body, Text title,
            Text subtitle, RectTransform headerActions, Image headerBar)
        {
            Holder = holder;
            Panel = panel;
            Body = body;
            Title = title;
            Subtitle = subtitle;
            HeaderActions = headerActions;
            HeaderBar = headerBar;
        }
    }

    public static class UiKit
    {
        public static readonly Vector2 Reference = new(1280f, 720f);

        static Font _title;
        static Font _body;
        static Font _fallback;

        public static Font TitleFont => _title ??= LoadFont("Fonts/PressStart2P-Regular");
        public static Font BodyFont => _body ??= LoadFont("Fonts/VT323-Regular");

        static Font LoadFont(string path)
        {
            var font = Resources.Load<Font>(path) ?? (_fallback ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            if (font != null && font.material != null && font.material.mainTexture != null)
                font.material.mainTexture.filterMode = FilterMode.Point;
            return font;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        public static Image Chrome(string name, Transform parent, Sprite sprite, Color color)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var image = node.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Panel(string name, Transform parent) =>
            Chrome(name, parent, UiSprites.Panel, Color.white);

        public static Image Inset(string name, Transform parent) =>
            Chrome(name, parent, UiSprites.Inset, Color.white);

        public static Image Frame(string name, Transform parent, Color color) =>
            Chrome(name, parent, UiSprites.Frame, color);

        public static Image Fill(string name, Transform parent, Color color) =>
            Chrome(name, parent, UiSprites.Solid, color);

        public static Image Icon(string name, Transform parent, Sprite sprite)
        {
            var image = Chrome(name, parent, sprite, Color.white);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool title = false)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var label = node.GetComponent<Text>();
            label.font = title ? TitleFont : BodyFont;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.text = text;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Text Wrapped(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var label = Label(name, parent, text, size, color, anchor);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        public static UiBar Bar(string name, Transform parent, Color fillColor)
        {
            var track = Chrome(name, parent, UiSprites.Bar, UiPalette.Ink.Alpha(0.85f));
            var fill = Chrome("Fill", track.transform, UiSprites.Bar, fillColor);
            Stretch(fill.rectTransform, 2f, 2f, 2f, 2f);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            return new UiBar(track.rectTransform, fill);
        }

        public static UiButton PushButton(string name, Transform parent, string text, ButtonTone tone,
            Action onClick, int fontSize = 15)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UiButton));
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            var background = node.GetComponent<Image>();
            background.sprite = UiSprites.Panel;
            background.type = Image.Type.Sliced;
            background.raycastTarget = true;

            var highlight = Frame("Highlight", rect, Color.clear);
            Stretch(highlight.rectTransform, 0f, 0f, 0f, 0f);

            var content = Node("Content", rect);
            Stretch(content, 0f, 0f, 0f, 0f);
            var label = Label("Text", content, text, fontSize, UiPalette.Cream, TextAnchor.MiddleCenter, true);
            Stretch(label.rectTransform, 8f, 0f, 8f, 0f);

            var (rest, hover) = ToneColors(tone);
            var button = node.GetComponent<UiButton>();
            button.Bind(background, highlight, content, rest, hover);
            if (onClick != null) button.Clicked += onClick;
            return button;
        }

        static (Color rest, Color hover) ToneColors(ButtonTone tone) => tone switch
        {
            ButtonTone.Danger => (UiPalette.Crimson, UiPalette.Ember),
            ButtonTone.Ghost => (UiPalette.PanelFill.Alpha(0.55f), UiPalette.RowSelected),
            _ => (new Color(0.180f, 0.235f, 0.330f), new Color(0.278f, 0.392f, 0.529f))
        };

        public static Text ButtonLabel(UiButton button) => button.GetComponentInChildren<Text>();

        public static RectTransform Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform Center(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Corner(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Shade(string name, Transform parent, Color color)
        {
            var image = Fill(name, parent, color);
            Stretch(image.rectTransform, 0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            return image;
        }

        public static Image Vignette(string name, Transform parent, float strength)
        {
            var image = Chrome(name, parent, UiSprites.Vignette, Color.white.Alpha(strength));
            image.type = Image.Type.Simple;
            Stretch(image.rectTransform, -120f, -120f, -120f, -120f);
            return image;
        }

        public static Image Glow(string name, Transform parent, Color color)
        {
            var image = Chrome(name, parent, UiSprites.Glow, color);
            image.type = Image.Type.Simple;
            return image;
        }

        public static Image Header(string name, Transform parent, Color accent)
        {
            var header = Fill(name, parent, accent);
            var hatch = Chrome("Hatch", header.transform, UiSprites.Hatch, Color.white.Alpha(0.5f));
            hatch.type = Image.Type.Tiled;
            Stretch(hatch.rectTransform, 0f, 0f, 0f, 0f);

            var underline = Fill("Underline", header.transform, UiPalette.Ink.Alpha(0.7f));
            var rect = underline.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 3f);
            return header;
        }


        public static UiDialog Dialog(Transform parent, string title, string subtitle, Color accent,
            float width, float height)
        {
            var holder = Node("Dialog", parent);
            Center(holder, width, height);

            var shadow = Fill("Shadow", holder, UiPalette.Ink.Alpha(0.5f));
            Stretch(shadow.rectTransform, -10f, -14f, -10f, -6f);

            var panel = Panel("Frame", holder);
            Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);

            var header = Header("Header", panel.transform, accent);
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.offsetMin = new Vector2(6f, -70f);
            headerRect.offsetMax = new Vector2(-6f, -6f);

            var titleLabel = Label("Title", header.transform, title, 21, UiPalette.Cream,
                TextAnchor.UpperLeft, true);
            Place(titleLabel.rectTransform, 18f, 12f, width - 260f, 26f);
            var subtitleLabel = Label("Subtitle", header.transform, subtitle, 17,
                UiPalette.Cream.Alpha(0.75f), TextAnchor.UpperLeft);
            Place(subtitleLabel.rectTransform, 18f, 38f, width - 260f, 22f);

            var headerRight = Node("Actions", header.transform);
            Corner(headerRight, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(220f, 40f));

            var body = Node("Body", panel.transform);
            Stretch(body, 18f, 84f, 18f, 18f);

            return new UiDialog(holder, panel.rectTransform, body, titleLabel, subtitleLabel, headerRight, header);
        }

        public static ScrollRect ScrollList(string name, Transform parent, out RectTransform content)
        {
            var frame = Inset(name, parent);
            frame.raycastTarget = true;
            var scroll = frame.gameObject.AddComponent<ScrollRect>();

            var viewport = Node("Viewport", frame.transform);
            Stretch(viewport, 8f, 8f, 8f, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            scroll.inertia = false;
            return scroll;
        }

        public static void SetContentHeight(RectTransform content, float height) =>
            content.sizeDelta = new Vector2(0f, height);

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static CanvasGroup Group(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            return group != null ? group : rect.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
