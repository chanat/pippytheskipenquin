// The music set built by Tools/make-music.py (Higgsfield demos) and the synthesized effects
// (tools/render-audio) must all be in Resources/Audio. Song loops are 80–92 s, end on sound (no
// silent gap at the wrap) and their head and tail sit within 3 dB (the 1-s crossfade is there);
// jingles are short. Sample checks read the WAV source, since streamed clips have no GetData.
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pip.Core;
using UnityEngine;

namespace Pip.Tests
{
    public class AudioClipTests
    {
        static AudioClip Load(string name)
        {
            var c = Resources.Load<AudioClip>("Audio/" + name);
            Assert.NotNull(c, "missing clip Audio/" + name);
            return c;
        }

        /// <summary>16-bit PCM WAV → interleaved floats (−1..1), channel count and rate.</summary>
        static float[] ReadWav(string name, out int channels, out int rate)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources/Audio", name + ".wav"));
            channels = 0; rate = 0;
            int bits = 0, at = 12;
            while (at + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
                int size = System.BitConverter.ToInt32(bytes, at + 4);
                if (id == "fmt ")
                {
                    channels = System.BitConverter.ToInt16(bytes, at + 10);
                    rate = System.BitConverter.ToInt32(bytes, at + 12);
                    bits = System.BitConverter.ToInt16(bytes, at + 22);
                }
                else if (id == "data")
                {
                    Assert.AreEqual(16, bits, name + " is 16-bit");
                    var s = new float[size / 2];
                    for (int i = 0; i < s.Length; i++) s[i] = System.BitConverter.ToInt16(bytes, at + 8 + i * 2) / 32768f;
                    return s;
                }
                at += 8 + size + (size & 1);
            }
            Assert.Fail(name + " has no data chunk");
            return null;
        }

        static double RmsDb(float[] s, int from, int to)
        {
            double sum = 0;
            for (int i = from; i < to; i++) sum += s[i] * (double)s[i];
            return 20 * System.Math.Log10(System.Math.Max(1e-9, System.Math.Sqrt(sum / System.Math.Max(1, to - from))));
        }

        /// <summary>Songs in the player now; Worlds 2–4's loops wait in Assets/Audio/Future~ until those worlds exist.</summary>
        static readonly string[] Shipped = { "snowday", "aurora", "showdown" };
        static readonly string[] Future = { "pine", "crystal", "lantern" };

        [TestCase("snowday")] [TestCase("aurora")] [TestCase("showdown")]
        public void SongLoopIsSeamless(string id)
        {
            string name = "music_" + id + "_loop";
            var c = Load(name);
            Assert.AreEqual(2, c.channels);
            Assert.That(c.length, Is.InRange(80f, 92f), name + " length");
            var s = ReadWav(name, out int ch, out int rate);
            Assert.AreEqual(44100, rate);
            int n = s.Length, tenth = rate / 10 * ch, half = rate / 2 * ch;
            Assert.Greater(RmsDb(s, n - tenth, n), -40, name + ": the last 0.1 s is not silent");
            double head = RmsDb(s, 0, half), tail = RmsDb(s, n - half, n);
            Assert.AreEqual(tail, head, 3, name + ": first and last 0.5 s match (crossfaded wrap)");
        }

        [TestCase("music_clear")] [TestCase("music_fail")] [TestCase("music_star")] [TestCase("music_question")]
        public void JingleIsShort(string name)
        {
            var c = Load(name);
            Assert.AreEqual(2, c.channels);
            Assert.That(c.length, Is.InRange(0.3f, 6f), name + " length");
        }

        [Test]
        public void EveryShippedSongHasALoop()
        {
            foreach (var id in Shipped) Load("music_" + id + "_loop");
            foreach (var id in Future)
            {
                Assert.IsNull(Resources.Load<AudioClip>("Audio/music_" + id + "_loop"), id + " is parked, not shipped");
                Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "Audio/Future~", "music_" + id + "_loop.wav")), id + " kept in Audio/Future~");
            }
            CollectionAssert.AreEquivalent(Levels.Songs, new System.Collections.Generic.List<string>(Shipped).Concat(Future), "every song is either shipped or parked");
            Assert.IsNull(Resources.Load<AudioClip>("Audio/music_battle_loop"), "old web-rendered music is out of Resources");
        }

        [Test]
        public void MissingSongFallsBackToTheHomeTheme()
        {
            System.Predicate<string> shipped = n => System.Array.Exists(Shipped, id => n == "music_" + id + "_loop") || n == "music_clear";
            Assert.AreEqual("snowday", Pip.Game.AudioService.SongOrFallback("pine", shipped), "World 2's song isn't shipped yet");
            Assert.AreEqual("snowday", Pip.Game.AudioService.SongOrFallback("lantern", shipped));
            Assert.AreEqual("aurora", Pip.Game.AudioService.SongOrFallback("aurora", shipped));
            Assert.AreEqual("clear", Pip.Game.AudioService.SongOrFallback("clear", shipped), "one-shot jingles pass through");
            Assert.AreEqual("nope", Pip.Game.AudioService.SongOrFallback("nope", shipped), "unknown ids aren't songs");
        }

        [TestCase("sfx_coin")] [TestCase("sfx_right")] [TestCase("sfx_wrong")] [TestCase("sfx_jump")] [TestCase("sfx_flip")]
        [TestCase("sfx_land")] [TestCase("sfx_crash")] [TestCase("sfx_power")] [TestCase("sfx_shield")] [TestCase("sfx_star")]
        [TestCase("sfx_tick")] [TestCase("sfx_pop")] [TestCase("sfx_smash")]
        public void EffectIsPresentAndAudible(string name)
        {
            var c = Load(name);
            Assert.Greater(c.length, 0.3f);
            Assert.AreEqual(2, c.channels);
        }

        [Test]
        public void SongFollowsWorldAndBoss()
        {
            string Song(int id) => Levels.SongFor(new LevelDef { Id = id });
            for (int id = 1; id <= 5; id++) Assert.AreEqual("snowday", Song(id), "level " + id);
            Assert.AreEqual("showdown", Song(6));
            Assert.AreEqual("showdown", Levels.SongFor(Levels.World1[5]), "World 1's boss race");
            Assert.AreEqual("aurora", Levels.SongFor(Levels.Endless));
            Assert.AreEqual("pine", Song(7));
            Assert.AreEqual("showdown", Song(12));
            Assert.AreEqual("crystal", Song(13));
            Assert.AreEqual("lantern", Song(19));
            Assert.AreEqual("aurora", Song(25));
            Assert.AreEqual("snowday", Levels.HomeSong);
        }
    }
}
