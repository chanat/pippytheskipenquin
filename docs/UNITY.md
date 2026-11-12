# Pippy The Ski Penguin — Unity project

Unity 6 (6000.6.8f1) + URP 17.6 · project folder: [`unity/`](../unity/) · decided 4 Nov 2026 (GDD §14)

## Layout

| Folder | What it is |
|---|---|
| `Assets/Scripts/Core/` (`Pip.Core`, no UnityEngine refs) | Game rules ported line-for-line from the web prototype: `Rng` (mulberry32), `Questions` (+ − × ÷ dealer, picture, odd, pattern, compare), `Levels` (World 1 table, daily seed, song per world + boss track), `CourseBuilder` (procedural slopes), `RunSim` (steering, speed, hold-to-boost, jump + flip, collisions, items, power crystal, gates, rival, scoring), `Progress` (stars / best per level, unlocks, next level to play, endless best) + `Settings` |
| `Assets/Scripts/Game/` (`Pip.Game`) | Scene layer: `GameBoot` (builds the 2D layer: camera, Art2D views; screen flow title → map → intro → play ⇄ pause → result, input), `Hud` (since the v5 art pass (5 Nov 2026) built from the u2 / u3 sprites: ui.pause button with a press squeeze, ui.heart / ui.heartOff swapped per heart, timer on the ui.plank and the count on the ui.pill with the ui.coin, white numerals with a 2.5 navy outline and no drop; ui.track + ui.fill under the growing mask with the gold stripes at 35 % on top, ui.markerMe / ui.markerRival, ui.flag; result stars ui.starOff / ui.star (72 / 88 / 72), the total on a ui.plaque, a −12° ui.badge "สถิติใหม่!" (84 × 84, centred 50 / 36 units in from the panel's top-right corner so it stays inside the panel's width, over the ribbon's tail) on a new best; the question card fits its question (`FitQuestion`: one line from 34 down to 20, else two lines broken at a space / Thai word boundary from 26 down to 16 with the card growing downward by the extra line; ring and hint pinned to the top; the hint shrinks 13 → 10 if needed — the longest, "ประตูไหนเป็นรูปข้าวหลามตัด?", fits on one line); intro badge on ui.tag; each falls back to the drawn widget without its slice) (top bar, progress (gold stripes #FFD45A / #F7B52C since 5 Nov 2026, also on the fail result's "% of the level" bar), power-ups + boost pills, question card + countdown ring, toast, coin streak, intro / pause / result panels; every screen change closes the intro / pause / result panels first), `SaveData` (PlayerPrefs; tests swap in a memory store), `Look` (hex colours) |
| `Assets/Scripts/Game/Art2D/` | **The 2D sprite layer (default, GDD §18.k)**: `View2D` (the web 2D projection as pure functions — persp, SY = 0.37·H, toY, toX, fadeTop — design space 390 × 390·h/w, sorting orders), `SpriteTable` (which sheet slice = which role, per-role sizes; one place to fix; also the `ui.*` roles of the u2–u4 kits — `SpriteTable.Ui(role)` returns the sprite or null, and every UI builder falls back to its procedural widget on null; u3's reading order puts the gold star (u3_1) before the ribbon (u3_2)), `Kit2D` (sprite placement by anchor, TextMesh labels with ink outline), `Placeholder2D` (procedural sprites the sheets don't carry: shadows, moguls, crest, rings, answer shapes, sign, haze; stand-ins for missing slices), `Backdrop2D` (b1 sky band + clouds + blizzard haze) + `Slope2D` (b2 tile and b3 edge strips scrolling with pz; strips 81 units wide, the right one half a tile out of phase, each inner edge under a wavy `Placeholder2D.Bank`), `Course2D` (pooled props, gate rows with answer text / shapes, arches with the u4 START / FINISH lettering on the banner — `ArchBannerW` × the arch width, centred on `ArchBannerY`, the `Label2D` text only when the slice is missing — signs, crest), `Pip2D` (+ `PoseFor`; the ski-pump cycle `Cycle` — speed-driven bob, squash / stretch, sway and x wiggle while on the snow, amplitudes as `Pump*` constants at the top of the class, the knees-bent frame `pip.straightB` on the low half of each pump (tall at the top, squat at the bottom), ω = 7 + 7·spd01 rad/s; `Trail2D` ski tracks, `Benny2D` with the same cycle at his own speed), `Particles2D` (bursts, carve spray, a continuous wake off both ski tails, speed streaks from 55 % speed and full ones while boosting, crystal sparks, landing poof on s07 sprites; screen-space snowfall; `Wake*` / `Streak*` constants — wake since 5 Nov 2026: 18 + 40·spd01 puffs/s off each ski tail (±12), life 0.55–0.9 s, size 7–13 × 1.6, fill #AFC9EA α 0.85 over a rim quad #86A9DB α 0.5 at 1.2×, drifting ±6 units outward and growing to 1.35× over its life, fading in the last 40 %; carve spray uses the same tint + rim (α 0.9, size × 1.25, starting just behind Pippy so it never covers him), rate unchanged). Everything runs on `View2D.T` / `View2D.Dt`, which `GameBoot.Update` advances only outside Pause, so Pippy, Benny, tracks, particles, backdrop and camera freeze behind the pause panel and resume without a jump |
| `Assets/Scripts/Game/Ui/` | The web's toy UI (`index.html` CSS + inline SVG) rebuilt as meshes, with the v5 sprite kit on top (5 Nov 2026: `K.Panel` = 9-sliced ui.panel with the red ui.ribbon header and its title in white lettering on a navy outline (no drop, the icicles and glare are gone with the sprite panel; the ribbon stays red on a fall), `K.BigButton` = ui.btnGreen (primary) / ui.btnBlue (second tier), `K.IceButton` = ui.btnIce (one sliced Image; a near-square button such as the result's 60 × 60 map / retry lowers `pixelsPerUnitMultiplier` so `Image.GetAdjustedBorders` scales the borders to meet exactly — squeezing the whole stretchable middle into a few units had blurred the outline into a pale streak down the centre), `K.RoundButton` = ui.back / ui.pause, the vector icons kept on the pill buttons; `K.Sprite` / `K.Sliced` place a slice, `Sliced` scaling the slice so its height maps to a given size; the map's World 2 sign on ui.plank and its star counter on ui.pill + ui.star, the title's star count on ui.pill): `Shapes` (`RRect` rounded rects with CSS-style gradients, inset bands and wood stripes; `Vec` SVG path data — M L H V C S Q T A Z — filled and round-stroked; `GameText` thick-outlined two-tone lettering — its ink drop is off by default since 5 Nov 2026 (user: the letters looked like they fell out of their frame), the outline stays, only the title logo ("Pippy", "Ski Penguin") keeps a drop; one-pixel edge smoothing), `Kit` (ice / big / chip / round buttons that sink when pressed, ice panels with ribbon + icicles, icons copied from the web SVGs, bounce / pulse / pop-in / shimmer), `Widgets` (countdown ring, candy stripes, result stars, row fade-in), `TitleScreen`, `MapScreen` (the web's 390×1100 map layout, nodes, stars, names, World 2 sign; since 5 Nov 2026 in the play screen's palette: royal-blue → violet → pink sky gradient with the b1 band across the top of the content fading into #EEF6FF snow, blue-violet shadow blobs and sparkles, the s04 trees / snowman / two rocks with soft shadows in place of the drawn ones (the SVG drawing is the fallback), a cream trail with a navy #1F2A4D edge, ice inner edge, glacier-blue dashes and a shadow, navy-rimmed nodes with a top glare, white name pills with a 2.5 navy outline; the opening scroll centres the current level in the area under the header but never further down than keeps the top level's name tag clear of the header, unless the current node would leave the screen), `MapPip` (Pippy waving on the map: the s02 wave sprite in an Image) |
| `Assets/Scripts/Game/Art/` | `Tex` (hex colours, `Canvas2` software canvas, the cloud puff and a white texel for the 2D placeholders) and `Mats` (`Pip/Unlit` materials, alpha / additive). The 3D art (MeshKit, Rig, PenguinModel, Props, V2Models, Backdrop) was removed on 5 Nov 2026 |
| `Assets/Shaders/` | `Pip/Unlit` (particles, decals, placeholders); `Pip/Lit` and `Pip/Sky` went with the 3D layer (5 Nov 2026) |
| `Assets/Resources/Sprites2D/` | The 2D sheets from `design/sprites2d/sheets` keyed and sliced by `SpriteImport`: `s01`–`s07` and the v5 UI kits `u2` (HUD: back / pause buttons, hearts on / off, timer plank, counter pill, coin, flag, progress track + fill, Pippy / Benny markers), `u3` (windows: ice panel, ribbon, star / star socket, blue / green / ice buttons, navy plaque, starburst badge, tag pill), `u4` (START / FINISH lettering) (Sprite Multiple, `<sheet>_<n>`; s04–s06 and u2–u4 are the toy-3D art of 5 Nov 2026 (art v5, flat v3 sheets archived in `sheets/v3-flat/`), keyed by un-matting against white so their soft shadows and the crystal's glow are translucent; the stretchable UI slices carry 9-slice borders; Pippy v4 from 5 Nov 2026 — s01: 0 straight · 1 straightB · 2 carveR · 3 carveL · 4 tuck · 5 jump · 6 flip; s02: 0 tumble · 1 dizzy · 2 cheer · 3 scared · 4 proud · 5 wave; s03 Benny: 0 straight · 1 carveL · 2 carveR · 3 cheer), `b1` sky band, `b2` snow tile (the v5 tile since 5 Nov 2026, made seamless in the importer — `MakeSeamless`: shifted by half its width, a 14 % band cross-faded across the old edges, then the same vertically — the flat `sheets/v3-flat/b2.png` only as a fallback when the v5 file is missing), `b3` edge strip (left 56 %, crisp inner cut covered in game by a drawn snow bank, bottom band cross-faded into the top); `b2` is blended 50 % toward flat snow #F7FBFF |
| `Assets/Audio/Future~/` | Worlds 2–4's song loops (`music_pine/crystal/lantern_loop.wav`) parked out of the build until those worlds exist (`~` = ignored by Unity) |
| `Assets/Scripts/Editor/` | `SpriteImport` — menu **Pip ▸ Import 2D Sprites** / `-executeMethod Pip.EditorTools.SpriteImport.Run`: keys the raw sheets (s01–s03 flood-fill of the border-connected near-white background, 2-px feather; s04–s06 and u2–u4 `UnmatteKey` since 5 Nov 2026: flood of the border-connected light pixels (min channel ≥ 150, stopped by the dark outlines; the background mask is opened by 4 px first so the flood can't slip through a hairline gap into the arch's blank ribbon or a star's sparkle), un-matted against white — a = 1 − min/255, colour = (p − 255·(1 − a)) / a, a < 0.03 transparent — islands cut at alpha ≥ 0.15 so the outline-less grey star socket still slices while shadows don't; `SolidCore` exception for u3_3 (ui.starOff) and s06_4 (item.slow): inside those slices the un-matting is kept only within 8 px of the fully transparent background around the item (the anti-aliased edge and the shadow), deeper pixels get their original colour back at alpha 1 (the import logs `[Pip] solid core …` with the count: about 64 k px on the socket, 0 on the hourglass, whose glass the flood never reached), s06 / u2–u4 rects grown over the translucent shadow / glow within 2.5 % of the sheet width, u4's letters joined per row; 9-slice borders on ui.panel / plaque / plank (22 % of width and height) and the pills, bars, tag and buttons (45 % of the height); s07 luminance key), slices islands into named rects (islands under 5.5 % of the largest are merged into the nearest bigger one within 3 % of the sheet width, so carve sprays and the dizzy stars stay with their figure; dust under 6 px is dropped; needs the built-in `com.unity.2d.sprite` package; `SpriteImport.RunCharacters` re-imports only s01–s03, `SpriteImport.RunEdge` only b3 — regenerated 5 Nov 2026 without structures, since the 56 % crop had cut the cocoa stand down to a roof and legs that read as a red table), Full Rect, mipmaps, ASTC 6x6 on iOS / Android, logs `[Pip] slice <name> x y w h` · `ProjectSetup` — menu **Pip ▸ Setup Project** / `-executeMethod Pip.EditorTools.ProjectSetup.Run`: URP asset for 2D (no HDR, MSAA, shadows or post-processing data), `Assets/Scenes/Game.unity` (GameBoot + the `PipUnlit` template), build list, portrait · `AudioImport` (music streaming / jingles + effects decompressed) · `IconSetup` (12 Nov 2026) — menu **Pip ▸ Setup App Icons** / `-executeMethod Pip.EditorTools.IconSetup.Run`: reads `design/icon/icon-master.png` (2048², full-bleed, opaque) and writes `Assets/Icons/` (iOS 1024 RGB without alpha by exact 2 × 2 averaging; Android adaptive foreground = the master at exactly 2/3 of a 1024 canvas, centred, its edge pixels extended into the margin with a light blur fading in; adaptive background = the mean colour of the master's four 128² corners; round (circle mask, anti-aliased) and legacy 512²), imports them (Default, sRGB, no mipmaps, NPOT none, uncompressed, max 1024, not readable; iOS alpha source None) and assigns them: every iOS icon kind (Application, Spotlight, Settings, Notifications, Marketing — each slot gets the 1024, Unity resizes), Android Adaptive (background + foreground), and the default icon (Standalone). Unity offers Android Round / Legacy only below API 26 (the project's minimum is 26), so those two PNGs are written but not assigned. Logs `[Pip] icon …` per file and platform; changes nothing else in Player Settings. **To change the icon: replace `design/icon/icon-master.png`, run Pip ▸ Setup App Icons, rebuild** |
| `Assets/Icons/` | App icons generated by `IconSetup` from `design/icon/icon-master.png` (outside `Resources`, so they are not shipped as game data): `icon-ios-1024`, `icon-android-foreground` / `-background` (adaptive layers), `icon-android-round`, `icon-android-legacy`. Don't edit by hand — re-run **Pip ▸ Setup App Icons** |
| `Assets/Tests/EditMode/` | NUnit tests (72, all passing 12 Nov 2026 with `IconTests`: iOS Application slots and the Android adaptive layers assigned, `icon-ios-1024.png` 1024² RGB without alpha, the adaptive foreground's centre 2/3 sampled against the master; the home-screen name is "Pippy" while the product name and bundle id stay, and the iOS Info.plist patch sets CFBundleDisplayName): 2D projection (`View2DTests`), sprite poses + every role sliced + the UI kits (slice counts s04 8 · s05 8 · s06 11 · u2 12 · u3 10 · u4 2, every `ui.*` role, 9-slice borders, slice shapes) + the ski-pump cycle (`Pip2DTests`), web parity, rules (incl. ski-track samples and boost ramp / air / crystal cap), audio clips (shipped loop seams, parked songs, home-theme fallback, jingles, song mapping), hex colours + `Canvas2` (`TexTests`), progress + save round trip, SVG path parsing; `Fixtures/` = courses exported from the web prototype |
| `Assets/Tests/PlayMode/` | Loads the real Game scene: walks title → map → level 2 → pause → win / fall results → "เล่นอีกครั้ง" (retry), pause → "เริ่มใหม่" and win → "ด่านต่อไป", each of which must leave only the next intro on screen (no result / pause panel) → title (progress in a memory store), autopilots level 3 with a power crystal, autopilots level 2 holding boost, checks that the 2D views are up (Pip2D, > 10 course sprites, ski tracks), and checks the music follows the screens (title snowday → level 6 showdown crossfade → ducked under a question → title) (4 tests, all passing 5 Nov 2026 after the v5b polish, again 12 Nov 2026 with the app icon); saves camera captures to `$PIP_SHOT_DIR` (the overlay UI isn't in those captures — check UI on the simulator) |
| `Assets/Materials/` | `PipUnlit` template material referenced by the scene, so the shader survives build stripping |

**2D (default)**: one orthographic camera (Base, no post-processing, no lights, no MSAA) over the
design space — screen (x, y) at world (x, −y), 1 unit = 1 web css px, 390 wide and 390·h/w tall
(at least 640). Projection exactly as `render.js`: Pippy at SY = 0.37·H, object y = SY + (z − pz),
x = W/2 + x·persp(y)/persp(SY), sprite scale persp(y) = 0.82 + 0.3·y/H, fade-in below the crest
(fadeTop), culled above the sky band and below the screen. Draw order (sorting orders, one layer):
snow tile → edge strips → moguls / shade → sky picture + clouds → ski tracks → ice / holes → ground
shadows → everything standing on the snow by screen y (lower = on top; the crest is one of them, at
its own y, so things beyond it go behind the hill) → particles → blizzard haze → UGUI. Sprites use
Sprites/Default; particles are two meshes on `Pip/Unlit` (alpha + additive) with the s07 texture.
The camera zooms out up to ~8 % with speed and boost about Pippy's point, dips on landing and shakes.

The old 3D layer (`-pipArt3d`: glTF v2 models, lights, fog, post-processing, camera stack) and the
glTFast package were removed on 5 Nov 2026; the models are archived in `design/higgsfield/3d/`.

## Run it

- Open `unity/` in Unity Hub → open `Assets/Scenes/Game.unity` → Play. It opens on the title screen.
- Title: play (next level short of three stars), daily endless (best distance), level map (stars so far), speed / music / sound / graphics chips — saved in PlayerPrefs (`stars.N`, `best.N`, `endlessBest`, `set.*`).
- A level unlocks once the one before it has a star (web rule). Pause button top-left; going to the background pauses too.
- Art: **2D sprites** (design/sprites2d, GDD §18.k). Re-import the sheets after the art changes with **Pip ▸ Import 2D Sprites** and check the `[Pip] slice` lines against `Art2D/SpriteTable.cs`. `-pipFps` logs `PIP FPS avg=… min=… over 5.0s` every 5 s to the player's stdout.
- Controls: hold + drag = steer · hold still for 0.25 s = boost until you lift (dragging still steers, a flick still jumps; the hold that starts a level doesn't boost) · quick tap / flick up = jump · tap again in the air = flip · keyboard ←/→ or A/D, Shift = boost, Space, P = pause.
- Boost (`Tuning.BoostSpeed` 0.35, `BoostHold` 0.25 s, `MaxSpeedMul` 1.5): eases up to ×1.35 in 0.45 s, only with the skis on the snow, and back down in 0.35 s on release; stacks with the power crystal (×1.15) up to ×1.5. Pippy tucks, spray thickens, speed streaks rush past, the lens opens (5 % zoom-out), a "»» เร่ง" pill shows and a whoosh loops.

## iOS Simulator build

Needs the *iOS Build Support* module (installed 4 Nov 2026 via `Unity Hub -- --headless install-modules --version 6000.6.8f1 -m ios`).

```bash
"/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath unity -executeMethod Pip.EditorTools.ProjectSetup.BuildIOSSimulator -pipOut "$PWD/unity/Builds/iOS-Sim" -logFile -
```

```bash
cd unity/Builds/iOS-Sim && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphonesimulator -destination "platform=iOS Simulator,id=6714FB90-A2AF-48EC-9BC6-147D96BBE76B" -derivedDataPath ../iOS-Sim-DD CODE_SIGNING_ALLOWED=NO build
```

The app lands in `unity/Builds/iOS-Sim-DD/Build/Products/Debug-iphonesimulator/PippyTheSkiPenguin.app` (236 MB Debug, Data 54 MB with the v5 sheets and the u2–u4 UI kits (5 Nov 2026, unchanged by the v5b polish with the seamless v5 b2; 229 MB / Data 46 MB before them, 241 MB before the 3D layer and its models were removed); the Release configuration of the same Xcode project is 156 MB, Data 46 MB; the Unity player is still a Development build, `BuildOptions.Development`). The destination is given by id because two "iPhone 17 Pro Max" simulators exist; the one above (iOS 27.2) is the standard test device for every simulator check, as the owner asked on 7 Nov 2026; it is the test
device. The first launch on a simulator compiles Metal shaders for a few seconds (black screen) before the level card shows.
The player's `Debug.Log` goes to stdout, not the unified log, so to read the `-pipFps` lines run
`xcrun simctl launch --console-pty --terminate-running-process 6714FB90-A2AF-48EC-9BC6-147D96BBE76B com.everyimagine.pippy`
with `SIMCTL_CHILD_PIP_ARGS="-pipLevel 1 -pipAuto -pipFps"` (simulator numbers say nothing about a real device).
Engine code stripping is off for now because the placeholder props are Unity primitives (they carry colliders).

## iPhone (device) build

Same module as the simulator build. `BuildIOSDevice` writes a separate Xcode project (Device SDK, Unity player without `BuildOptions.Development`, Xcode scheme Run = Release, automatic signing):

```bash
"/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath unity -executeMethod Pip.EditorTools.ProjectSetup.BuildIOSDevice -pipOut "$PWD/unity/Builds/iOS-Device" -logFile -
```

Optional `-pipTeam <TEAMID>` stores the Apple Developer Team ID in Player Settings so Xcode needs no clicks; without it:

1. open `unity/Builds/iOS-Device/Unity-iPhone.xcodeproj` in Xcode;
2. target **Unity-iPhone** ▸ *Signing & Capabilities* ▸ *Automatically manage signing* on, pick the **Team** (add the Apple ID first in Xcode ▸ Settings ▸ Accounts if the list is empty);
3. pick the phone in the run-destination menu and press Run (⌘R).

First run on a phone: iOS asks for *Developer Mode* (Settings ▸ Privacy & Security ▸ Developer Mode, then restart); with a free Apple ID the developer certificate must also be trusted once (Settings ▸ General ▸ VPN & Device Management) and the app expires after 7 days.
Bundle id is `com.everyimagine.pippy`; save data on the phone is separate from the simulator.

**Name under the icon** (12 Nov 2026): **"Pippy"** — one constant, `ProjectSetup.HomeScreenName`. The product name stays "Pippy The Ski Penguin" (Xcode product, `PippyTheSkiPenguin.app`, CFBundleName, save path). iOS: `Editor/HomeScreenNameIOS.cs` (`[PostProcessBuild]`) sets `CFBundleDisplayName` in the exported `Info.plist` after every iOS build (simulator and device) — not through Player Settings, because in Unity 6 `PlayerSettings.iOS.applicationDisplayName` is an alias of `productName` and would rename the app too. Android: `Editor/AndroidAppName.cs` (`IPostGenerateGradleAndroidProject`, compiled only under `UNITY_ANDROID`) rewrites `app_name` in the generated Gradle project's `res/values/strings.xml` (launcher module first, then unityLibrary) and logs a `[Pip]` warning if it can't find it — **unverified**, there is no Android build yet.
Compile check without signing (passed 6 Nov 2026, `ReleaseForRunning-iphoneos/PippyTheSkiPenguin.app` 150 MB): `cd unity/Builds/iOS-Device && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration ReleaseForRunning -sdk iphoneos -destination generic/platform=iOS -derivedDataPath ../iOS-Device-DD CODE_SIGNING_ALLOWED=NO build` (Xcode's Run uses the `ReleaseForRunning` configuration).
Re-running `BuildIOSDevice` regenerates the Xcode project, so a Team picked by hand in Xcode has to be picked again; a Team ID passed once with `-pipTeam` stays in Player Settings and survives rebuilds.

## Audio

Music is the v2 soundtrack (GDD §13 / §18.l): the Higgsfield demos in `design/higgsfield/full/`
(ElevenLabs Music, 90-s songs + jingles; licence still to confirm, see `ASSET_LICENSES.md`).
Effects are still the web prototype's procedural Web Audio rendered to clips.

- `unity/Tools/make-music.py` (Python stdlib + `/usr/bin/afconvert`, no Unity needed) builds the
  music into `Assets/Resources/Audio`: `music_<id>_loop.wav` per song (snowday, pine, crystal,
  lantern, aurora, showdown) and the jingles `music_clear`, `music_fail`, `music_star`,
  `music_question`. Loops: decode to 16-bit 44.1 kHz stereo, trim trailing silence (last sample
  above −50 dBFS), cut a fade-out / ringing tail (after the last 0.25-s window within 6 dB of the
  song's RMS), crossfade the final 1.0 s into the first 1.0 s (equal power) so the wrap is
  seamless, normalize to −16 dBFS RMS. Jingles: trailing silence trimmed, −14 dBFS RMS. A block
  peak limiter keeps everything under −0.2 dBFS (at most 3 dB of limiting; quieter jingles land a
  little under target). Re-run `python3 unity/Tools/make-music.py` after replacing a demo (about
  1.5 min) and let Unity reimport.
- Song per screen (`Levels.SongFor`, `Levels.Songs`, `Levels.HomeSong`): title, level map and
  World 1 levels 1–5 → **snowday** · every boss level (Id % 6 == 0, now level 6 vs Benny) →
  **showdown** · Daily Endless → **aurora** · Worlds 2–5 (not in `Levels` yet) by world →
  pine / crystal / lantern / aurora. This replaces the old "new song every 5 levels" cycling.
- Shipped loops: snowday, aurora, showdown only. pine / crystal / lantern are parked in
  `Assets/Audio/Future~/` (5 Nov 2026, to keep the player's music under target) — move them back to
  `Resources/Audio` when Worlds 2–4 exist. Until then `AudioService.Music` plays the home theme for a
  song whose clip isn't shipped and logs `music: no clip for <id>, playing snowday`
  (`AudioService.SongOrFallback`).
- `AudioService.Music(id)` keeps one song going across screens: the title starts snowday, the map
  keeps it, a level's intro switches to its song, and `RunSim.Start` asking for the same song is a
  no-op (no restart). A different song starts while the old one fades out over 0.6 s (equal-power).
  `clear` / `fail` replace the song once; `Jingle("star", pitch)` plays on each result star as it
  lands (pitch 1.0 / 1.12 / 1.25; the old chime if music is off) and `Jingle("question")` when a
  question card appears, on top of the duck (music → 0.45/0.8 while the question shows).
  `MusicOn` off stops the music, on resumes the current screen's song. Levels: loops × 0.4,
  jingles × 0.35 — about the old web mix under the effects; nobody has listened yet, tune on device.
  Each song change logs `music: <id>`.
- Effects: `tools/render-audio/` drives `prototype/web/js/audio.js` through an `OfflineAudioContext`
  and saves `sfx_*` WAVs (start the `render-audio` entry in `.claude/launch.json` or
  `node tools/render-audio/server.js`, open http://localhost:8766/?only=sfx). Coin sound is
  rate-limited like the web. Ski carve hiss, spray and wind are synthesized at runtime from
  biquad-filtered noise and follow speed and carve every frame (port of `A.ski()`). The boost whoosh
  (Unity-only) is made the same way: 0.8 s of band-passed noise (900 Hz) swelling 55 % → 100 % →
  55 %, looped at 0.5 × boost level.
- Player size (iOS build report, 5 Nov 2026): the three shipped loops are 7.0 MB (showdown 2.6 + aurora 2.2 +
  snowday 2.2; all six were 13.9 MB), Sounds 7.8 MB in all; the 2D sheets 23 MB as ASTC 6x6 (2.4 MB per
  2048² sheet) before the three UI kit sheets u2–u4 (art v5) added about 7 MB. `-pipFps` on the simulator
  (level 3, autopilot, art v5): avg 60.0, min 55–56 fps per 5 s window (the first window dips while shaders compile).
- Import settings (`Editor/AudioImport.cs`): all Vorbis; song loops quality 0.7 **Streaming**,
  jingles (0.7) and effects (0.8) Decompress On Load. `AudioClipTests` checks every clip exists,
  loops are 80–92 s, end on sound and their first / last 0.5 s are within 3 dB, jingles ≤ 6 s, and
  pins the song mapping; it also checks the parked songs are out of Resources and kept in `Future~`.
- Legacy: the web-rendered music (`music_battle*`, `music_rival_loop`, `music_snow_loop`, old
  `music_clear` / `music_fail`) is parked in `Assets/Audio/Legacy~/` (Unity ignores `~` folders,
  so it isn't imported or shipped).

## Tests

Batch mode (close the editor first):

```bash
"/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults /tmp/pip-results.xml -logFile -
```

PlayMode smoke test with screenshots (renders offscreen, no window needed):

```bash
PIP_SHOT_DIR=/tmp "/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath unity -runTests -testPlatform PlayMode -testResults /tmp/pip-pm.xml -logFile -
```

Player builds also accept `-pipLevel N -pipAuto -pipPower S -pipBoost -pipEndAt S -pipShot path` for unattended runs
(straight into that level, skipping the title; `-pipLevel 0` is the Daily Endless; `-pipBoost` holds boost for the whole run, which PlayMode
tests get from `GameBoot.SmokeBoost`; `-pipEndAt S` makes the autopilot let go after S s of play — no steering, no
refilled hearts — so an endless run ends by itself and shows its result). On the iOS Simulator launch arguments don't reach
the player, so pass them in `PIP_ARGS`:

```bash
SIMCTL_CHILD_PIP_ARGS="-pipLevel 1 -pipAuto" xcrun simctl launch --terminate-running-process <device> com.everyimagine.pippy
```

Parity fixtures are regenerated from the web prototype with the node script below (it loads
`prototype/web/js/levels.js` and writes `L1…L6.txt`, `Endless-20261104.txt`, `Rng-12345.txt`).
Re-export whenever `levels.js` changes, then the tests show any drift between web and Unity.

```bash
node tools/export-fixtures.js prototype/web/js/levels.js unity/Assets/Tests/EditMode/Fixtures
```

## Not done yet

- **Android build**: add *Android Build Support* in Unity Hub (iOS Build Support is installed).
- **App icon**: the Android adaptive icon is assigned in Player Settings but not checked on a device (no Android build yet), nor is the "Pippy" launcher label (`AndroidAppName`); iOS dark / tinted icon variants (iOS 18+) are not provided, so iOS shows the light icon in every appearance.
- **Company name**: still `DefaultCompany` (bundle id fixed to `com.everyimagine.pippy` on 6 Nov 2026 for iOS / Android / Standalone).
- **2D art (GDD §18.k)**: verified with the autopilot only (PlayMode + iOS Simulator); text positions on the gate / arch banners are measured from the sheets (`SpriteTable.GateBannerY` …) and may need a nudge; 2-answer gates are stretched sideways, the arch squashed to ~165 units; no world 2–5 backdrops yet (sunset tints b1); the ramp (s05_7) and heart (s06_5) slices are unused; the ski-pump / wake amplitudes were tuned on the simulator only. Art v5 (toy-3D sheets + UI kits, 5 Nov 2026): the new b2 tile seamed where it repeats, so the importer now makes it seamless (offset + cross-fade) and the game uses it — no seam line on the simulator while the ground scrolls (level 1); the title key art `t2` is not used (fitted to the width of a portrait screen it would sit entirely behind the title's buttons, and it carries its own Pippy next to the live one), the title keeps the live slope behind the logo with the arch lettering hidden (`Course2D.ArchWords`); the star socket (u3_3) and the hourglass are opaque inside (only their rim and shadow are un-matted); the hourglass's white gaps between the pillars and the glass stay opaque white, as before; the fall result's ribbon stays red (the drawn ribbon turned slate); the play button's sweeping shine and the panel icicles went with the procedural widgets; the map's level nodes keep their drawn look; the "สถิติใหม่!" badge only ever shows on a Daily Endless result, because `Progress.Record` reports a new best only for endless distance (levels keep their best score silently).
- **3D layer removed (5 Nov 2026)**: `-pipArt3d`, the 3D scene code, `Pip/Lit` / `Pip/Sky`, `Resources/Models` and glTFast are gone from the project; the models and their sources are archived in `design/higgsfield/3d/` (`gltf/` holds a copy of every model that was in Unity).
- **Fonts**: the phone's built-in fonts for now (Sukhumvit Set for Thai, Arial Rounded for game digits / Latin); the final UI bundles Kanit / Mitr / Fredoka (OFL).
- Cloud save, ads / IAP / leaderboards (GDD §8–10, business decisions D1–D14 pending).
