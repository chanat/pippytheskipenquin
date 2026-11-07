# Pippy The Ski Penguin — Art & Music Direction v2 (Higgsfield brief)

สรุปภาษาไทย: เอกสารนี้คือ "คัมภีร์สไตล์" รอบใหม่ของเกม — ตัวละครเล็ก กลม สว่าง แบบของเล่น 3 มิติ
ฉากรายละเอียดเยอะ โทนสีคุมทั้งเกม และเพลงแนวสตริงบาโรก (แบบวิวัลดี) ผสมออร์เคสตราเกม JRPG
ส่วน prompt ด้านล่างเป็นภาษาอังกฤษเพราะเป็นภาษาที่โมเดลสร้างภาพ/เพลงเข้าใจดีที่สุด
กติกาลิขสิทธิ์ (GDD §19): ใช้ "สไตล์" เท่านั้น ห้ามใส่ชื่อเกม ตัวละคร หรือแฟรนไชส์ใดๆ ลงใน prompt

Budget: images 2 credits each (Nano Banana Pro 2k), music ≈ 13 credits per 90 s track.
Planned spend ≈ 200 credits of 887. Hard cap for this pass: **300 credits**.

---

## 1. Style bible

**Look.** Stylized 3D game art, "toy world" render: chunky rounded forms with soft beveled edges,
matte clay / plush / soft-plastic materials, bright saturated candy-pastel colours, soft global
illumination, pale-blue soft shadows (never black), a gentle cool rim light from behind, clean
readable silhouettes, no outlines, no photorealism, no grit.

**Characters.** Chibi: head as big as the body, short stubby limbs, round tummy, big round glossy
eyes with two highlights, rosy cheeks, tiny beak/snout, one signature accessory each. Everyone
rides tiny skis with tiny poles. Expressions are broad and readable from far away.

**Worlds.** Dense but tidy detail: every scene has 10+ small props (cottages, flags, lanterns,
snowmen, critters) arranged along a winding slope; distance fades to pastel haze; sky is a
gradient with soft blobby clouds. One palette for the whole game, with a per-world tint.

**Palette anchors (GDD §12).** snow `#F7FBFF` · ice `#CDEBFF` · mint sky `#BDF2E9` · berry
`#D6336F` · blush `#FF9EC4` · sunny `#FFC93C` · glacier `#2A6FC4` · pine `#23845A` · ink `#1F2A4D`.

**Per-world tint.** W1 pastel daylight pink-mint · W2 pine green + orange afternoon light ·
W3 ice blue + crystal purple · W4 navy night + warm orange lanterns · W5 aurora green-purple.

**Never in a prompt:** any game, console, studio, franchise or character name; real brand names;
text labels, numbers, watermarks, logos.

### Prompt building blocks

```
STYLE = "Stylized 3D game-art render in a chunky toy-like style: soft rounded forms with beveled
edges, matte clay and plush materials, bright saturated candy-pastel palette, soft global
illumination with pale-blue soft shadows and a gentle cool rim light, clean readable silhouettes,
no outlines, no text, no watermark, no logo. Palette anchors: snow #F7FBFF, ice #CDEBFF, mint sky
#BDF2E9, berry pink #D6336F, blush #FF9EC4, sunny yellow #FFC93C, glacier blue #2A6FC4, pine green
#23845A, ink navy #1F2A4D."

CHIBI = "chibi proportions with the head as big as the body, short stubby limbs, round tummy, big
round glossy eyes with two highlights, rosy cheeks, cheerful and friendly"

SHEET = "Character turnaround sheet on a plain pale-blue #CDEBFF studio background: front view,
three-quarter view, side view and back view of the same character standing on tiny skis holding
tiny ski poles, plus two small inset head expressions (happy, surprised); even spacing, no text."
```

Model: `nano_banana_pro`, `resolution: "2k"`. Aspect: sheets 3:2 · world key art 16:9 ·
gameplay mocks 9:16 · prop / VFX sheets 4:3 · title key art 3:2 · UI mood 9:16.

### Locked style (after the phase-A probe, 5 Nov 2026)

The user chose probe 0 (full/probe-0.png): the matte clay / plush look exactly as §1 describes.
From here on every prompt uses `STYLE_LOCKED` instead of `STYLE`:

```
STYLE_LOCKED = STYLE + " No colour swatches, palette chips, reference bars or frames anywhere in
the image."
```

Consistency: probe-0 (job 3dc2cd57-6929-4474-a84a-a13699050bca) is the style reference passed as
`image_references` for every other character sheet, so the whole cast shares its rendering style
and sheet layout. Pippy himself was redesigned with signature features so he is not confusable with
existing penguin mascots (three variants, full/pipsig-0..2.png; the glossy pipsig-gloss-* set was
made before the style decision and is superseded). The user chose variant A — pipsig-0, job
b2fb1282-fa40-4cf2-9354-afa470d61d30 — which is now full/c01-pip.png and the "pip-v2" reference
element used by every image that shows Pippy. Its description is the c01 row in §2.

---

## 2. Characters (16 sheets, 3:2)

Each prompt = `SHEET + " " + <description> + ", " + CHIBI + ". " + STYLE`.

| id | name | description |
|---|---|---|
| c01 | Pippy (hero) — v2 design chosen 5 Nov (variant A) | a small penguin with a deep teal-navy body and cream belly, a heart-shaped white face patch with a navy widow's peak between the eyes, a light-blue knitted beanie with a white snowflake-pattern band and a two-tone pink-and-yellow pompom, mint ski goggles pushed up on the beanie, a chunky candy-striped scarf in berry pink and white, three tiny freckles on each rosy cheek, small orange beak and feet, yellow skis with a berry-pink stripe |
| c02 | Mochi | a white plush bunny with long floppy ears, pink earmuffs, pink button nose, pink mittens, shy smile |
| c03 | Kuri | a shiba-inu puppy, brick-orange fur with cream cheeks and chest, curled tail, yellow ski goggles on the forehead, green bandana, excited open-mouth grin |
| c04 | Nuri | a red-panda cub, russet fur with a cream face mask, big ringed fluffy tail, berry-and-white striped scarf, curious explorer look |
| c05 | Juni | an arctic-fox kit, snow-white fur with pale-blue inner ears, big fluffy tail, mint knit hat with earflaps, clever confident smile |
| c06 | Tub | a round polar-bear cub, cream fur, very round tummy, red scarf, gentle sleepy-happy face |
| c07 | Taro | a capybara, warm brown fur, half-closed chill eyes, small yuzu-orange bucket hat with a leaf, relaxed smile |
| c08 | Puff | a lamb with cloud-like fluffy white wool and a lavender face, a star-topped nightcap, drowsy content expression |
| c09 | Captain Walrus | a plump friendly walrus, grey-brown, small rounded tusks, white captain's hat, brown leather mail satchel with a snowflake badge, warm grandpa smile |
| c10a | Yeti Jr. (option A — NOT chosen, 5 Nov) | a small fluffy snow-creature kid, white shaggy fur, pale-blue round face, rainbow striped scarf, big innocent eyes |
| c10b | Mammoth Jr. (option B — CHOSEN by the user, 5 Nov) | a baby woolly mammoth, lavender-grey fluff, tiny curled trunk, small rounded tusks, rainbow striped scarf, big innocent eyes |
| r01 | Benny (rival W1) | a beaver, chocolate-brown fur, big flat paddle tail, two cute front teeth, red racing goggles, a plain striped racing bib, competitive grin |
| r02 | Ollie (rival W2) | a round owl, mocha and cream feathers, very large thoughtful eyes behind round spectacles, tiny bow tie, feather tufts |
| r03 | Pearl (rival W3) | a sleek seal, silver-grey with a pearly sheen, a speedy streak-patterned scarf, confident smirk, racing pose |
| r05a | Big Yeti (boss W5, option A — NOT chosen, 5 Nov) | a tall gentle giant snow-creature, snow-white shaggy fur, pale-blue face, shy kind smile, slightly hunched as if lonely, holding a tiny gift |
| r05b | Big Mammoth (boss W5, option B — CHOSEN by the user, 5 Nov) | a tall gentle giant woolly mammoth, lavender-grey shaggy fur, big curled trunk, shy kind smile, holding a tiny gift |

Extra Pippy sheets (after the style is locked, with the Pippy element):
- c01x **Expressions**: "Expression sheet of <<<PIP>>>: 8 head-and-shoulders portraits in a 4×2 grid on pale-blue #CDEBFF: happy, excited, surprised, scared, dizzy, pouting, sleepy, proud. No text. " + STYLE
- c01p **Ski poses**: "Action pose sheet of <<<PIP>>> on skis on a white snow ground: carving left, carving right, tucked speed crouch, mid-air jump, backflip, tumbling in a snowball, cheering at the finish; dynamic snow spray. No text. " + STYLE

---

## 3. Worlds (5 × 2 images)

Key art (16:9): `"Wide key-art environment painting of a snowy ski slope winding downhill through <WORLD>, viewed from slightly above, no characters; dense charming detail: <DETAILS>; soft pastel haze in the distance, gradient sky with soft blobby clouds. " + STYLE`

Gameplay mock (9:16, uses the Pippy element): `"In-game view for a portrait mobile ski game: camera high behind a tiny <<<PIP>>> skiing straight down a wide snowy slope through <WORLD>, the slope fills the lower two thirds of the frame, slope edges lined with <EDGE>; on the slope a few obstacles and candy-striped answer gates; <DETAILS>; no UI, no text. " + STYLE`

| id | world | tint | DETAILS | EDGE |
|---|---|---|---|---|
| w1 | Marshmallow Meadow | pastel daylight, pink and mint | rolling snow hills shaped like marshmallows, candy-striped slalom gates, snowmen wearing berry-pink hats, cozy cottages with smoking chimneys, pine trees with round snow blobs, bunting flags, a hot-cocoa stand, a frozen pond, a start arch, distant pastel mountains | snow-blob pine trees and snowmen |
| w2 | Whispering Pines | golden afternoon light, pine green and orange | tall snowy pines, hanging paper lanterns and string lights, wooden bridges and log fences, owl nests, a frozen waterfall, red-capped mushrooms, a timber lodge, soft light shafts through mist | tall pines with lanterns and log fences |
| w3 | Crystal Lake | ice blue and crystal purple | a frozen lake with fish visible under clear ice, giant glowing ice crystals, crystal arches over the trail, icy caves, snowdrifts with sparkle, ice sculptures of animals, pastel purple sky | glowing ice crystals and sculptures |
| w4 | Lantern Village | night, navy sky with warm orange lantern light | strings of glowing paper lanterns, cozy houses with warm windows and snowy roofs, red mailboxes, festival food stalls, fireflies, a big moon, footprints in the snow | lantern posts and cozy houses |
| w5 | Aurora Peak | aurora green and purple over deep blue | a mountain summit under curtains of aurora, ice spires, a festival stage with flags, fireworks, colourful tents, a brass telescope lookout, a bonfire | ice spires and festival tents |

---

## 4. Prop sheets (6 × 4:3)

Template: `"Game prop sheet on a plain pale-blue #CDEBFF background, items evenly spaced in a grid, each shown from a three-quarter view, no text: <ITEMS>. " + STYLE`

| id | ITEMS |
|---|---|
| p1 obstacles | four snow-blob pine trees of different shapes, two rounded boulders, a snowy log, a snowman with a berry hat, a big round snow puff ball, a wooden jump ramp, a slippery ice patch, an ice hole with a cracked rim |
| p2 gates & items | three answer-gate styles (candy-striped poles with a banner, a wooden signpost gate, an ice-crystal gate), a start arch and a finish arch with bunting, a golden snowflake coin, a shield bubble, a horseshoe magnet, a glowing power crystal, a slow-motion hourglass, a heart |
| p3 world 2 | hanging paper lanterns, a wooden bridge, red-capped mushrooms, an owl nest on a stump, a log fence, a timber lodge, a frozen waterfall |
| p4 world 3 | crystal clusters in three sizes, a crystal arch, ice sculptures of a fish and a bear, a frozen-lake tile with fish under the ice, an icy cave entrance |
| p5 world 4 | six cozy snow-roofed houses of different shapes, a red mailbox, a festival food stall, a lantern string on posts, a stone well, a sled |
| p6 world 5 | ice spires in three sizes, a festival stage with flags, colourful tents, a brass telescope, a bonfire, a firework launcher |

---

## 5. VFX sheets (2 × 4:3)

Template: `"VFX concept sheet on a dark navy #1F2A4D background, each effect drawn as a three-frame sequence left to right, stylized soft particle shapes, no text: <FX>. " + STYLE`

| id | FX |
|---|---|
| v1 snow & motion | ski snow spray, a landing snow puff, a snow burst from a smashed snow ball, speed-boost trail streaks behind a skier, ski-track grooves in snow, wind speed lines |
| v2 magic & feedback | twinkling sparkle stars, a golden snowflake-coin pickup burst, a glowing power-crystal aura, a green correct-answer ring of light, a soft wrong-answer puff, finish-line confetti, aurora shimmer |

---

## 6. Title & UI (2 images)

- t1 title key art (3:2, Pippy element): `"Title key art: <<<PIP>>> leading three small animal friends (a white bunny with pink earmuffs, a shiba puppy with yellow goggles, a round polar-bear cub with a red scarf) skiing down a sunny marshmallow-shaped snow slope toward the viewer, snow spray, bunting flags, pastel mountains, blue sky, joyful; room at the top for a logo; no text. " + STYLE`
- u1 UI mood (9:16): `"Mobile game title-screen mock-up: a chunky candy-style logo made of the three letters P I P in rounded pink, yellow and blue letters at the top, below it big pill-shaped glossy buttons in berry pink and glacier blue with a dark bottom edge, round ice-panel cards, thick rounded icons, a snowy pastel 3D backdrop; playful toy UI. " + STYLE`

---

## 7. Music (ElevenLabs Music v2.5, `force_instrumental: true`)

Direction: baroque string ensemble energy (Vivaldi-like Allegro ostinatos, harpsichord, pizzicato)
crossed with a warm orchestral game-adventure score (soaring melodies, horns, choir, timpani,
glockenspiel). Always original melodies. 90 s, loop-friendly endings. Clean modern mix.

| id | track | use | prompt |
|---|---|---|---|
| m1 | Snow Day! | menu + world 1 | Bright playful orchestral instrumental in C major at 150 BPM: a baroque-style string ensemble drives a sparkling Allegro ostinato with harpsichord, glockenspiel and pizzicato, a light flute counter-melody, then a soaring main theme with warm horns and a touch of choir; cheerful winter adventure game music for a family audience, clean mix, loop-friendly ending (2 variations) |
| m2 | Pine Trail | world 2 | Adventurous forest-journey orchestral instrumental in G major at 132 BPM with a gentle shuffle: pizzicato cellos, bassoon and oboe melody, French horns, baroque violin runs, tambourine and light percussion, golden-hour mood, loop-friendly ending |
| m3 | Crystal Glide | world 3 | Shimmering orchestral instrumental in D major at 120 BPM: harp and celesta arpeggios, sparkling high violins, music box, soft choir pads, baroque string figures flowing like skating on ice, crystalline and serene, loop-friendly ending |
| m4 | Lantern Lullaby | world 4 | Warm cozy night waltz instrumental in F major at 108 BPM in 3/4: string ensemble, oboe melody, music box, soft harpsichord, gentle glockenspiel, lantern-lit village feel, tender, loop-friendly ending |
| m5 | Aurora Rush | world 5 | Driving Presto orchestral instrumental starting in A minor and lifting into C major at 168 BPM: storm-like baroque string runs, timpani, brass fanfares, choir, triumphant modulation at the climax, thrilling but never dark, loop-friendly ending |
| m6 | Rival Showdown | boss / rival levels | Playful racing orchestral instrumental in A minor at 170 BPM: fugue-like chasing violin runs over harpsichord, staccato brass hits, snare rolls, cheeky tension, energetic and cute, never scary, loop-friendly ending |
| j1 | Level Clear | jingle 5 s | Short triumphant orchestral fanfare, 5 seconds: strings, brass and glockenspiel, bright major resolution, celebratory |
| j2 | Star | jingle 3 s | Short two-note sparkle, 3 seconds: celesta and high violins with a shimmer, bright and magical |
| j3 | Fall | jingle 3 s | Short soft comedic descending phrase, 3 seconds: pizzicato strings and a gentle bassoon slide, kind and funny, not sad |
| j4 | Question | sting 3 s | Short curious harpsichord flourish with a questioning upward string lift, 3 seconds |

---

## 8. Deliverables layout

```
design/higgsfield/
  BRIEF.md            this file
  manifest.json       every generation: id, category, name, prompt, model, job_id, url, file, thumb, credits
  REPORT.md           credits spent, failures, picks, what to look at first
  full/<id>-<name>.png|jpg|mp3   full-size results
  thumbs/<id>-<name>.jpg         ≤ 1024 px JPEG for the review page
  review.html         one self-contained gallery page (relative paths), sections: Characters,
                      Worlds, Props, VFX, Title & UI, Music; caption = id + name + Thai label
```
