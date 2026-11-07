// Pip2D.PoseFor picks the sprite pose from the run state (fake sims built from level 1); Pip2D.Cycle
// is the ski-pump cycle that keeps a straight run moving.
using NUnit.Framework;
using Pip.Core;
using Pip.Game.Art2D;
using Mathf = UnityEngine.Mathf;

namespace Pip.Tests
{
    public class Pip2DTests
    {
        static RunSim Sim()
        {
            var L = Levels.World1[0];
            return new RunSim(L, CourseBuilder.Build(L, new SysRng()));
        }

        [Test]
        public void PoseTable()
        {
            Assert.AreEqual(Pose.Wave, Pip2D.PoseFor(null), "no run: the title's wave");
            Assert.AreEqual(Pose.Straight, Pip2D.PoseFor(Sim()));

            var s = Sim(); s.Lean = 0.6;
            Assert.AreEqual(Pose.CarveR, Pip2D.PoseFor(s));
            s = Sim(); s.Lean = -0.6;
            Assert.AreEqual(Pose.CarveL, Pip2D.PoseFor(s));
            s = Sim(); s.Lean = 0.2;
            Assert.AreEqual(Pose.Straight, Pip2D.PoseFor(s), "a small lean stays straight");

            s = Sim(); s.BoostT = 1;
            Assert.AreEqual(Pose.Tuck, Pip2D.PoseFor(s));
            s = Sim(); s.BoostT = 1; s.Lean = 0.8;
            Assert.AreEqual(Pose.Tuck, Pip2D.PoseFor(s), "boost tuck wins over a carve");

            s = Sim(); s.Air = true;
            Assert.AreEqual(Pose.Jump, Pip2D.PoseFor(s));
            s = Sim(); s.Air = true; s.Flip = new Flip { T0 = 0, Dur = 0.6 };
            Assert.AreEqual(Pose.Flip, Pip2D.PoseFor(s));

            s = Sim(); s.TumbleT = 0.5; s.Air = true;
            Assert.AreEqual(Pose.Tumble, Pip2D.PoseFor(s));
            s = Sim(); s.Finished = true;
            Assert.AreEqual(Pose.Cheer, Pip2D.PoseFor(s));
            s = Sim(); s.Failed = true; s.Finished = true;
            Assert.AreEqual(Pose.Dizzy, Pip2D.PoseFor(s), "out of hearts");

            s = Sim(); s.ScaredT = 0.3;
            Assert.AreEqual(Pose.Scared, Pip2D.PoseFor(s));
            s = Sim(); s.SadT = 0.5;
            Assert.AreEqual(Pose.Dizzy, Pip2D.PoseFor(s), "wrong answer");
            s = Sim(); s.HappyT = 0.5;
            Assert.AreEqual(Pose.Proud, Pip2D.PoseFor(s), "right answer");
        }

        [Test]
        public void EveryPoseHasARole()
        {
            foreach (Pose p in System.Enum.GetValues(typeof(Pose)))
                Assert.DoesNotThrow(() => SpriteTable.Get(Pip2D.RoleOf(p)), p.ToString());
            Assert.DoesNotThrow(() => SpriteTable.Get(Pip2D.StraightB));
        }

        [Test]
        public void PumpCycleOnlyOnSnowAndGrowsWithSpeed()
        {
            for (float ph = 0; ph < 13; ph += 0.37f)
            {
                var off = Pip2D.Cycle(0.8f, ph, false);
                Assert.AreEqual(0, off.Bob); Assert.AreEqual(0, off.Sway); Assert.AreEqual(0, off.Wiggle);
                Assert.AreEqual(1, off.ScaleX); Assert.AreEqual(1, off.ScaleY);
            }
            float Amp(float spd)
            {
                float m = 0;
                for (float ph = 0; ph < Mathf.PI * 4; ph += 0.01f) m = Mathf.Max(m, Mathf.Abs(Pip2D.Cycle(spd, ph, true).Bob));
                return m;
            }
            Assert.Greater(Amp(0), 1, "Pip bobs even when slow");
            Assert.Greater(Amp(0.5f), Amp(0) + 0.5f, "more bob with speed");
            Assert.Greater(Amp(1), Amp(0.5f) + 0.5f);
            for (float spd = 0; spd <= 1; spd += 0.25f)
                for (float ph = 0; ph < Mathf.PI * 4; ph += 0.05f)
                {
                    var p = Pip2D.Cycle(spd, ph, true);
                    Assert.That(p.ScaleX, Is.InRange(0.9f, 1.1f));
                    Assert.That(p.ScaleY, Is.InRange(0.9f, 1.1f));
                    Assert.That(Mathf.Abs(p.Sway), Is.LessThanOrEqualTo(Pip2D.PumpSwayDeg + 1e-4f));
                }
            Assert.Greater(Pip2D.PumpOmega(1), Pip2D.PumpOmega(0), "faster pump at speed");
            var low = Pip2D.Cycle(1, Mathf.PI * 1.5f, true);
            Assert.Less(low.Bob, 0, "phase 3π/2 is the low point");
            Assert.Less(low.ScaleY, 1, "squat at the bottom of the pump");
            Assert.Greater(Pip2D.Cycle(1, Mathf.PI * 0.5f, true).ScaleY, 1, "tall at the top");
            Assert.IsFalse(SpriteTable.Has("pip.noSuchRole"), "Has is false for a role the table doesn't know");
        }

        [Test]
        public void SheetsAreImportedAndSliced()
        {
            foreach (var role in SpriteTable.All)
                Assert.IsTrue(SpriteTable.Has(role), role + " has its sheet slice (Pip ▸ Import 2D Sprites)");
            Assert.NotNull(SpriteTable.Background("b1"));
            Assert.NotNull(SpriteTable.Background("b2"));
            Assert.NotNull(SpriteTable.Background("b3"));
            Assert.AreEqual(96, SpriteTable.Get("pip.straight").Size.y, 0.5, "Pip stands 96 units tall");
        }

        /// <summary>Slices per sheet after the v5 import (s04 8, s05 8, s06 11, u2 12, u3 10, u4 2), every ui.*
        /// role resolves to a real sprite, and the stretchable UI slices carry 9-slice borders.</summary>
        [Test]
        public void UiKitsAreSlicedWithBorders()
        {
            var expect = new System.Collections.Generic.Dictionary<string, int> { { "s04", 8 }, { "s05", 8 }, { "s06", 11 }, { "u2", 12 }, { "u3", 10 }, { "u4", 2 } };
            foreach (var kv in expect)
                Assert.AreEqual(kv.Value, UnityEngine.Resources.LoadAll<UnityEngine.Sprite>("Sprites2D/" + kv.Key).Length, kv.Key + " slice count");
            string[] ui = { "ui.back", "ui.pause", "ui.heart", "ui.heartOff", "ui.plank", "ui.pill", "ui.coin", "ui.flag", "ui.track", "ui.fill", "ui.markerMe", "ui.markerRival",
                "ui.panel", "ui.ribbon", "ui.star", "ui.starOff", "ui.btnBlue", "ui.btnGreen", "ui.btnIce", "ui.plaque", "ui.badge", "ui.tag", "ui.start", "ui.finish" };
            foreach (var role in ui) Assert.NotNull(SpriteTable.Ui(role), role + " is in the table and sliced");
            foreach (var role in new[] { "ui.panel", "ui.pill", "ui.track", "ui.fill", "ui.plaque", "ui.tag", "ui.btnBlue", "ui.btnGreen", "ui.btnIce", "ui.plank" })
            {
                var b = SpriteTable.Ui(role).border;
                Assert.IsTrue(b.x > 0 && b.y > 0 && b.z > 0 && b.w > 0, role + " has 9-slice borders");
            }
            // Shape checks that catch a wrong index: the bars are long, the panel is the biggest UI slice,
            // the lettering is wide, the ribbon wider than tall.
            UnityEngine.Vector2 Sz(string r) => SpriteTable.Ui(r).rect.size;
            Assert.Greater(Sz("ui.track").x / Sz("ui.track").y, 4, "track is a long bar");
            Assert.Greater(Sz("ui.fill").x / Sz("ui.fill").y, 4, "fill is a long bar");
            Assert.Greater(Sz("ui.ribbon").x / Sz("ui.ribbon").y, 2.5f, "ribbon is wide");
            Assert.Greater(Sz("ui.start").x / Sz("ui.start").y, 2.5f, "START lettering is wide");
            Assert.Greater(Sz("ui.panel").x * Sz("ui.panel").y, Sz("ui.plaque").x * Sz("ui.plaque").y * 2, "panel is the big window");
            Assert.IsNull(SpriteTable.Ui("ui.noSuchRole"), "missing UI role falls back (null)");
        }
    }
}
