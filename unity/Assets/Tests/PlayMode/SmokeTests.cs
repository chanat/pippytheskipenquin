// Loads the real Game scene (2D sprite layer): walks the screens (title → map → intro → pause → results,
// then retry / restart / next level, each of which must leave only the next intro on screen) and lets
// the autopilot ski level 3 with a power crystal, then level 2 holding boost (-pipBoost), checking
// for progress without errors; music follows the screens (snowday ⇄ showdown, ducked under a
// question, no restart when the run starts). Saves
// camera captures (HUD included) to $PIP_SHOT_DIR if set. Progress goes to a memory store, never
// the player's saved game.
using System.Collections;
using System.IO;
using NUnit.Framework;
using Pip.Core;
using Pip.Game;
using Pip.Game.Art2D;
using Pip.Game.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pip.Tests
{
    public class SmokeTests
    {
        // Renders the one 2D camera (the overlay UI isn't in these captures).
        static void Capture(Camera cam, string file)
        {
            string dir = System.Environment.GetEnvironmentVariable("PIP_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            var rt = new RenderTexture(430, 932, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        SaveData.IStore keep;

        [SetUp]
        public void UseMemoryStore()
        {
            keep = SaveData.Store;
            SaveData.Store = new SaveData.Memory();
        }

        [TearDown]
        public void Restore()
        {
            SaveData.Store = keep;
            GameBoot.SmokeLevel = -1;
            GameBoot.SmokeAuto = false;
            GameBoot.SmokePowerAt = -1;
            GameBoot.SmokeBoost = false;
        }

        static bool Shown<T>() where T : Component
        {
            var c = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            return c != null && c.gameObject.activeInHierarchy;
        }

        /// <summary>Clicks the visible button with this GameObject name, like a tap.</summary>
        static void Press(string name)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
                if (b.name == name && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); return; }
            Assert.Fail("no visible button named " + name);
        }

        /// <summary>Only the level intro is up: no result or pause panel left over, run HUD on.</summary>
        static void OnlyIntro(GameBoot boot, string after)
        {
            var hud = boot.Overlay;
            Assert.AreEqual(GameBoot.Mode.Intro, boot.CurrentMode, after + ": back on the intro");
            Assert.IsFalse(hud.ResultShown, after + ": the result panel is gone");
            Assert.IsFalse(hud.PauseShown, after + ": the pause panel is gone");
            Assert.IsTrue(hud.IntroShown, after + ": the intro panel shows");
            Assert.IsTrue(hud.RunShown, after + ": the run HUD is back");
            Assert.IsFalse(boot.Sim.Started, after + ": a fresh run waits on the intro");
        }

        [UnityTest]
        public IEnumerator ScreensFromTitleToResults()
        {
            // Level 1 done with three stars, level 2 with one: level 2 is next and 3 is open.
            SaveData.Store.SetInt("stars.1", 3);
            SaveData.Store.SetInt("stars.2", 1);
            SceneManager.LoadScene("Game");
            yield return null;
            var boot = Object.FindAnyObjectByType<GameBoot>();
            Assert.NotNull(boot, "Game scene has its GameBoot");
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(Shown<TitleScreen>(), "opens on the title");
            Assert.IsFalse(Shown<MapScreen>());
            Capture(boot.Cam, "screen-title.png");

            boot.OpenMap();
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(Shown<MapScreen>(), "map open");
            Assert.IsFalse(Shown<TitleScreen>());
            Assert.IsTrue(GameObject.Find("level 3").GetComponent<Button>().interactable, "one star on level 2 opens level 3");
            Assert.IsFalse(GameObject.Find("level 4").GetComponent<Button>().interactable, "level 4 still locked");
            Capture(boot.Cam, "screen-map.png");

            GameObject.Find("level 2").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(2, boot.Sim.L.Id, "picked level 2 from the map");
            Assert.IsFalse(boot.Sim.Started, "waits on the intro");
            Capture(boot.Cam, "screen-intro.png");

            boot.SendMessage("StartRun");
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(boot.Sim.Started);
            boot.SendMessage("SetPaused", true);
            double z = boot.Sim.Z;
            yield return null;
            var pipBody = Object.FindAnyObjectByType<Pip2D>().transform.Find("pip");
            Vector3 bodyAt = pipBody.localPosition, bodyScale = pipBody.localScale, camAt = boot.Cam.transform.position;
            float clock = View2D.T;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(z, boot.Sim.Z, "paused: the run holds still");
            Assert.AreEqual(clock, View2D.T, "paused: the 2D clock stops");
            Assert.IsTrue(bodyAt == pipBody.localPosition && bodyScale == pipBody.localScale, "paused: Pip's pump freezes");
            Assert.IsTrue(camAt == boot.Cam.transform.position, "paused: the camera holds still");
            Capture(boot.Cam, "screen-pause.png");
            boot.SendMessage("SetPaused", false);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(boot.Sim.Z, z, "resumed");

            boot.SendMessage("ShowResult", true);
            yield return new WaitForSeconds(2.6f);
            Capture(boot.Cam, "screen-result-win.png");
            Assert.GreaterOrEqual(SaveData.Store.GetInt("stars.2", 0), 1, "result saved");
            boot.SendMessage("ShowResult", false);
            yield return new WaitForSeconds(1.6f);
            Capture(boot.Cam, "screen-result-fall.png");
            Assert.IsTrue(boot.Overlay.ResultShown, "fall result up");
            Assert.IsFalse(boot.Overlay.RunShown, "the run HUD steps aside for the result");

            // Fail result → "เล่นอีกครั้ง": the same level's intro, nothing of the result left.
            Press("retry big");
            yield return null;
            OnlyIntro(boot, "retry");
            Assert.AreEqual(2, boot.Sim.L.Id, "retry replays level 2");
            yield return new WaitForSeconds(0.5f);
            Capture(boot.Cam, "screen-retry-intro.png");

            // Pause → "เริ่มใหม่": the pause panel closes too.
            boot.SendMessage("StartRun");
            yield return new WaitForSeconds(0.5f);
            boot.SendMessage("SetPaused", true);
            yield return null;
            Assert.IsTrue(boot.Overlay.PauseShown, "pause panel up");
            Press("restart");
            yield return null;
            OnlyIntro(boot, "restart");
            Assert.AreEqual(2, boot.Sim.L.Id, "restart replays level 2");

            // Win → "ด่านต่อไป": level 3's intro, no result panel.
            boot.SendMessage("StartRun");
            yield return new WaitForSeconds(0.3f);
            boot.SendMessage("ShowResult", true);
            yield return new WaitForSeconds(0.5f);
            Press("next");
            yield return null;
            OnlyIntro(boot, "next");
            Assert.AreEqual(3, boot.Sim.L.Id, "next opens level 3");

            boot.GoTitle();
            yield return null;
            Assert.IsTrue(Shown<TitleScreen>(), "back on the title");
            Assert.IsFalse(boot.Overlay.IntroShown || boot.Overlay.ResultShown || boot.Overlay.PauseShown || boot.Overlay.RunShown, "the title has no run panels");
        }

        [UnityTest]
        public IEnumerator Level3AutopilotWithPowerCrystal()
        {
            GameBoot.SmokeLevel = 3;
            GameBoot.SmokeAuto = true;
            GameBoot.SmokePowerAt = 4;
            SceneManager.LoadScene("Game");
            yield return null;
            var boot = Object.FindAnyObjectByType<GameBoot>();
            Assert.NotNull(boot, "Game scene has its GameBoot");
            yield return new WaitForSeconds(1.0f);
            Capture(boot.Cam, "smoke-0-intro.png");
            yield return new WaitForSeconds(4.5f);
            Assert.IsTrue(boot.Sim.Started, "autopilot started the run");
            Assert.Greater(boot.Sim.Power, 0, "power crystal active");
            Capture(boot.Cam, "smoke-1-power.png");
            yield return new WaitForSeconds(3.5f);
            Capture(boot.Cam, "smoke-2-later.png");
            Assert.Greater(boot.Sim.Z, 1200, "Pip is skiing down the slope");
            Assert.Greater(boot.Sim.Coins, 0, "picked up snowflakes on the safe line");
            Assert.IsTrue(Shown<Pip2D>(), "Pip2D active");
            var course = Object.FindAnyObjectByType<Course2D>();
            Assert.Greater(course.LiveSprites, 10, "the course shows its props, coins and gates as sprites");
            Assert.IsTrue(boot.Cam.orthographic, "one orthographic 2D camera");
            var tracks = Object.FindAnyObjectByType<Trail2D>();
            Assert.NotNull(tracks, "ski tracks");
            Assert.Greater(tracks.Points, 10, "two tracks behind Pip, up to the crest band");
            Assert.Greater(Object.FindAnyObjectByType<Particles2D>().Count, 0, "spray and sparks in the air");
            GameBoot.SmokeLevel = -1;
            GameBoot.SmokeAuto = false;
            GameBoot.SmokePowerAt = -1;
        }

        [UnityTest]
        public IEnumerator Level2AutopilotHoldingBoost()
        {
            GameBoot.SmokeLevel = 2;
            GameBoot.SmokeAuto = true;
            GameBoot.SmokeBoost = true;
            SceneManager.LoadScene("Game");
            yield return null;
            var boot = Object.FindAnyObjectByType<GameBoot>();
            Assert.NotNull(boot, "Game scene has its GameBoot");
            yield return new WaitForSeconds(3.5f);
            Assert.IsTrue(boot.Sim.Started, "autopilot started the run");
            Assert.IsTrue(boot.Sim.Boost, "-pipBoost holds boost while the run is live");
            // A bump can knock the boost down for a moment (tumble, airborne); it is back within 0.45 s.
            float until = Time.time + 6;
            while (boot.Sim.BoostLevel <= 0.9 && Time.time < until) yield return null;
            Assert.Greater(boot.Sim.BoostLevel, 0.9, "boost ramps to full on the snow");
            yield return null;
            var pill = GameObject.Find("boost");
            Assert.NotNull(pill, "the เร่ง pill shows while boosting");
            Assert.Greater(boot.Zoom, 1.02f, "lens opens up with speed and boost");
            Capture(boot.Cam, "smoke-boost.png");
            GameBoot.SmokeBoost = false;
        }

        [UnityTest]
        public IEnumerator MusicFollowsTheScreens()
        {
            SaveData.Store.SetInt("set.music", 1);
            SceneManager.LoadScene("Game");
            yield return null;
            var boot = Object.FindAnyObjectByType<GameBoot>();
            var audio = Object.FindAnyObjectByType<AudioService>();
            Assert.NotNull(audio, "Game scene has its AudioService");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(Shown<TitleScreen>(), "opens on the title");
            Assert.AreEqual("snowday", audio.Current, "the title plays the home theme");
            Assert.IsTrue(audio.CurrentLoops, "and loops it");
            var src = GameObject.Find("song snowday").GetComponent<AudioSource>();
            Assert.IsTrue(src.isPlaying && src.loop, "snowday's source is playing, looped");

            boot.OpenMap();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("snowday", audio.Current, "the map keeps the same song");
            Assert.IsFalse(audio.IsFading("snowday"), "…without restarting it");

            boot.OpenLevel(Levels.World1[5]);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("showdown", audio.Current, "the boss race has its own track");
            Assert.IsTrue(audio.IsFading("snowday"), "snowday fades out under it");
            yield return new WaitForSeconds(0.7f);
            Assert.IsFalse(audio.IsFading("snowday"), "the crossfade is over within a second");
            var song = GameObject.Find("song showdown");
            Assert.IsTrue(song.GetComponent<AudioSource>().isPlaying, "showdown playing");

            boot.SendMessage("StartRun");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(boot.Sim.Started);
            Assert.AreEqual("showdown", audio.Current);
            Assert.AreSame(song, GameObject.Find("song showdown"), "starting the run doesn't restart its song");

            var row = boot.Sim.Course.Rows[0];
            boot.Sim.Out.ShowQuestion(row);
            boot.Sim.Out.Duck(true);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(AudioService.Ducked, audio.DuckTarget, 1e-4, "a question ducks the music");
            Assert.Less(song.GetComponent<AudioSource>().volume, 0.4f * 0.7f, "the song is under its full level");
            boot.Sim.Out.Duck(false);

            boot.GoTitle();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("snowday", audio.Current, "back on the title, back to the home theme");
            Assert.IsTrue(audio.IsFading("showdown"));
        }
    }
}
