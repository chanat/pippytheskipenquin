# Pippy The Ski Penguin — 2D art direction (v3) and Unity 2D plan

สรุปภาษาไทย (5 พ.ย. 2026): ผู้ใช้ดูแล้วว่า Pippy 3D ในเกมดูไม่ออกเป็นเพนกวิน และเกมแนวนี้ไม่จำเป็นต้องเป็น
3 มิติ → เปลี่ยนงานภาพทั้งหมดเป็น **2D สไตล์การ์ตูนเส้นหนา สีจัด** (ผู้ใช้อ้างอิงแนว Angry Birds — ใช้เป็น
"แนว" เท่านั้น ห้ามใส่ชื่อเกม/ตัวละครของคนอื่นใน prompt ตาม GDD §19) · Pippy = เพนกวินกลมๆ ไม่ใส่ชุดสกี
มีแค่รองเท้าสกี · ผู้ใช้เลือกสไตล์ "เส้นขอบหนา" (probe `pip2d-0`) · แพลตฟอร์ม iOS + Android →
**อยู่กับ Unity แต่เปลี่ยนตัววาดภาพเป็นสไปรต์ 2D** (เก็บกติกาเกม HUD เสียง เซฟ เทสต์ไว้ทั้งหมด)
งาน 3D (design/higgsfield) เก็บไว้เป็นคลัง · ชั้น 3D และโมเดล glTF เอาออกจาก Unity แล้ว 5 พ.ย. 2026 (เหลือแค่คลังใน design/higgsfield)

---

## 1. Style bible (2D)

```
STYLE_2D = "2D mobile game art in a bold cartoon vector style: thick dark navy #1F2A4D outlines,
glossy rounded shapes, flat shading with one simple highlight and one shadow tone, saturated candy
colours (snow #F7FBFF, ice #CDEBFF, mint sky #BDF2E9, berry #D6336F, blush #FF9EC4, sunny #FFC93C,
glacier #2A6FC4, pine #23845A, ink #1F2A4D), exaggerated expressive eyes and eyebrows, clean
readable silhouettes at small size. No text, no numbers, no watermark, no logo, no colour swatches."

PIP_2D = "a simple ball-shaped penguin with no clothing at all — no hat, no scarf, no goggles —
wearing only chunky red ski boots clipped onto short yellow skis and holding tiny ski poles with its
flippers; deep teal-navy body, cream belly, a heart-shaped cream face patch with a small navy
widow's peak between the eyes, three spiky feather tufts on top of the head, big round glossy eyes
with thick expressive eyebrows, a small orange beak, rosy cheeks with three freckle dots"

VIEW = "seen from the front and slightly from above (three-quarter top-down game camera), skiing
down the slope toward the viewer"
```

Locked reference: `design/higgsfield/full/pip2d-0.png` (job `9f83897f-4135-412e-a1e1-e554ede1bd69`)
— pass it as `image_references` on every sheet ("match the line weight, shading and colours of the
reference exactly") and register it as the reference element "pip-2d" for sheets that show Pippy.

Sheet rules: plain pure-white background (sprites) or pure-black background (VFX, used additively);
items evenly spaced with generous empty padding between them (they are auto-sliced); each item a
complete, unclipped silhouette; consistent light from the upper left; no cast shadows except a soft
elliptical ground shadow drawn as part of the sprite only where listed.

Model: `nano_banana_pro`, resolution 2k. Budget cap for this pass: **120 credits** (≈2/image +
background removal). Then `remove_background` (Higgsfield) on every white-background sheet to get
transparent PNGs; VFX sheets stay on black (additive blending in Unity ignores black).

## 2. Sprite sheets

| id | aspect | contents |
|---|---|---|
| s01 pip-ski-a | 3:2 | Pippy (PIP_2D, VIEW, element) in 6 ski poses: skiing straight; carving left (leaning, snow spray at the outside ski); carving right; boost tuck (crouched low, poles back); jump (airborne, skis apart, flippers up); mid-backflip (upside down, skis up) |
| s02 pip-ski-b | 3:2 | Pippy in 6 more poses: tumbling rolled up in a snowball (only face and boots poking out); sitting dizzy with spiral eyes; cheering at the finish (flippers up, big grin); scared (eyes wide, braced); proud with chest out; idle wave facing the viewer (title screen) |
| s03 benny | 3:2 | Benny the rival: a chocolate-brown beaver with a big flat paddle tail, two front teeth, red racing goggles on his forehead, a plain red-and-white striped racing bib, red ski boots on green skis, same style and VIEW; 4 poses: skiing straight, carving left, carving right, cheering |
| s04 obstacles-a | 4:3 | VIEW props: four snow-covered pine trees of different shapes and heights, two rounded grey-blue boulders with snow caps, a snowy log lying across the slope, a snowman with a berry-pink hat |
| s05 obstacles-b | 4:3 | VIEW props: a big round snow puff ball; a rolling snowball with motion lines; a bear hiding in a snow mound (two states: closed mound, bear popped up grinning); an ice hole with a cracked frozen rim (top-down oval, dark water); a slippery ice patch (top-down oval, pale blue with sparkle); a wooden jump ramp |
| s06 items-gates | 4:3 | a golden snowflake coin (front view); a shield bubble; a red horseshoe magnet; a glowing cyan power crystal; a slow-motion hourglass; a red heart; candy-striped answer-gate poles with a blank rounded banner between them, in four colourways (berry, glacier, pine, sunny); a start/finish arch with bunting flags and a blank banner |
| s07 vfx | 4:3, **black background** | white snow spray puffs in three sizes; two four-point sparkle stars; a golden coin-burst ring; four confetti pieces in berry/sunny/glacier/mint; two pale-blue speed streaks; a landing poof; a soft grey wrong-answer puff — flat cartoon shapes, no outlines on the glows |
| b1 backdrop-w1 | 16:9 | the top band of the screen for world 1: pastel pink-and-mint sky with soft blobby clouds, a row of rounded snowy mountains with marshmallow-like hills in front, tiny cottages and pine trees along the horizon, no characters, STYLE_2D |
| b2 snow-tile-w1 | 1:1 | a seamless top-down snow ground texture tile: very low-contrast soft snow with faint marshmallow-shaped bumps and a few sparkle dots, must tile on all four edges, STYLE_2D colours only (snow / ice) |
| b3 edge-strip-w1 | 9:16 | a vertical strip of the slope's left edge for world 1: snow banks with snow-covered pine trees, a snowman, bunting flags and a cocoa stand seen from VIEW, designed to repeat top-to-bottom (same snow line at the top and bottom edges), empty slope on the right third, STYLE_2D |
| t2 title-2d | 3:2 | title key art: Pippy (element) waving on a sunny marshmallow slope with bunting flags, pastel mountains, room at the top for a logo, STYLE_2D |

Worlds 2–5 backdrops/tiles/edges: later, when those worlds exist in the game.

## 3. Deliverables

```
design/sprites2d/
  BRIEF-2D.md         this file
  manifest.json       every generation: id, prompt, model, job_id, url, file, cutout, credits
  REPORT.md           credits, regenerations, what to look at first
  sheets/<id>.png     raw results
  cutouts/<id>.png    transparent versions (remove_background), VFX left on black
  thumbs/<id>.jpg     ≤1024 px
  review2d.html       one gallery page (relative paths; sections ตัวละคร / ของประกอบฉาก / เอฟเฟกต์ / ฉาก / หน้าแรก)
```

## 4. Unity 2D plan (for the engineering pass, after the music agent finishes)

Keep untouched: `Core/` (RunSim, Levels, Questions, Progress), `Hud.cs` + `Ui/` (UGUI screens,
Kit), `AudioService`, `SaveData`, the screen flow and input in `GameBoot`, all tests.

Replace the 3D scene layer with a sprite layer, same world units (the web's css px, 390 wide):
- Projection = the web 2D prototype (`prototype/web/js/render.js`): Pippy's screen y = 0.37·H;
  object screen y = SY + (z − pz) (ahead = lower on screen, scrolling up); x = W/2 + x·persp(y)/persp(SY);
  sprite scale = persp(y) = 0.82 + 0.3·y/H; sky band at the top (SKY_H) with a crest; objects fade
  in just below the crest. Orthographic camera; design space 390×844 scaled to the screen.
- New views: `Backdrop2D` (b1 sprite + parallax clouds drifting with Pippy's x), `Slope2D` (b2 tiled
  ground scrolling with pz, b3 edge strips tiled vertically left/right, right side mirrored),
  `Course2D` (one SpriteRenderer per CourseObj, pooled; sorted by screen y so nearer = on top; gates
  with the question banner text drawn by UGUI as today), `Pip2D` (pose sprite picked from the sim
  state: straight / carve L / carve R / tuck (BoostLevel) / jump / flip (FlipA) / tumble / cheer /
  scared / dizzy / proud; squash-and-stretch by scale; a soft ground shadow sprite; ski trail as a
  2D LineRenderer pair), `Particles2D` (the existing ParticlesView logic emitting s07 sprites,
  additive material for sparkles/streaks), Benny from s03.
- Rendering: SpriteRenderer + Sprites/Default (unlit) in the existing URP forward renderer; the
  post-processing Volume off for the 2D camera (crisp lines); no lights.
- Import: `unity/Assets/Sprites/<world>/…` from design/sprites2d/cutouts, auto-sliced in an editor
  script (TextureImporter Multiple + automatic rectangles, then named), pixels-per-unit = 1 css px,
  SpriteAtlas per sheet, ASTC on mobile.
- The 3D code first stayed behind `-pipArt3d` for comparison; it was removed from the project on
  5 Nov 2026 (the archive lives only in design/higgsfield).
- Title screen: 2D Pippy idle wave (s02) instead of the RawImage/MapPip camera; map screen unchanged.
- Tests: EditMode unchanged + a projection test (persp / toX / toY match the web numbers);
  PlayMode smoke tests must stay green; captures to `$PIP_SHOT_DIR`.

---

## 1b. Pippy v4 — เพนกวินดำคลาสสิก ตากลมเล็กสีขาว (5 พ.ย. 2026 ช่วงบ่าย)

ผู้ใช้ดู Pippy v3 ในเกมแล้วขอ "ตากลมๆ เล็กๆ สีขาว ทำให้เรียบง่าย สีสวยสดใส ตัวเล็กกลม" → ทำ probe 3 แบบ
(`probes-v4/pipv4-A` น้ำเงินเข้ม · `B` ฟ้าสด · `C` ดำคลาสสิก) ผู้ใช้เลือก **C** · เส้นขอบบางลงจาก v3 เล็กน้อย
(medium-weight) แต่ยังเป็นการ์ตูนเส้นขอบเข้ม สีแบน สดใส

```
STYLE_V4 = "clean kawaii cartoon style with medium-weight dark outlines, flat bright saturated colours,
soft simple shading, very simple rounded shapes, minimal detail"

PIP_V4 = "a tiny ball-shaped classic black penguin with a big white belly and white face patch, small round
white eyes with black dot pupils (no eyebrows), a small orange beak, pink cheeks, one small feather tuft,
wearing only chunky red ski boots clipped on short yellow skis and holding tiny ski poles — no hat, no
scarf, no goggles"
```

Reference element **pip-v4** = `0239c242-a5ee-46e2-8685-47ec565ece2b` (สร้างจาก job `74639977-1d65-4bb6-8f08-d08216b9dd94`,
ไฟล์ `probes-v4/pipv4-C.png`) ใช้ `<<<0239c242-a5ee-46e2-8685-47ec565ece2b>>>` ในทุก prompt ที่มี Pippy ·
เบนนี่ใช้ภาพ C เป็น `image_references` ให้สไตล์ตรงกัน

แผ่นที่ทำใหม่ (ชื่อไฟล์เดิมเพื่อให้ `SpriteImport` อ่านได้ทันที · v3 ย้ายไป `sheets/v3/`, `thumbs/v3/`, `cutouts/v3/`):

| id | ไฟล์ | ลำดับหั่น (reading order) |
|---|---|---|
| s01-v4 pip-ski-a | `sheets/s01.png` | 0 straight · **1 straightB** (ย่อเข่า เฟรม 2 ของ ski-pump) · 2 carveL · 3 carveR · 4 tuck · 5 jump · 6 flip — ละอองหิมะของท่าเลี้ยวเป็นเกาะแยก |
| s02-v4 pip-ski-b | `sheets/s02.png` | 0 tumble · 1 dizzy (ดาว 3 ดวงเป็นเกาะแยก) · 2 cheer · 3 scared · 4 proud · 5 wave |
| s03-v4 benny | `sheets/s03.png` | 0 straight · 1 carveL · 2 carveR · 3 cheer |

บทบาทใหม่ใน `SpriteTable`: `pip.straightB` (Pip2D สลับกับ `pip.straight` ครึ่งหลังของรอบ ski-pump) · `pip.jumpB` ไม่มีแล้ว
(ชี้ไป jump เดียวกัน) · `pip.wave` = s02 index 5 · ตอน import ต้องรวม/ทิ้งเกาะเล็ก (ละอองหิมะ ดาว) ให้จำนวน slice = 7 / 6 / 4

เครดิตรอบนี้: probe 3 × 2 + s03 2 + s01 2 (+ rej 2) + s02 2 = **14** · คงเหลือ ≈ 343.6
ของประกอบฉาก/ไอเทม/ประตู/VFX/ฉาก (s04–s07, b1–b3) ยังเป็นชุด v3 — สไตล์เข้ากันได้ (เส้นขอบเข้ม สีแบน) ไม่ทำใหม่

Import notes (5 Nov 2026): `SpriteImport.RunCharacters` (re-keys and slices only s01–s03) gave exactly 7 / 6 / 4 slices with the existing island rule (islands under 5.5 % of the largest merge into the nearest figure within 3 % of the sheet width ≈ 61 px — sprays and the dizzy stars fold in; dust < 6 px dropped). By eye the carve poses are the other way round from the table above: s01_2 skis right with the spray behind on the left (→ `pip.carveR`), s01_3 skis left (→ `pip.carveL`); Benny as listed (s03_1 carveL, s03_2 carveR). `pip.jumpB` removed. Placeholder colour for missing Pippy slices #2A2A2E.

**b3-v4 (5 พ.ย. 2026 บ่าย)**: ผู้ใช้ถามว่า "สีแดงนี่มันอะไร อย่างกับโต๊ะ" — คือซุ้มโกโก้ในขอบลาน b3 ที่ถูกครอป 56 % ตอนนำเข้าจนเหลือแต่หลังคาแดงกับขาไม้ → ทำ b3 ใหม่
(`sheets/b3.png`, v3 ย้ายไป `sheets/v3/b3.png`, job `5962b5a5-3245-411d-b8b0-4a584ef89942`, 2 เครดิต) โดยห้ามมีสิ่งปลูกสร้างทุกชนิด เหลือต้นสน ตุ๊กตาหิมะ ธง พุ่มไม้
บทเรียน: ของที่มี "ขา/เสา" ในขอบลานจะโดนครอปแล้วอ่านไม่ออก — ขอบลานควรมีแต่ของทรงกลมทึบ (ต้นไม้ หิน ตุ๊กตาหิมะ พุ่ม)
