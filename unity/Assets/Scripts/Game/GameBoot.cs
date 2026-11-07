// Scene entry point: builds the slope, Pip, props, particles, ski tracks and HUD in code — the 2D sprite
// layer (Art2D: orthographic camera over the web's 390-wide projection, GDD §18.k) — then runs the
// screens like game.js: title → level map → intro → play ⇄ pause → result, with progress and settings
// saved. Input matches the web prototype: hold and slide to steer, quick tap or flick up to jump, tap
// again in mid-air to flip; arrows/A-D + Space on keyboard.
// Unity adds hold-to-boost: a finger held still for Tuning.BoostHold s boosts until it lifts (it can
// still slide to steer and flick to jump meanwhile); Shift on keyboard.
using System.Collections.Generic;
using Pip.Core;
using Pip.Game.Art;
using Pip.Game.Art2D;
using Pip.Game.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace Pip.Game
{
    public sealed class GameBoot : MonoBehaviour
    {
        [Header("Shader template (set by Pip ▸ Setup Project)")]
        public Material unlitTemplate;

        public enum Mode { Title, Map, Intro, Play, Pause, Result }

        Camera cam;
        Backdrop2D backdrop2;
        Slope2D slope2;
        Course2D course2;
        Pip2D pip2;
        Benny2D benny2;
        Particles2D particles2;
        float zoom = 1;
        AudioService audio;
        Hud hud;
        TitleScreen title;
        MapScreen map;
        RunSim sim, titleSim;
        LevelDef level;
        Mode mode;
        Progress progress;
        Settings settings;
        bool introFromMap;
        // −1 opens on the title; 1–6 = straight into a World 1 level, 0 = Daily Endless (smoke tests).
        int startLevel = -1;
        float bump, bumpV;
        bool wasAir, flipped;

        // touch state (boosting: this press was held still long enough to boost)
        bool down, noTap, noBoost, boosting;
        Vector2 start;
        float minY, downT, moved, x0;

        static readonly System.Collections.Generic.Dictionary<ObjType, string[]> SmashCol = new System.Collections.Generic.Dictionary<ObjType, string[]>
        {
            { ObjType.Tree, new[] { "#2FA36B", "#FFFFFF" } }, { ObjType.Rock, new[] { "#9AA3B8", "#FFFFFF" } }, { ObjType.Log, new[] { "#A66A40", "#D9A877" } },
            { ObjType.Snowman, new[] { "#FFFFFF", "#E5484D" } }, { ObjType.Bear, new[] { "#8B5A3C", "#FFFFFF" } },
        };

        sealed class Listener : RunListener
        {
            readonly GameBoot g;
            public Listener(GameBoot g) { this.g = g; }
            public override void Toast(string text, string color) => g.hud.Toast(text, color);
            public override void Sfx(string id) => g.audio.Sfx(id);
            public override void Music(string id) => g.audio.Music(id);
            public override void Duck(bool on)
            {
                g.audio.Duck(on);
                if (on) g.audio.Jingle("question"); // Duck(true) comes once, as the card first shows
            }
            public override void ShowQuestion(GateRow row) => g.hud.ShowQuestion(row);
            public override void HideQuestion() => g.hud.HideQuestion();
            public override void Burst(double x, double z, double h, int n, string kind, params string[] colors) => g.Burst(x, z, h, n, kind, colors);
            public override void CoinCollected(CourseObj coin)
            {
                g.hud.Coin();
                g.audio.Sfx("coin");
                g.Burst(coin.X, coin.Z, 16, 3, "spark", new[] { "#FFE066" });
            }
            public override void Smashed(CourseObj o)
            {
                g.Burst(o.X, o.Z, 10, 14, "puff", SmashCol.TryGetValue(o.Type, out var c) ? c : new[] { "#FFFFFF", "#DCE7FF" });
                g.Burst(o.X, o.Z, 20, 8, "spark", new[] { "#8FF3FF", "#FFE98A" });
            }
            public override void RowResolved(GateRow row, bool right)
            {
                if (!row.Smashed) return;
                for (int i = 0; i < row.N; i++)
                    g.Burst(-195 + 390.0 / row.N * (i + 0.5), row.Z, 60, 12, "puff", new[] { row.Q.Options[i].Color ?? "#3FA9F5", "#FFFFFF" });
            }
            public override void Finished() => g.ShowResult(true);
            public override void Failed() => g.ShowResult(false);
        }

        void Burst(double x, double z, double h, int n, string kind, string[] colors)
        {
            particles2.Burst(x, z, h, n, kind, colors);
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Build2D();
            audio = new GameObject("Audio").AddComponent<AudioService>();
            hud = new GameObject("Hud").AddComponent<Hud>();
            Kit2D.LabelFont = Hud.ThaiFont();
            progress = SaveData.LoadProgress();
            settings = SaveData.LoadSettings();
            title = TitleScreen.Build(hud.SafeArea(hud.CanvasRoot));
            map = MapScreen.Build(hud.CanvasRoot, hud.SafeArea);
            WireScreens();
            ApplySettings();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
                // A small finger wobble on a level node is still a tap, not a scroll.
                es.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt((Screen.dpi > 0 ? Screen.dpi : 160) * 0.06f));
            }

            // The title shows level 1's slope behind the logo, with Pip idling on it.
            titleSim = new RunSim(Levels.World1[0], CourseBuilder.Build(Levels.World1[0], new SysRng()), 1, new Listener(this));
            ReadSmokeArgs();
            if (startLevel >= 0) OpenLevel(startLevel == 0 ? Levels.Endless : Levels.World1[Mathf.Clamp(startLevel, 1, Levels.World1.Length) - 1]);
            else GoTitle();
        }

        /// <summary>The 2D sprite layer: one orthographic camera (no post, no lights) over the design space
        /// (390 × 390·h/w units, View2D), sky band, scrolling snow + edge strips, course, Pip, Benny, VFX.</summary>
        void Build2D()
        {
            Mats.Init(unlitTemplate);
            View2D.H = View2D.DesignHeight(Screen.width, Screen.height);
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = View2D.H / 2;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Tex.C("#F7FBFF");
            cam.transform.position = new Vector3(View2D.W / 2, -View2D.H / 2, -10);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderType = CameraRenderType.Base;
            camData.renderPostProcessing = false;
            camData.renderShadows = false;
            camData.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<AudioListener>();

            var root = new GameObject("World2D").transform;
            backdrop2 = new GameObject("Backdrop2D").AddComponent<Backdrop2D>();
            backdrop2.transform.SetParent(root, false);
            backdrop2.Init();
            slope2 = new GameObject("Slope2D").AddComponent<Slope2D>();
            slope2.transform.SetParent(root, false);
            slope2.Init();
            course2 = new GameObject("Course2D").AddComponent<Course2D>();
            course2.transform.SetParent(root, false);
            pip2 = new GameObject("Pip2D").AddComponent<Pip2D>();
            pip2.transform.SetParent(root, false);
            pip2.Init();
            benny2 = new GameObject("Benny2D").AddComponent<Benny2D>();
            benny2.transform.SetParent(root, false);
            benny2.Init();
            particles2 = new GameObject("Particles2D").AddComponent<Particles2D>();
            particles2.transform.SetParent(root, false);
            particles2.Init();
        }

        static readonly float[] StarPitch = { 1f, 1.12f, 1.25f };

        void WireScreens()
        {
            title.Play += () => { audio.Sfx("pop"); introFromMap = false; OpenLevel(progress.NextPlayable()); };
            title.Endless += () => { audio.Sfx("pop"); introFromMap = false; OpenLevel(Levels.Endless); };
            title.Map += () => { audio.Sfx("pop"); OpenMap(); };
            title.SettingsChanged += s =>
            {
                SaveData.SaveSettings(s);
                ApplySettings();
                audio.Sfx("pop");
            };
            map.Back += () => { audio.Sfx("pop"); GoTitle(); };
            map.Pick += L => { audio.Sfx("pop"); introFromMap = true; OpenLevel(L); };
            hud.Sfx += id => audio.Sfx(id);
            // Each result star lands with the star jingle, a step higher per star (the chime if music is off).
            hud.StarLanded += i => { if (audio.MusicOn) audio.Jingle("star", StarPitch[Mathf.Clamp(i, 0, 2)]); else audio.Sfx("star"); };
            hud.Pause += () => { audio.Sfx("pop"); SetPaused(true); };
            hud.Resume += () => SetPaused(false);
            hud.Restart += () => { audio.Sfx("pop"); OpenLevel(level); };
            hud.Home += () => { audio.Sfx("pop"); GoTitle(); };
            hud.IntroBack += () => { audio.Sfx("pop"); if (introFromMap) OpenMap(); else GoTitle(); };
            hud.ResultMap += () => { audio.Sfx("pop"); OpenMap(); };
            hud.Retry += () => { audio.Sfx("pop"); OpenLevel(level); };
            hud.Next += () => { audio.Sfx("pop"); OpenLevel(Levels.World1[Mathf.Min(level.Id, Levels.World1.Length - 1)]); };
        }

        /// <summary>Music / sound switches (the graphics chip is saved but the 2D layer has nothing to drop).</summary>
        void ApplySettings()
        {
            audio.MusicOn = settings.Music;
            audio.SfxOn = settings.Sfx;
            title.Refresh(progress, settings);
        }

        // Smoke-test hooks for unattended builds: -pipLevel N, -pipAuto (start and follow the safe
        // line), -pipPower S (grab a power crystal after S seconds), -pipBoost (hold boost all run),
        // -pipShot base (screenshots, then quit), -pipFps (log the frame rate every 5 s), -pipEndAt S (the
        // autopilot lets go after S s of play: no more steering or refilled hearts, so a Daily Endless run
        // ends on its own and shows its result).
        // On iOS, where launch arguments don't reach the player, the same flags can come from the
        // PIP_ARGS environment variable (xcrun simctl launch with SIMCTL_CHILD_PIP_ARGS="-pipLevel 1 -pipAuto").
        bool auto, smokeBoost, fpsLog;
        string shotBase;
        float powerAt = -1, endAt = -1;
        int shotsTaken;

        /// <summary>Set by PlayMode tests before the scene loads (same effect as the command-line flags).</summary>
        public static int SmokeLevel = -1;
        public static bool SmokeAuto;
        public static float SmokePowerAt = -1;
        public static bool SmokeBoost;

        public RunSim Sim => sim;
        public Camera Cam => cam;
        /// <summary>Current screen (tests).</summary>
        public Mode CurrentMode => mode;
        public Hud Overlay => hud;
        /// <summary>Lens feel: 1 at rest, larger while fast / boosting (ortho zoom-out).</summary>
        public float Zoom => zoom;

        /// <summary>Command-line arguments plus the PIP_ARGS environment variable (iOS).</summary>
        static List<string> LaunchArgs()
        {
            var a = new List<string>(System.Environment.GetCommandLineArgs());
            var env = System.Environment.GetEnvironmentVariable("PIP_ARGS");
            if (!string.IsNullOrEmpty(env)) a.AddRange(env.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
            return a;
        }

        void ReadSmokeArgs()
        {
            if (SmokeLevel >= 0) startLevel = SmokeLevel;
            auto |= SmokeAuto;
            smokeBoost |= SmokeBoost;
            if (SmokePowerAt >= 0) powerAt = SmokePowerAt;
            var a = LaunchArgs();
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] == "-pipAuto") auto = true;
                else if (a[i] == "-pipBoost") smokeBoost = true;
                else if (a[i] == "-pipFps") fpsLog = true;
                else if (a[i] == "-pipLevel" && i + 1 < a.Count && int.TryParse(a[i + 1], out int lv)) startLevel = lv;
                else if (a[i] == "-pipPower" && i + 1 < a.Count) float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out powerAt);
                else if (a[i] == "-pipShot" && i + 1 < a.Count) shotBase = a[i + 1];
                else if (a[i] == "-pipEndAt" && i + 1 < a.Count) float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out endAt);
            }
        }

        float fpsFrom = -1, fpsWorst;
        int fpsFrames;

        /// <summary>-pipFps: average and worst-frame rate over each 5 s window, to the log only.</summary>
        void FpsTick()
        {
            if (!fpsLog) return;
            float now = Time.realtimeSinceStartup;
            if (fpsFrom < 0) { fpsFrom = now; return; }
            fpsFrames++;
            fpsWorst = Mathf.Max(fpsWorst, Time.unscaledDeltaTime);
            float span = now - fpsFrom;
            if (span < 5) return;
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "PIP FPS avg={0:F1} min={1:F1} over {2:F1}s", fpsFrames / span, fpsWorst > 0 ? 1 / fpsWorst : 0, span));
            fpsFrom = now;
            fpsFrames = 0;
            fpsWorst = 0;
        }

        void SmokeTick()
        {
            if (!auto && !smokeBoost && shotBase == null) return;
            float t = Time.timeSinceLevelLoad;
            if (smokeBoost && mode == Mode.Play && sim.Started && !sim.Finished) sim.Boost = true;
            if (auto)
            {
                if (mode == Mode.Intro && Time.unscaledTime - introAt > 1.5f) StartRun();
                if (mode == Mode.Play && (endAt < 0 || sim.Time < endAt))
                {
                    sim.Hearts = 3;
                    sim.Steer(sim.Course.SafeX(sim.Z + 120));
                    if (powerAt >= 0 && t > powerAt && sim.Power <= 0) { sim.Power = Tuning.Power; powerAt = -1; }
                }
            }
            if (shotBase != null)
            {
                float[] at = { 1.0f, 5.5f, 9f };
                if (shotsTaken < at.Length && t > at[shotsTaken]) ScreenCapture.CaptureScreenshot(shotBase + "-" + shotsTaken++ + ".png");
                if (t > 10.5f) Application.Quit();
            }
        }

        /// <summary>Shows a run's slope (or the title's) in the 2D view.</summary>
        void Stage(RunSim s, bool idlePip)
        {
            sim = s;
            var L = s.L;
            down = false;
            AudioListener.pause = false;
            audio.SkiStop();
            audio.Boost(0);
            audio.Duck(false);
            course2.ArchWords = !idlePip; // the title / map backdrop: no START lettering behind the logo
            course2.Bind(s);
            pip2.Sim = idlePip ? null : s;
            benny2.Sim = s;
            particles2.Sim = idlePip ? null : s;
            particles2.Clear();
            particles2.SetWeather(L.Weather);
            backdrop2.SetWorld(L);
        }

        public void GoTitle()
        {
            mode = Mode.Title;
            Stage(titleSim, true);
            hud.ShowRun(false);
            map.Hide();
            title.gameObject.SetActive(true);
            title.Refresh(progress, settings);
            audio.Music(Levels.HomeSong);
        }

        public void OpenMap()
        {
            mode = Mode.Map;
            Stage(titleSim, true);
            hud.ShowRun(false);
            title.gameObject.SetActive(false);
            map.Show(progress);
            audio.Music(Levels.HomeSong);
        }

        float introAt;
        int introFrame;

        /// <summary>The level's intro; its song (Levels.SongFor) starts here and carries into the run.</summary>
        public void OpenLevel(LevelDef L)
        {
            level = L;
            var course = L.Endless ? CourseBuilder.BuildEndless(Levels.DailySeed(System.DateTime.UtcNow)) : CourseBuilder.Build(L, new SysRng());
            Stage(new RunSim(L, course, settings.Speed, new Listener(this)), false);
            mode = Mode.Intro;
            introAt = Time.unscaledTime;
            introFrame = Time.frameCount;
            title.gameObject.SetActive(false);
            map.Hide();
            hud.ShowIntro(sim, progress);
            audio.Music(Levels.SongFor(L));
        }

        void StartRun()
        {
            if (mode != Mode.Intro) return;
            mode = Mode.Play;
            hud.HideIntro();
            sim.Start();
        }

        void SetPaused(bool on)
        {
            if (on && mode != Mode.Play) return;
            if (!on && mode != Mode.Pause) return;
            mode = on ? Mode.Pause : Mode.Play;
            down = false;
            hud.ShowPause(on);
        }

        // Going to the background mid-run pauses, like the web's visibilitychange.
        void OnApplicationPause(bool paused)
        {
            if (paused) SetPaused(true);
        }

        void ShowResult(bool win)
        {
            mode = Mode.Result;
            audio.SkiStop();
            var res = sim.Result(win);
            bool newBest = progress.Record(level, res);
            SaveData.SaveProgress(progress);
            bool hasNext = win && !level.Endless && level.Id < Levels.World1.Length;
            hud.ShowResult(sim, res, newBest, hasNext);
        }

        void Update()
        {
            ReadInput();
            SmokeTick();
            // The 2D views' clock stops while paused (View2D.T / Dt).
            View2D.Step(mode == Mode.Pause, Time.deltaTime);
            FpsTick();
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            if (mode == Mode.Intro || mode == Mode.Play) sim.Advance(dt);
            // Ski hiss, spray and wind follow speed and carve while a run is live.
            if (mode == Mode.Play && sim.Started && !sim.Finished)
            {
                audio.Ski(Pip2D.SkiSpeed(sim), Mathf.Min(1, Mathf.Abs((float)sim.Lean)), !sim.Air && sim.TumbleT <= 0);
                audio.Boost((float)sim.BoostLevel);
            }
            else { audio.SkiStop(); audio.Boost(0); }
            AudioListener.pause = mode == Mode.Pause;
        }

        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        /// <summary>True when the press landed on a button or panel (those taps don't steer).</summary>
        static bool OverUi(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            UiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, UiHits);
            return UiHits.Count > 0;
        }

        void ReadInput()
        {
            bool live = mode == Mode.Intro || mode == Mode.Play;
            var kb = Keyboard.current;
            // Boost is re-decided every frame: Shift held, or a finger that was held still (below).
            sim.Boost = mode == Mode.Play && kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (kb != null)
            {
                sim.KeyLeft = live && (kb.leftArrowKey.isPressed || kb.aKey.isPressed);
                sim.KeyRight = live && (kb.rightArrowKey.isPressed || kb.dKey.isPressed);
                bool jumpKey = kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                if (mode == Mode.Intro && (jumpKey || sim.KeyLeft || sim.KeyRight)) StartRun();
                else if (mode == Mode.Play && jumpKey) sim.Jump();
                if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) SetPaused(mode == Mode.Play);
            }

            var p = Pointer.current;
            if (p == null || !live) { down = false; boosting = false; return; }
            // One web "CSS pixel" of the 390-wide frame, in device pixels.
            float k = Screen.width / 390f;
            Vector2 pos = p.position.ReadValue();
            // The tap that opened the level (a map node, retry, next) doesn't also start it.
            bool fresh = mode != Mode.Intro || (Time.frameCount > introFrame && Time.unscaledTime - introAt > 0.2f);
            if (p.press.wasPressedThisFrame && fresh && !OverUi(pos))
            {
                bool startedNow = mode == Mode.Intro;
                if (startedNow) StartRun();
                down = true;
                start = pos;
                minY = pos.y;
                downT = Time.unscaledTime;
                moved = 0;
                noTap = startedNow;
                // The hold that starts the run ("แตะค้าง…เพื่อเริ่ม") doesn't also boost; lift and press again.
                noBoost = startedNow;
                boosting = false;
                x0 = (float)sim.Tx;
            }
            if (!down) return;
            if (p.press.isPressed && mode == Mode.Play)
            {
                float dxPx = pos.x - start.x;
                sim.Steer(x0 + dxPx / k * 1.25f);
                moved = Mathf.Max(moved, (pos - start).magnitude);
                // Flick up (screen y grows upward here) jumps.
                if (pos.y < minY) minY = pos.y;
                if (pos.y - minY > 44 * k) { sim.Jump(); minY = pos.y; noTap = true; }
                // Held still (under the tap's 12 px) for BoostHold s → boost until the finger lifts; a press
                // that slid first is a steer and never boosts. Taps (< 0.22 s) end before this can trigger.
                if (!boosting && !noBoost && moved < 12 * k && Time.unscaledTime - downT >= (float)Tuning.BoostHold) boosting = true;
                if (boosting) sim.Boost = true;
            }
            if (p.press.wasReleasedThisFrame)
            {
                if (!noTap && mode == Mode.Play && Time.unscaledTime - downT < 0.22f && moved < 12 * k) sim.Jump();
                down = false;
                boosting = false;
            }
        }

        /// <summary>Camera feel: a zoom-out with speed and boost that keeps Pip's spot on screen, a
        /// springy dip on landing, shake; then the backdrop and ground scroll.</summary>
        void LateUpdate()
        {
            // Paused: camera, backdrop and HUD hold still behind the pause panel (no shake, no drift).
            if (mode == Mode.Pause) return;
            float dt = View2D.Dt;
            View2D.H = View2D.DesignHeight(Screen.width, Screen.height);
            float H = View2D.H, SY = View2D.SY;
            float vMin = (float)(sim.L.V0 * sim.Mul * 0.85), vMax = (float)(sim.L.V1 * sim.Mul);
            float spd = sim.Started ? Mathf.Clamp01(((float)sim.V - vMin) / Mathf.Max(1, vMax - vMin)) : 0;
            if (wasAir && !sim.Air && sim.TumbleT <= 0) bumpV += flipped ? 70 : 45;
            flipped = sim.Flip != null || (flipped && sim.Air);
            wasAir = sim.Air;
            bumpV += (-bump * 150 - bumpV * 13) * dt;
            bump += bumpV * dt;
            zoom += (1 + spd * 0.03f + (float)sim.BoostLevel * 0.05f + (sim.Flip != null ? 0.012f : 0) - zoom) * Mathf.Min(1, dt * 3);
            float shake = (float)sim.Shake;
            cam.orthographicSize = H / 2 * zoom;
            // Zoom about Pip's point so he keeps his place at 0.37 of the height.
            float cy = SY + (H / 2 - SY) * zoom + bump * 0.08f + (Random.value - 0.5f) * 8 * shake;
            cam.transform.position = new Vector3(View2D.W / 2 + (Random.value - 0.5f) * 8 * shake, -cy, -10);
            backdrop2.Tick(View2D.T, sim == titleSim ? Mathf.Sin(View2D.T * 0.3f) * 60 : (float)sim.X);
            slope2.Tick(sim.Z);
            hud.Tick(sim, cam, pip2.HeadWorld);
        }
    }
}
