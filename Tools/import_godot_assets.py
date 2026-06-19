#!/usr/bin/env python3
"""
One-off importer: copy the 0x72 Dungeon Tileset II PNGs out of the Godot
project and into an organized Assets/Art tree in the Unity project, writing a
hand-authored .meta for each so Unity imports them as crisp pixel-art sprites
(16 PPU, Point filter, no mipmaps, no compression).

Idempotent: re-running overwrites copies + metas with stable (path-derived) GUIDs.
"""
import hashlib
import shutil
from pathlib import Path

SRC = Path("/Users/benz/Documents/DungeonDashGodot/assets")
DST_ROOT = Path("/Users/benz/Documents/dungeon-dash/Assets/Art")

# Authoritative playable roster (from Godot PlayerData.gd CHARACTER_DEFS)
CHARACTERS = {"knight", "elf", "dwarf", "lizard", "wizzard", "doc"}
ITEMS = {"bomb", "chest", "coin", "potion"}

def category_dest(folder_name: str) -> Path:
    """Map a source asset folder to its destination under Assets/Art."""
    if folder_name in CHARACTERS:
        return DST_ROOT / "Characters" / folder_name
    if folder_name == "tiles":
        return DST_ROOT / "Tiles"
    if folder_name == "weapons":
        return DST_ROOT / "Weapons"
    if folder_name in ITEMS:
        return DST_ROOT / "Items" / folder_name
    if folder_name == "ui":
        return DST_ROOT / "UI" / "hearts"
    if folder_name == "button":
        return DST_ROOT / "UI" / "button"
    # Every other creature folder is a non-playable actor.
    return DST_ROOT / "Enemies" / folder_name

# Standard Unity 6 sprite .meta tuned for pixel art. __GUID__ is substituted.
META_TEMPLATE = """fileFormatVersion: 2
guid: __GUID__
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 16
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: __SPRITEID__
    internalID: 0
    vertices: []
    indices:\x20
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  spritePackingTag:\x20
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

def folder_guid(rel: str) -> str:
    return hashlib.md5(("folder:" + rel).encode()).hexdigest()

FOLDER_META = "fileFormatVersion: 2\nguid: __GUID__\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\x20\n  assetBundleName:\x20\n  assetBundleVariant:\x20\n"

def write_folder_meta(folder: Path):
    """Unity needs a .meta for every folder too."""
    rel = folder.relative_to(DST_ROOT.parent.parent).as_posix()  # relative to project root
    meta = folder.with_suffix(folder.suffix + ".meta") if folder.suffix else Path(str(folder) + ".meta")
    guid = folder_guid(rel)
    meta.write_text(FOLDER_META.replace("__GUID__", guid).replace("{{", "{").replace("}}", "}"))

def png_meta(rel_assets_path: str) -> str:
    guid = hashlib.md5(("png:" + rel_assets_path).encode()).hexdigest()
    sprite_id = hashlib.md5(("sprite:" + rel_assets_path).encode()).hexdigest()
    return META_TEMPLATE.replace("__GUID__", guid).replace("__SPRITEID__", sprite_id)

def main():
    if not SRC.exists():
        raise SystemExit(f"Source not found: {SRC}")
    DST_ROOT.mkdir(parents=True, exist_ok=True)

    counts = {}
    created_dirs = set()
    total = 0

    for sub in sorted(p for p in SRC.iterdir() if p.is_dir()):
        dest = category_dest(sub.name)
        dest.mkdir(parents=True, exist_ok=True)
        created_dirs.add(dest)
        cat = dest.relative_to(DST_ROOT).parts[0]
        for png in sorted(sub.glob("*.png")):
            target = dest / png.name
            shutil.copy2(png, target)
            rel = target.relative_to(DST_ROOT.parent.parent).as_posix()  # Assets/Art/...
            (Path(str(target) + ".meta")).write_text(png_meta(rel))
            counts[cat] = counts.get(cat, 0) + 1
            total += 1

    # Folder metas for every directory we created (and their parents under Art).
    all_dirs = set()
    for d in created_dirs:
        cur = d
        while cur != DST_ROOT.parent:  # stop above Assets/Art's parent (Assets)
            all_dirs.add(cur)
            cur = cur.parent
    for d in sorted(all_dirs):
        write_folder_meta(d)

    print(f"Imported {total} sprites into {DST_ROOT}")
    for cat in sorted(counts):
        print(f"  {cat:12s} {counts[cat]:4d}")

if __name__ == "__main__":
    main()
