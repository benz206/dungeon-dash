#!/usr/bin/env python3
"""Generate 16x16 tileable GROUND tiles (grass + path) matching the
0x72 16x16 Dungeon Tileset II aesthetic used by dungeon-dash.

Deterministic (fixed seed) and idempotent — re-running overwrites its
outputs. Produces:
    Assets/Art/Tiles/grass_1..4.png  + .meta
    Assets/Art/Tiles/path_1..4.png   + .meta

Palette was calibrated against the existing floor_*.png / wall tiles:
    floor base    #483b3a  (72,59,58)   dark warm stone
    floor shadow  #222222  (34,34,34)   near-black
    floor light   #775c55  (119,92,85)  warm muted highlight
    accent        #aa8d7a  (170,141,122)
The grass/path palettes below sit at the same low saturation and
brightness range so they read as belonging to the same set.

.meta files are cloned verbatim from floor_1.png.meta with only the
top-level `guid:` swapped for a fresh unique 32-hex GUID, preserving the
exact import settings (16 PPU, point filter, no compression, Single).
"""
import os
import re
import math
import random
import uuid

TILES_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "Tiles")
TILES_DIR = os.path.abspath(TILES_DIR)
REF_META = os.path.join(TILES_DIR, "floor_1.png.meta")
SIZE = 16
MASTER_SEED = 0x0472  # deterministic

# --- Palettes -------------------------------------------------------------
# Ordered dark -> light. Each is a hard-pixel band; specks add accents.
GRASS = {
    "shadow": (44, 53, 33),    # #2c3521 deep moss shadow
    "base":   (66, 78, 46),    # #424e2e mid mossy green
    "mid":    (86, 100, 58),   # #56643a lit green
    "light":  (112, 126, 74),  # #707e4a highlight blade
    "spark":  (134, 148, 88),  # #869458 sparse bright speck
    "dark":   (34, 41, 26),    # #22291a darkest speck/dirt fleck
}
PATH = {
    "shadow": (44, 35, 28),    # #2c231c packed-earth shadow
    "base":   (84, 68, 51),    # #544433 warm dirt brown
    "mid":    (104, 84, 63),   # #68543f trodden dirt
    "light":  (130, 106, 78),  # #826a4e worn highlight
    "spark":  (156, 129, 97),  # #9c8161 pale pebble top
    "dark":   (34, 27, 21),    # #221b15 crack / deep gap
}


def wrapping_value_noise(size, lattice, rng):
    """Tileable value-noise field in [0,1], smoothed and wrap-safe.

    A random value grid of period `lattice` (which must divide `size`) is
    bilinearly interpolated with wrap, guaranteeing seamless edges.
    """
    grid = [[rng.random() for _ in range(lattice)] for _ in range(lattice)]

    def smooth(t):
        return t * t * (3 - 2 * t)  # smoothstep

    field = [[0.0] * size for _ in range(size)]
    scale = lattice / size
    for y in range(size):
        gy = y * scale
        y0 = int(math.floor(gy)) % lattice
        y1 = (y0 + 1) % lattice
        fy = smooth(gy - math.floor(gy))
        for x in range(size):
            gx = x * scale
            x0 = int(math.floor(gx)) % lattice
            x1 = (x0 + 1) % lattice
            fx = smooth(gx - math.floor(gx))
            top = grid[y0][x0] * (1 - fx) + grid[y0][x1] * fx
            bot = grid[y1][x0] * (1 - fx) + grid[y1][x1] * fx
            field[y][x] = top * (1 - fy) + bot * fy
    return field


def quantize(v, thresholds, colors):
    """Map a [0,1] value to a palette color via ascending thresholds."""
    for t, c in zip(thresholds, colors):
        if v < t:
            return c
    return colors[-1]


def put(px, x, y, color):
    px[x % SIZE, y % SIZE] = color + (255,)


def scatter(px, rng, count, color, size_choices):
    """Place `count` wrap-safe specks of the given color."""
    for _ in range(count):
        cx = rng.randrange(SIZE)
        cy = rng.randrange(SIZE)
        s = rng.choice(size_choices)
        if s == 1:
            put(px, cx, cy, color)
        elif s == 2:  # 2px vertical blade
            put(px, cx, cy, color)
            put(px, cx, cy + 1, color)
        elif s == 3:  # tiny L cluster
            put(px, cx, cy, color)
            put(px, cx + 1, cy, color)
            put(px, cx, cy + 1, color)


def make_grass(variant):
    from PIL import Image
    rng = random.Random(MASTER_SEED ^ (0x1000 + variant))
    img = Image.new("RGBA", (SIZE, SIZE))
    px = img.load()

    # Two overlaid noise fields (coarse blotches + finer detail) -> banded
    # mossy ground. lattice 4 & 8 both divide 16 -> tileable.
    coarse = wrapping_value_noise(SIZE, 4, rng)
    fine = wrapping_value_noise(SIZE, 8, rng)
    cols = [GRASS["shadow"], GRASS["base"], GRASS["mid"], GRASS["light"]]
    for y in range(SIZE):
        for x in range(SIZE):
            v = 0.62 * coarse[y][x] + 0.38 * fine[y][x]
            put(px, x, y, quantize(v, [0.30, 0.60, 0.82], cols))

    # Sparse blades (light, vertical) and dark flecks so a field reads varied.
    scatter(px, rng, 6, GRASS["light"], [2, 2, 1])
    scatter(px, rng, 3, GRASS["spark"], [1, 2])
    scatter(px, rng, 4, GRASS["dark"], [1])
    return img


def make_path(variant):
    from PIL import Image
    rng = random.Random(MASTER_SEED ^ (0x2000 + variant))
    img = Image.new("RGBA", (SIZE, SIZE))
    px = img.load()

    coarse = wrapping_value_noise(SIZE, 4, rng)
    fine = wrapping_value_noise(SIZE, 8, rng)
    cols = [PATH["shadow"], PATH["base"], PATH["mid"], PATH["light"]]
    for y in range(SIZE):
        for x in range(SIZE):
            v = 0.58 * coarse[y][x] + 0.42 * fine[y][x]
            put(px, x, y, quantize(v, [0.32, 0.64, 0.85], cols))

    # Scattered pebbles (pale tops with a shadow pixel) and hairline cracks.
    for _ in range(4):
        cx, cy = rng.randrange(SIZE), rng.randrange(SIZE)
        put(px, cx, cy, PATH["spark"])
        put(px, cx, cy + 1, PATH["shadow"])
        if rng.random() < 0.5:
            put(px, cx + 1, cy, PATH["light"])
    scatter(px, rng, 3, PATH["dark"], [1, 2])  # cracks
    scatter(px, rng, 3, PATH["mid"], [1])
    return img


def fresh_guid():
    return uuid.uuid4().hex  # 32 lowercase hex chars


def write_meta(png_path, ref_text, used_guids):
    while True:
        g = fresh_guid()
        if g not in used_guids:
            used_guids.add(g)
            break
    text = re.sub(r"(?m)^guid: [0-9a-fA-F]{32}\s*$", f"guid: {g}", ref_text, count=1)
    with open(png_path + ".meta", "w") as f:
        f.write(text)
    return g


def main():
    with open(REF_META) as f:
        ref_text = f.read()
    used_guids = {re.search(r"(?m)^guid: ([0-9a-fA-F]{32})", ref_text).group(1)}

    jobs = [(f"grass_{i}", make_grass, i) for i in range(1, 5)]
    jobs += [(f"path_{i}", make_path, i) for i in range(1, 5)]

    made = []
    for name, fn, variant in jobs:
        img = fn(variant)
        out = os.path.join(TILES_DIR, name + ".png")
        img.save(out)
        g = write_meta(out, ref_text, used_guids)
        made.append((name, g))
        print(f"  {name}.png  guid={g}")

    assert len({g for _, g in made}) == len(made), "duplicate guid!"
    print(f"Generated {len(made)} tiles into {TILES_DIR}")
    return made


if __name__ == "__main__":
    main()
