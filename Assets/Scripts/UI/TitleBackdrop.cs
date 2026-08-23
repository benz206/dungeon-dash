using UnityEngine;
using UnityEngine.UI;

namespace DungeonDash
{
    public sealed class TitleBackdrop : MonoBehaviour
    {
        const float TileSize = 72f;
        const float CastSpacing = 132f;
        const int CastCount = 6;

        Image[] _cast;
        float[] _baseY;
        float _time;

        public void Initialize(CatalogIndex catalog)
        {
            var root = (RectTransform)transform;
            UiKit.Stretch(root, 0f, 0f, 0f, 0f);

            var backdrop = UiKit.Fill("Ground", root, new Color(0.028f, 0.026f, 0.032f));
            UiKit.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            BuildTiles(root, catalog);
            BuildWalls(root, catalog);
            BuildCast(root, catalog);

            var shade = UiKit.Fill("Shade", root, new Color(0.02f, 0.016f, 0.022f, 0.55f));
            UiKit.Stretch(shade.rectTransform, 0f, 0f, 0f, 0f);
            UiKit.Vignette("Vignette", root, 0.8f);
        }

        static void BuildTiles(RectTransform root, CatalogIndex catalog)
        {
            var tiles = UiKit.Node("Tiles", root);
            UiKit.Stretch(tiles, 0f, 0f, 0f, 0f);
            var floors = catalog.Catalog.floors;
            int columns = Mathf.CeilToInt(UiKit.Reference.x / TileSize);
            int rows = Mathf.CeilToInt(UiKit.Reference.y / TileSize);
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                var tile = UiKit.Icon($"Tile {x},{y}", tiles, floors[(x + y * 3) % floors.Length]);
                UiKit.Place(tile.rectTransform, x * TileSize, y * TileSize, TileSize, TileSize);
                tile.color = new Color(0.40f, 0.38f, 0.41f);
            }
        }

        static void BuildWalls(RectTransform root, CatalogIndex catalog)
        {
            var walls = UiKit.Node("Walls", root);
            UiKit.Stretch(walls, 0f, 0f, 0f, 0f);
            var top = catalog.Tile("wall_top_mid");
            var bottom = catalog.Tile("edge_down");
            var tint = new Color(0.68f, 0.62f, 0.62f);
            int columns = Mathf.CeilToInt(UiKit.Reference.x / TileSize);
            for (int x = 0; x < columns; x++)
            {
                var upper = UiKit.Icon($"Top {x}", walls, top);
                UiKit.Place(upper.rectTransform, x * TileSize, 22f, TileSize, TileSize);
                upper.color = tint;
                var lower = UiKit.Icon($"Bottom {x}", walls, bottom);
                UiKit.Place(lower.rectTransform, x * TileSize, UiKit.Reference.y - 92f, TileSize, TileSize);
                lower.color = tint;
            }
        }

        void BuildCast(RectTransform root, CatalogIndex catalog)
        {
            var castRoot = UiKit.Node("Cast", root);
            UiKit.Stretch(castRoot, 0f, 0f, 0f, 0f);
            var characters = catalog.Catalog.characters;
            int count = Mathf.Min(CastCount, characters.Length);
            _cast = new Image[count];
            _baseY = new float[count];

            for (int i = 0; i < count; i++)
            {
                if (characters[i].idle.Length == 0) continue;
                var hero = UiKit.Icon($"Cast {i}", castRoot, characters[i].idle[0]);
                float x = i < 3 ? 58f + i * CastSpacing : UiKit.Reference.x - 162f - (i - 3) * CastSpacing;
                float y = UiKit.Reference.y - 214f - (i % 2) * 24f;
                UiKit.Place(hero.rectTransform, x, y, 104f, 128f);
                hero.color = new Color(0.78f, 0.75f, 0.75f);
                _cast[i] = hero;
                _baseY[i] = -y;
            }
        }

        void Update()
        {
            _time += Time.unscaledDeltaTime;
            for (int i = 0; i < _cast.Length; i++)
            {
                if (_cast[i] == null) continue;
                float bob = Mathf.Sin(_time * (1.25f + i * 0.13f) + i * 1.7f) * 5f;
                var rect = _cast[i].rectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, _baseY[i] + bob);
            }
        }
    }
}
