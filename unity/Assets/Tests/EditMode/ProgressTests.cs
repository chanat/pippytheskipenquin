// Progress and settings, as in the web's ps-progress / ps-settings: unlocks, the next level to
// play, keeping the best result, and save → load round trips through a memory store.
using NUnit.Framework;
using Pip.Core;
using Pip.Game;

namespace Pip.Tests
{
    public class ProgressTests
    {
        static LevelDef L(int id) => Levels.World1[id - 1];

        [Test]
        public void OnlyLevelOneIsOpenAtFirst()
        {
            var p = new Progress();
            Assert.IsTrue(p.Unlocked(1));
            Assert.IsFalse(p.Unlocked(2));
            Assert.AreEqual(1, p.NextPlayable().Id);
            Assert.AreEqual(0, p.TotalStars);
            Assert.AreEqual(18, Progress.MaxStars);
        }

        [Test]
        public void OneStarUnlocksTheNextLevel()
        {
            var p = new Progress();
            p.Stars[1] = 1;
            Assert.IsTrue(p.Unlocked(2));
            Assert.IsFalse(p.Unlocked(3));
            Assert.AreEqual(1, p.NextPlayable().Id, "level 1 still short of three stars");
        }

        [Test]
        public void NextPlayableSkipsThreeStarLevels()
        {
            var p = new Progress();
            p.Stars[1] = 3;
            p.Stars[2] = 2;
            Assert.AreEqual(2, p.NextPlayable().Id);
            for (int i = 1; i <= 6; i++) p.Stars[i] = 3;
            Assert.AreEqual(6, p.NextPlayable().Id, "all done: the last level");
            Assert.AreEqual(18, p.TotalStars);
        }

        [Test]
        public void RecordKeepsTheBestOfEach()
        {
            var p = new Progress();
            p.Record(L(1), new RunResult { Win = true, Stars = 2, Total = 1500 });
            p.Record(L(1), new RunResult { Win = true, Stars = 1, Total = 2100 });
            p.Record(L(1), new RunResult { Win = false, Stars = 0, Total = 0 });
            Assert.AreEqual(2, p.StarsOf(1));
            Assert.AreEqual(2100, p.BestOf(1));
        }

        [Test]
        public void EndlessRecordOnlyWhenFarther()
        {
            var p = new Progress();
            Assert.IsTrue(p.Record(Levels.Endless, new RunResult { Distance = 500 }));
            Assert.IsFalse(p.Record(Levels.Endless, new RunResult { Distance = 400 }));
            Assert.AreEqual(500, p.EndlessBest);
            Assert.AreEqual(0, p.TotalStars);
        }

        [Test]
        public void ProgressAndSettingsRoundTrip()
        {
            var keep = SaveData.Store;
            try
            {
                SaveData.Store = new SaveData.Memory();
                var p = new Progress { EndlessBest = 1234 };
                p.Stars[1] = 3;
                p.Stars[2] = 1;
                p.Best[1] = 4200;
                SaveData.SaveProgress(p);
                var q = SaveData.LoadProgress();
                Assert.AreEqual(3, q.StarsOf(1));
                Assert.AreEqual(1, q.StarsOf(2));
                Assert.AreEqual(0, q.StarsOf(3));
                Assert.AreEqual(4200, q.BestOf(1));
                Assert.AreEqual(1234, q.EndlessBest);

                var fresh = SaveData.LoadSettings();
                Assert.AreEqual(1, fresh.Speed, 1e-6);
                Assert.IsTrue(fresh.Music && fresh.Sfx && fresh.HighGfx);
                SaveData.SaveSettings(new Settings { Speed = 1.15, Music = false, Sfx = true, HighGfx = false });
                var s = SaveData.LoadSettings();
                Assert.AreEqual(1.15, s.Speed, 1e-6);
                Assert.IsFalse(s.Music);
                Assert.IsTrue(s.Sfx);
                Assert.IsFalse(s.HighGfx);
            }
            finally { SaveData.Store = keep; }
        }
    }
}
