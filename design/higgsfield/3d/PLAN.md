# 3D pipeline plan — concept v2 → Unity (drafted 5 Nov 2026, paused before any generation)

Status (5 Nov 2026, resumed): **generated**. 8 models made with Tripo H3.1 (81 credits: Pip 18 detailed, 7 × 9),
plus 2 regenerations of Pip / Benny with `texture_alignment: geometry` (18) to fix a ghost face painted on the back of
the head. GLBs in this folder, job ids in `jobs.tsv` and `../manifest.json` (ids `3d-*`). Post-processed with
`tools/postprocess.py` (Blender 5.2) into `unity/Assets/Models/<name>/` (.gltf + .bin + textures/); previews with
`tools/preview.py`. Found: Tripo's front comes out along ±X, so the yaw per model was set by eye (pip −90, benny +90,
snowman −90, log PCA-aligned to X, trees/rock 0) so that every file faces glTF +Z.

## Ghost-face fix (`tools/fixback.py`)
Tripo paints the front face onto the back of the head (both Pip and Benny, also with `texture_alignment: geometry`),
and for Benny the back-of-head faces share texels with the front eye, so painting the texture directly damaged the eye.
`fixback.py --remap` moves the back-facing faces inside a height zone (Pip 0.48–0.75 of the height = head + hat band,
Benny 0.50–0.76) onto free 32×32 blocks of the atlas filled with the median colour of the SIDE faces of the same height
slice, and flattens the normal map there. Run it on the post-processed .gltf; it re-exports the mesh (UVs changed) and
then overwrites the two textures (the glTF exporter rewrites textures from the original bytes, so the pixels are saved
after the export). Final files: `gltf/<name>/` here = `unity/Assets/Resources/Models/<name>/`. Known leftovers: Benny's
left eye has a big grey highlight straight from Tripo; the back of Pip's hat band has a faint colour seam where the
band edge crosses a height slice.

## Costs found (get_cost preflight, 5 Nov 2026)
| model | cost / job | note |
|---|---:|---|
| `tripo_h3_1_image_to_3d` (texture + pbr) | 9 | single image |
| `tripo_h3_1_multiview_to_3d` (texture + pbr) | 9 | 2–4 ordered views |
| `meshy_v7_image_to_3d` (texture + pbr) | 38 | too expensive for props |
| `multi_image_to_3d` (Meshy) | ? | preflight refused without media uploaded |

Proposed set (all Tripo H3.1): Pip (multiview: front, three-quarter, side, back) 9 ·
Benny (multiview) 9 · tree-a, tree-c, tree-d, rock-boulder, snowman, log (single image) 6 × 9 = 54
→ **72 credits of the 150 cap**. Headroom for ~8 retries at 9 each.
Face limits: Pip 12000, Benny 10000, trees 5000, rock 2500, snowman 3000, log 2500.

## Crops (`src/`)
Pip / Benny: x ranges of the four views on the 2528×1696 sheets, y 607–1555.
Props from p1-obstacles (2400×1792) grid: cols 0/624/1200/1776, rows 72–624 / 648–1176.
tree-b and rock-cluster are cropped but not in the first batch (only 3 tree variants + 1 rock are used by CourseView keys `tree:0..2`, `Rock`).

## Sizes to match (web units, measured from PenguinModel.cs / Props.cs)
- Pip: height ≈ 150 (pom top = hat y112 + 27 + r11.5); skis z −36…79; width ±43 (arms). Faces **local +z**
  (face parts at z 22–34; ski tip at z 71). PipView multiplies Root.localScale by 0.62 every frame.
- Benny: height ≈ 137 (head 82+28+15, ears), scale 0.62 × 0.9.
- Tree: height ≈ 146 (trunk 0–24, tiers 28…134, tip +12), radius ≈ 40; CourseView scales by o.S and yaws by (x·13+z) % 360.
- Rock: ≈ 48 × 27 × 40; Snowman: ≈ 79 tall; Log: 92 long × 22 dia (+5 snow), lies along x.

## Unity import plan (for the Opus implementation agent)
1. Add `com.unity.cloud.gltfast` to `unity/Packages/manifest.json`.
2. Post-process each GLB with Blender 5.2 CLI (`/Applications/Blender.app/Contents/MacOS/Blender -b --python …`):
   decimate to target, resize textures to 1024 (Pip/Benny) / 512 (props), apply transforms, origin at feet,
   front → +Z, export `.gltf` + `.bin` + external PNG so Unity's TextureImporter controls ASTC/max size.
3. Put results in `unity/Assets/Models/<name>/`.
4. `Art/GltfModels.cs`: load prefab, fit renderer bounds height to the target above, yaw correction constant per model,
   convert every material to `Pip/Lit` (_BaseMap, _BaseColor, _BumpMap + `_NORMALMAP`, _Smoothness = (1−roughness)·0.85).
5. `PenguinModel`: flag `Art.UseV2` (static, default on, overridable by `-pipArtV1` and a title chip later).
   V2 Pip = GLB under spin/inner/body groups; pose = Root squash, spin Flip/Spin, body Pitch/Lean/Bob/tuck.
   Still legacy / not on v2 yet: 8 expressions (face is baked in the texture), arm pump / wave, pole hide,
   ski carve rotation, scarf tail + pom swing, blink, eye look. Dizzy stars kept (they live on Root).
6. `Props.Build`: Tree/Rock/Snowman/Log take the GLB when `Art.UseV2`, else the procedural mesh.
7. Tests 58/58 + 3/3, simulator build (iPhone 17 Pro Max 6714FB90-A2AF-48EC-9BC6-147D96BBE76B), screenshot level 1,
   report fps + .app size (baseline 203 MB Debug-iphonesimulator).
8. Docs: GDD §18.m, UNITY.md, ASSET_LICENSES.md row (Tripo via Higgsfield, verify terms before shipping).

## Batch 2 (5 Nov 2026, later): in-game enemies + items, starter props for worlds 2–5
139 credits (balance 540.60 → 401.60): 2 concept images (Nano Banana Pro, 2 each: `full/e1-snow-puff.png`,
`full/e2-bear.png` — the p1 sheet had dropped the Snow Puff / Bear obstacles, and the cast sheets c08 "Puff" (a lamb) and
c06 "Tub" are friends, not these enemies) + 15 Tripo H3.1 single-image jobs at 9 each.
- In game now (copied to `unity/Assets/Resources/Models/`): snow-puff (Puff, ground disc cut with `--cut-below 0.06`),
  bear (bust, rises from the procedural mound), coin, shield, magnet, crystal, hourglass (= "slow" item). Crops in `src2/`.
- For worlds 2–5 (only in `gltf/`, NOT in Unity — those worlds are not in the Unity level table yet): mushroom, owl-stump
  (W2) · crystal-cluster, ice-bear (W3) · house-a, mailbox (W4) · ice-spire, tent (W5).
- Orientation: Tripo put the front on +X for all of these except the tent (−X) and the bear (+Y); yaws −90 / +90 / 180.
- Textures 512² for everything in this batch. Items are 900–1800 tris, props 2.4–4.8k.
