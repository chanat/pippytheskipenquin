# Pippy v2 — production report (phase A + phase B, 5 Nov 2026)

สรุปภาษาไทย: ผลิตครบตามบรีฟ — ภาพ 37 ชิ้นตามรายการ (ตัวละคร 15 + Pippy 2 แผ่น, โลก 10, ของประกอบฉาก 6, เอฟเฟกต์ 2, หน้าแรก/UI 2) และเพลง 11 แทร็ก ใช้เครดิตรวม 247.61 จากเพดาน 300 (คงเหลือ 639.60) สไตล์สุดท้ายคือ probe-0 (ดินปั้น/ผ้าขนด้าน) และ Pippy ดีไซน์ใหม่ pipsig-0 มี 2 ชิ้นที่ยังมีตัวหนังสือเล็กๆ ต้องลบมือ (c01p, w4g) ดูหน้า review.html

Open `review.html` for the gallery; `manifest.json` lists every generation (89 entries, credits sum = 247.61).
Produced by the Opus 5.5 production agent from BRIEF.md; art direction and the Pippy redesign by the
coordinating session.

## Credits
| Step | Spent | Balance after |
|---|---:|---:|
| Before phase A | | 887.21 |
| Phase A style probes (3 images) | 6.00 | 881.21 |
| B1 crop + upload + reference element | 0.00 | 881.21 |
| B2 first image pass, vinyl STYLE_LOCKED (24 images, all superseded) | 48.00 | |
| Coordinator's Pippy redesign (6 images, pipsig-*) | 12.00 | |
| B3 music, first batch (7 jobs; 4 more refused with 429, not charged) | 53.93 | 767.28 (checkpoint) |
| B2 matte pass (29 images) | 58.00 | |
| B3 music, retry (4 jobs) | 39.68 | 669.60 (checkpoint) |
| B2 Pippy-showing images (8) | 16.00 | |
| B2 quality-gate regenerations (7) | 14.00 | **639.60 (final)** |
| **Total for this pass (cap 300)** | **247.61** | headroom 52.39 |

Unit costs from `transactions`: Nano Banana Pro 2k = 2.00 per image; ElevenLabs Music v2.5 = 13.08 per 90 s, 0.73 per 5 s, 0.44 per 3 s. Music total 93.61.

## IDs
- Pippy reference element (current): `pip-v2-1`, element_id `73aeb99d-7e74-4b5c-b442-7700235125fd`, created from image job `b2fb1282-fa40-4cf2-9354-afa470d61d30` (pipsig-0) as type image_job, status completed, no upload needed. The server renamed it because `pip-v2` was already taken.
- Superseded element: `pip-v2`, element_id `060e6771-8066-47e4-a624-9b85f8720193`, made from the probe-1 crop (media_id `11a7ac28-1906-493b-80ed-d0cdf987e0ee`). Nothing showing Pippy was generated with it. It still exists in the workspace; delete or rename it there by hand if you want the plain name back.
- Style reference for the 15 cast sheets: probe-0 job `3dc2cd57-6929-4474-a84a-a13699050bca` (image_references).
- `full/c01-pip.png` is a byte copy of `full/pipsig-0.png`. History: probe-1 crop, then probe-0, then pipsig-0.

## Quality-gate regenerations (7 of 10 used, one per item)
| id | reason | result |
|---|---|---|
| c06 Tub | copied the reference hero's beanie and goggles (accessory bleed) | fixed |
| r05a Big Yeti | copied the reference hero's beanie, goggles and coral scarf | fixed |
| w1k Marshmallow Meadow key art | "START" lettering on arches, tiny skaters | fixed (a little less dense) |
| v2 Magic & Feedback VFX | text caption under every effect | fixed |
| p5 World 4 props | pseudo-text and numbers on menu boards | fixed (abstract bars only) |
| c01p Pippy ski poses | "FINISH" on the finish banner | regeneration worse (a caption under every pose, a repeated "carving left"); original kept; paint out banner text |
| w4g Lantern Village gameplay | pseudo-text on a stall sign, numbers on a menu board | regeneration kept (big signs blank); a small "MENU" remains on 2 chalkboards; paint out |

Rejected originals are kept as `*-rej1.png` (the c01p regeneration as `-rej2.png`) and marked `"rejected": true`.

## Superseded work (kept, marked rejected)
- Vinyl pass, 24 images (`*-vinyl.png`): the 15 cast sheets made with probe-1 as image_references ("style reference superseded"), plus 5 key arts and props p1–p4 made with the vinyl STYLE_LOCKED text. All were redone in matte. Redoing the 9 key-art and prop images (18 credits) was the agent's call for consistency.
- 3 gloss Pippy redesigns (pipsig-gloss-0/1/2): "style superseded by probe-0".

## Failures and deviations
- Tool error, verbatim, 4 times on the first generate_audio_batch call (m2, m3, m4, j3): `elevenlabs_music_v2_5 backend request failed (429): rate_limit_reached`. Not charged; the resubmission succeeded.
- `jobs_wait` reports `"model":"nano_banana_2"` for every image job, while billing says "Nano Banana Pro" at 2 credits.
- `sips --cropOffset 0 0` ignored the offset (centred crop); the probe-1 crop was redone with offset `1 0`. That file was later replaced.
- `<WORLD>` was filled with a lowercase description plus the tint (e.g. "a marshmallow meadow in pastel daylight with pink and mint tones"), so there are no proper-noun titles to letter.
- The leading "Pip, " was dropped from the Pippy description (no character names in prompts). The element placeholder carries his identity.
- t1 uses the full §2 descriptions of the three friends, without names. u1 keeps the brief's "P I P" logo letters. m1's "(2 variations)" became two jobs, m1a and m1b.
- Minor issues accepted without regeneration:
  - c05 Juni: has an extra red scarf.
  - c02: insets are in circular frames.
  - p2: one arch instead of separate start and finish arches.
  - r05a (regeneration): poles float beside the body, and the giant scale doesn't read.
  - w3g, w5g: camera in front of Pippy rather than behind him.
  - w4k: tiny vendor figures in the stalls.
  - p5: 8 houses, not 6.

## Look at these first
1. t1 Title key art: the new Pippy leading Mochi, Kuri and Tub. All on-model, no text.
2. c01x Pippy expressions: 8 readable expressions on the new design.
3. w1g Marshmallow Meadow gameplay: the cleanest read of the gameplay camera.
4. w4k Lantern Village key art: the strongest mood piece.
5. The cast set c02–r05b: c08 Puff and c09 Captain Walrus are the strongest; c10a/b and r05a/b are the A/B options still to decide.

Still open: paint the text out of c01p and w4g; decide c10a vs c10b and r05a vs r05b.

## 3D models (5 Nov 2026, later the same day)
Tripo H3.1 via Higgsfield: Pippy multiview detailed 18 · Benny multiview 9 · tree-a / tree-c / tree-d / rock-boulder /
snowman / log 6 × 9 · Pippy + Benny regenerations (texture_alignment geometry, rejected) 2 × 9 → **99 credits**, balance
639.60 → **540.60**. Details, crops, scripts and the Unity hand-over in `3d/PLAN.md`; manifest ids `3d-*`.
Tripo painted the front face onto the back of both heads (also with geometry alignment); fixed in Blender (`3d/tools/fixback.py`).
Batch 2 (same day): 2 enemy concept images (e1 Snow Puff, e2 bear; 2 each) + 15 Tripo single-image models (coin, shield,
magnet, crystal, hourglass, snow-puff, bear, mushroom, owl-stump, crystal-cluster, ice-bear, house-a, mailbox, ice-spire,
tent; 9 each) → **139 credits**, balance 540.60 → **401.60**. 3D total 238; grand total 485.61.
