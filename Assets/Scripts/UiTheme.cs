using UnityEngine;

namespace DungeonDash
{
    /// Shared IMGUI palette, pixel fonts and drawing helpers for the DungeonGame UI.
    public static class UiTheme
    {
        public static readonly Color PanelFill = new(0.12f, 0.145f, 0.19f, 0.99f);
        public static readonly Color PanelBorder = new(0.035f, 0.045f, 0.065f, 1f);
        public static readonly Color PanelShadow = new(0f, 0f, 0f, 0.62f);
        public static readonly Color SubPanelFill = new(0.065f, 0.078f, 0.105f, 0.98f);
        public static readonly Color SubPanelBorder = new(0.19f, 0.22f, 0.28f, 1f);
        public static readonly Color AccentRed = new(0.57f, 0.085f, 0.12f, 1f);
        public static readonly Color AccentGold = new(0.72f, 0.53f, 0.31f, 1f);
        public static readonly Color Cream = new(0.93f, 0.89f, 0.77f, 1f);

        static Font _titleFont;
        static Font _bodyFont;
        static bool _fontsLoaded;

        // Press Start 2P — blocky display face, headings/short caps labels only.
        public static Font TitleFont { get { LoadFonts(); return _titleFont; } }
        // VT323 — legible pixel face for body text, buttons and long strings.
        public static Font BodyFont { get { LoadFonts(); return _bodyFont; } }

        static void LoadFonts()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;
            // Resources.Load returns null when missing; GUIStyle.font = null falls back to the skin default.
            _titleFont = Resources.Load<Font>("Fonts/PressStart2P-Regular");
            _bodyFont = Resources.Load<Font>("Fonts/VT323-Regular");
        }

        public static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        // Top-level floating dialog: drop shadow + fill + double border.
        public static void DrawPanel(Rect rect)
        {
            DrawRect(new Rect(rect.x + 8f, rect.y + 10f, rect.width, rect.height), PanelShadow);
            DrawRect(rect, PanelFill);
            DrawBorder(rect, PanelBorder, 4f);
            DrawBorder(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f),
                new Color(0.25f, 0.29f, 0.36f, 0.8f), 2f);
        }

        // Nested container inside a dialog: fill + single border, no shadow.
        public static void SubPanel(Rect rect)
        {
            DrawRect(rect, SubPanelFill);
            DrawBorder(rect, SubPanelBorder, 2f);
        }

        public static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null) return;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
        }

        // GUI.Button with a manual hover brighten — the sprite-backed styles have no hover texture.
        public static bool Button(Rect rect, string text, GUIStyle style)
        {
            bool hovered = Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition);
            var previous = GUI.color;
            if (hovered) GUI.color = previous * 1.15f;
            bool clicked = GUI.Button(rect, text, style);
            GUI.color = previous;
            if (clicked) GameAudio.Play("ui_click", 0.5f);
            return clicked;
        }

        // GUILayout.Button equivalent with the same hover brighten, for layout-driven screens.
        public static bool LayoutButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), style, options);
            bool hovered = Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition);
            var previous = GUI.color;
            if (hovered) GUI.color = previous * 1.15f;
            bool clicked = GUI.Button(rect, text, style);
            GUI.color = previous;
            if (clicked) GameAudio.Play("ui_click", 0.5f);
            return clicked;
        }

        public static Color RarityColor(string rarity) => rarity switch
        {
            "Mythic" => new Color(1f, 0.58f, 0.18f),
            "Epic" => new Color(0.72f, 0.4f, 1f),
            "Rare" => new Color(0.2f, 0.68f, 1f),
            _ => new Color(0.55f, 0.65f, 0.74f)
        };
    }
}
