using UnityEngine;

namespace DungeonDash
{
    public static class UiSprites
    {
        const int ChromeSize = 18;
        const int ChromeBorder = 6;

        static Sprite _panel;
        static Sprite _inset;
        static Sprite _frame;
        static Sprite _bar;
        static Sprite _solid;
        static Sprite _gradient;
        static Sprite _vignette;
        static Sprite _glow;
        static Sprite _hatch;

        public static Sprite Panel => _panel ??= BuildChrome("UI Panel", raised: true, hollow: false);
        public static Sprite Inset => _inset ??= BuildChrome("UI Inset", raised: false, hollow: false);
        public static Sprite Frame => _frame ??= BuildChrome("UI Frame", raised: true, hollow: true);
        public static Sprite Bar => _bar ??= BuildBar();
        public static Sprite Solid => _solid ??= BuildSolid();
        public static Sprite Gradient => _gradient ??= BuildGradient();
        public static Sprite Vignette => _vignette ??= BuildVignette();
        public static Sprite Glow => _glow ??= BuildGlow();
        public static Sprite Hatch => _hatch ??= BuildHatch();

        static Sprite BuildChrome(string name, bool raised, bool hollow)
        {
            var pixels = new Color32[ChromeSize * ChromeSize];
            var light = raised ? UiPalette.PanelLight : UiPalette.PanelDark;
            var dark = raised ? UiPalette.PanelDark : UiPalette.PanelLight;
            var fill = raised ? UiPalette.PanelFill : UiPalette.InsetFill;

            for (int y = 0; y < ChromeSize; y++)
            for (int x = 0; x < ChromeSize; x++)
            {
                int inset = Mathf.Min(Mathf.Min(x, y), Mathf.Min(ChromeSize - 1 - x, ChromeSize - 1 - y));
                Color color;
                if (inset == 0) color = UiPalette.Ink;
                else if (inset == 1) color = UiPalette.PanelFrame;
                else if (inset == 2) color = y > ChromeSize / 2 || x < ChromeSize / 2 ? light : dark;
                else if (inset == 3) color = UiPalette.PanelFrame.Scale(1.25f);
                else color = hollow ? Color.clear : fill;
                pixels[y * ChromeSize + x] = color;
            }

            return Build(name, ChromeSize, ChromeSize, pixels,
                new Vector4(ChromeBorder, ChromeBorder, ChromeBorder, ChromeBorder), 50f);
        }

        static Sprite BuildBar()
        {
            const int size = 9;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inset = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                pixels[y * size + x] = inset == 0 ? UiPalette.Ink : Color.white;
            }
            return Build("UI Bar", size, size, pixels, new Vector4(3f, 3f, 3f, 3f), 100f);
        }

        static Sprite BuildSolid()
        {
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            return Build("UI Solid", 4, 4, pixels, Vector4.zero, 100f);
        }

        static Sprite BuildGradient()
        {
            const int height = 64;
            var pixels = new Color32[height * 4];
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                var color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t));
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = color;
            }
            return Build("UI Gradient", 4, height, pixels, Vector4.zero, 100f);
        }

        static Sprite BuildVignette()
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - center, y - center).magnitude / center;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - 0.35f) / 0.75f));
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
            return Build("UI Vignette", size, size, pixels, Vector4.zero, 100f);
        }

        static Sprite BuildGlow()
        {
            const int size = 48;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - center, y - center).magnitude / center;
                float alpha = Mathf.Clamp01(1f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
            return Build("UI Glow", size, size, pixels, Vector4.zero, 100f);
        }

        static Sprite BuildHatch()
        {
            const int size = 8;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = (x + y) % 4 == 0 ? new Color(1f, 1f, 1f, 0.14f) : Color.clear;
            return Build("UI Hatch", size, size, pixels, Vector4.zero, 100f);
        }

        static Sprite Build(string name, int width, int height, Color32[] pixels, Vector4 border,
            float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.one * 0.5f,
                pixelsPerUnit, 0u, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
