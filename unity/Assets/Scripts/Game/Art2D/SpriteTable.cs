// Which sheet slice plays which role — the one place to fix after looking at the slices (Editor/
// SpriteImport logs "[Pip] slice <name> x y w h"; names are <sheet>_<n> in reading order). Sheets are
// design/sprites2d (BRIEF-2D §2), keyed and sliced into Resources/Sprites2D. A role whose slice is
// missing gets a Placeholder2D blob, so a wrong table shows on screen but never breaks a run.
// Directions are the screen's: carveR = Pip moving toward the right edge (sim Lean > 0).
using System.Collections.Generic;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public struct Slice
    {
        public Sprite S;
        public bool FlipX;
        public bool Real;
        /// <summary>World units per sprite unit that bring this slice to its role's size at scale 1
        /// (characters: per sheet, so every pose of a sheet keeps the same proportions).</summary>
        public float Scale;
        public Vector2 Size => S ? (Vector2)S.bounds.size * Scale : Vector2.zero;
    }

    public static class SpriteTable
    {
        enum Fit { Height, Width, Max }

        sealed class Role
        {
            public string Sheet; public int Index; public bool Flip;
            public Fit Fit; public float Target; public string Fallback;
            public Role(string sheet, int index, Fit fit, float target, string fallback, bool flip = false)
            { Sheet = sheet; Index = index; Fit = fit; Target = target; Fallback = fallback; Flip = flip; }
        }

        // Characters: the reference slice per sheet is sized here, every other pose of that sheet follows.
        static readonly Dictionary<string, (int index, float height)> SheetRef = new Dictionary<string, (int, float)>
        {
            { "s01", (0, 96) }, { "s02", (4, 96) }, { "s03", (0, 100) },
        };

        /// <summary>Placeholder blob colour for a missing Pip slice (his black body).</summary>
        const string PipBody = "#2A2A2E";

        static readonly Dictionary<string, Role> Roles = new Dictionary<string, Role>
        {
            // Pip v4 (5 Nov 2026, classic black penguin). s01 ski poses: straight, straightB (knees bent, the
            // pump's low frame), carveR (moving right, spray behind on the left, leaning right), carveL (moving
            // left, spray on the right); tuck (drawn standing with poles out), jump, backflip (upside down).
            { "pip.straight", new Role("s01", 0, Fit.Height, 0, PipBody) },
            { "pip.straightB", new Role("s01", 1, Fit.Height, 0, PipBody) },
            { "pip.carveR", new Role("s01", 2, Fit.Height, 0, PipBody) },
            { "pip.carveL", new Role("s01", 3, Fit.Height, 0, PipBody) },
            { "pip.tuck", new Role("s01", 4, Fit.Height, 0, PipBody) },
            { "pip.jump", new Role("s01", 5, Fit.Height, 0, PipBody) },
            { "pip.flip", new Role("s01", 6, Fit.Height, 0, PipBody) },
            // s02 emotes: snowball tumble, dizzy sitting (stars merged into its slice), cheer; scared, proud,
            // idle wave (title / map).
            { "pip.tumble", new Role("s02", 0, Fit.Height, 0, "#FFFFFF") },
            { "pip.dizzy", new Role("s02", 1, Fit.Height, 0, PipBody) },
            { "pip.cheer", new Role("s02", 2, Fit.Height, 0, PipBody) },
            { "pip.scared", new Role("s02", 3, Fit.Height, 0, PipBody) },
            { "pip.proud", new Role("s02", 4, Fit.Height, 0, PipBody) },
            { "pip.wave", new Role("s02", 5, Fit.Height, 0, PipBody) },
            // s03 Benny: straight, carveL (moving left, spray on the right), carveR (moving right), cheer.
            { "benny.straight", new Role("s03", 0, Fit.Height, 0, "#8B5A3C") },
            { "benny.carveL", new Role("s03", 1, Fit.Height, 0, "#8B5A3C") },
            { "benny.carveR", new Role("s03", 2, Fit.Height, 0, "#8B5A3C") },
            { "benny.cheer", new Role("s03", 3, Fit.Height, 0, "#8B5A3C") },
            // s04 obstacles-a: four pines, two boulders, log, snowman. Sizes at persp 1 (web art.js × a
            // little, the sprites carry their own snow and outline).
            { "tree0", new Role("s04", 0, Fit.Height, 100, "#28C17F") },
            { "tree1", new Role("s04", 1, Fit.Height, 84, "#28C17F") },
            { "tree2", new Role("s04", 2, Fit.Height, 94, "#28C17F") },
            { "tree3", new Role("s04", 3, Fit.Height, 92, "#28C17F") },
            { "rock0", new Role("s04", 4, Fit.Width, 56, "#8D9DC4") },
            { "rock1", new Role("s04", 5, Fit.Width, 54, "#8D9DC4") },
            { "log", new Role("s04", 6, Fit.Width, 100, "#9C6B45") },
            { "snowman", new Role("s04", 7, Fit.Height, 86, "#FFFFFF") },
            // s05 obstacles-b: puff, rolling snowball, plain mound; bear mound closed (ears peeking), bear
            // popped up; ice hole, ice patch, ramp (no ramp object in the course yet).
            { "puff", new Role("s05", 0, Fit.Height, 50, "#FFFFFF") },
            { "ball", new Role("s05", 1, Fit.Width, 1, "#FFFFFF") },          // × 2·rad at runtime
            { "mound", new Role("s05", 2, Fit.Width, 84, "#FFFFFF") },
            { "bear.peek", new Role("s05", 3, Fit.Width, 84, "#B5774A") },
            { "bear.up", new Role("s05", 4, Fit.Width, 84, "#B5774A") },
            { "hole", new Role("s05", 5, Fit.Width, 1, "#2C5C9E") },          // stretched to the hole's w × h
            { "ice", new Role("s05", 6, Fit.Width, 1, "#C6ECFF") },           // stretched to the patch's w × h
            { "ramp", new Role("s05", 7, Fit.Width, 90, "#C98A5A") },
            // s06 items-gates: coin, shield bubble, magnet, crystal, hourglass (slow), heart; gates in
            // berry, glacier, pine, sunny; start / finish arch.
            { "coin", new Role("s06", 0, Fit.Width, 26, "#FFC93C") },
            { "item.shield", new Role("s06", 1, Fit.Max, 40, "#BFE6FF") },
            { "item.magnet", new Role("s06", 2, Fit.Max, 38, "#E5484D") },
            { "item.crystal", new Role("s06", 3, Fit.Max, 44, "#3FD0F5") },
            { "item.slow", new Role("s06", 4, Fit.Max, 40, "#E0A21B") },
            { "heart", new Role("s06", 5, Fit.Max, 36, "#E5484D") },
            { "gate.berry", new Role("s06", 6, Fit.Width, 1, "#D6336F") },
            { "gate.glacier", new Role("s06", 7, Fit.Width, 1, "#2A6FC4") },
            { "gate.pine", new Role("s06", 8, Fit.Width, 1, "#23845A") },
            { "gate.sunny", new Role("s06", 9, Fit.Width, 1, "#FFC93C") },
            { "arch", new Role("s06", 10, Fit.Width, 1, "#FFF4E0") },
            // s07 vfx (luminance-keyed): sprays ×3, sparkles ×2, coin ring, confetti ×4 (berry, sunny,
            // glacier, mint), speed streaks ×2, landing poof (big cloud), wrong-answer puff. Unit width.
            { "vfx.spray0", new Role("s07", 0, Fit.Max, 1, "#FFFFFF") },
            { "vfx.spray1", new Role("s07", 1, Fit.Max, 1, "#FFFFFF") },
            { "vfx.sparkle0", new Role("s07", 2, Fit.Max, 1, "#FFFFFF") },
            { "vfx.sparkle1", new Role("s07", 3, Fit.Max, 1, "#FFFFFF") },
            { "vfx.ring", new Role("s07", 4, Fit.Max, 1, "#FFC93C") },
            { "vfx.spray2", new Role("s07", 5, Fit.Max, 1, "#FFFFFF") },
            { "vfx.confetti0", new Role("s07", 6, Fit.Max, 1, "#D6336F") },
            { "vfx.confetti1", new Role("s07", 7, Fit.Max, 1, "#FFC93C") },
            { "vfx.confetti2", new Role("s07", 8, Fit.Max, 1, "#2A6FC4") },
            { "vfx.confetti3", new Role("s07", 9, Fit.Max, 1, "#BDF2E9") },
            { "vfx.streak0", new Role("s07", 10, Fit.Max, 1, "#CFE6FF") },
            { "vfx.streak1", new Role("s07", 11, Fit.Max, 1, "#CFE6FF") },
            { "vfx.poof", new Role("s07", 12, Fit.Max, 1, "#FFFFFF") },
            { "vfx.wrong", new Role("s07", 13, Fit.Max, 1, "#888888") },
            // UI kits (v5 toy-3D, 5 Nov 2026), used by the UGUI widgets through UiSprite (Ui/Kit.cs), which
            // falls back to the procedural widget when a slice is missing. Unit size (UI sets its own).
            // u2 HUD kit: round back / pause buttons, glass hearts on / off; timer plank, counter pill,
            // snowflake coin, flag; progress track + gold fill, Pip's / Benny's markers.
            { "ui.back", new Role("u2", 0, Fit.Max, 1, "#2B3A67") },
            { "ui.pause", new Role("u2", 1, Fit.Max, 1, "#2B3A67") },
            { "ui.heart", new Role("u2", 2, Fit.Max, 1, "#E5484D") },
            { "ui.heartOff", new Role("u2", 3, Fit.Max, 1, "#B8BEC8") },
            { "ui.plank", new Role("u2", 4, Fit.Max, 1, "#C98A5A") },
            { "ui.pill", new Role("u2", 5, Fit.Max, 1, "#2B3A67") },
            { "ui.coin", new Role("u2", 6, Fit.Max, 1, "#FFC93C") },
            { "ui.flag", new Role("u2", 7, Fit.Max, 1, "#FF5C93") },
            { "ui.track", new Role("u2", 8, Fit.Max, 1, "#2B3A67") },
            { "ui.fill", new Role("u2", 9, Fit.Max, 1, "#FFC93C") },
            { "ui.markerMe", new Role("u2", 10, Fit.Max, 1, "#2A6FC4") },
            { "ui.markerRival", new Role("u2", 11, Fit.Max, 1, "#8B5A3C") },
            // u3 window kit (reading order puts the gold star before the ribbon: the big panel's row also
            // holds the ribbon above the two stars, and the row is sorted by x).
            { "ui.panel", new Role("u3", 0, Fit.Max, 1, "#BFE6FF") },
            { "ui.star", new Role("u3", 1, Fit.Max, 1, "#FFC93C") },
            { "ui.ribbon", new Role("u3", 2, Fit.Max, 1, "#E5484D") },
            { "ui.starOff", new Role("u3", 3, Fit.Max, 1, "#B8BEC8") },
            // The three button tiers come from the ice kit since 7 Nov 2026 (u3_4-6 stay for the near-square
            // ice buttons, whose stretched snow cap / icicles would not read).
            { "ui.btnBlue", new Role("ice_blue", 0, Fit.Max, 1, "#2A6FC4") },
            { "ui.btnGreen", new Role("ice_green", 0, Fit.Max, 1, "#3DB34A") },
            { "ui.btnIce", new Role("ice_frost", 0, Fit.Max, 1, "#F2FAFF") },
            { "ui.btnIceSq", new Role("u3", 6, Fit.Max, 1, "#F2FAFF") },
            { "ui.plaque", new Role("u3", 7, Fit.Max, 1, "#2B3A67") },
            { "ui.badge", new Role("u3", 8, Fit.Max, 1, "#C23BC9") },
            { "ui.tag", new Role("u3", 9, Fit.Max, 1, "#7CC4F5") },
            // Ice UI kit (7 Nov 2026, matches the t1 title sign): snow cap on top, icicles below.
            { "ui.tagline", new Role("ice_tagline", 0, Fit.Max, 1, "#8FD8FF") },
            { "ui.icePill", new Role("ice_pill", 0, Fit.Max, 1, "#3FA9F5") },
            { "ui.iceTag", new Role("ice_tag", 0, Fit.Max, 1, "#F2FAFF") },
            { "ui.chipOn", new Role("ice_chipOn", 0, Fit.Max, 1, "#3FA9F5") },
            { "ui.chipOff", new Role("ice_chipOff", 0, Fit.Max, 1, "#F2FAFF") },
            { "ui.node", new Role("ice_node", 0, Fit.Max, 1, "#3A9BEB") },
            { "ui.nodeBoss", new Role("ice_nodeBoss", 0, Fit.Max, 1, "#FFC93C") },
            { "ui.nodeLock", new Role("ice_nodeLock", 0, Fit.Max, 1, "#DDE7EF") },
            { "ui.iceBack", new Role("ice_back", 0, Fit.Max, 1, "#3FA9F5") },
            // u4 lettering for the course arches.
            { "ui.start", new Role("u4", 0, Fit.Max, 1, "#FFC93C") },
            { "ui.finish", new Role("u4", 1, Fit.Max, 1, "#FFC93C") },
        };

        /// <summary>The imported UI slice for a ui.* role, or null when it is missing (callers then build
        /// the procedural widget instead).</summary>
        public static Sprite Ui(string role) => Has(role) ? Get(role).S : null;

        /// <summary>Gate banner centre, as a fraction of the gate / arch sprite height from its bottom,
        /// and the banner's width as a fraction of the sprite width (measured on the s06 sheet).</summary>
        public const float GateBannerY = 0.76f, GateBannerW = 0.62f, GateBannerH = 0.24f, ArchBannerY = 0.74f, ArchBannerW = 0.62f;

        static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Slice> cache = new Dictionary<string, Slice>();

        static Sprite Find(string sheet, int index)
        {
            if (!sheets.TryGetValue(sheet, out var all)) sheets[sheet] = all = Resources.LoadAll<Sprite>("Sprites2D/" + sheet);
            string name = sheet + "_" + index;
            foreach (var s in all) if (s.name == name) return s;
            return null;
        }

        /// <summary>The single-sprite backgrounds (b1 sky band, b2 snow tile, b3 edge strip), or null.</summary>
        public static Sprite Background(string id)
        {
            if (!sheets.TryGetValue(id, out var all)) sheets[id] = all = Resources.LoadAll<Sprite>("Sprites2D/" + id);
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>The title sign (t1: ice crystal with the gold "Pippy" lettering), or null when it is not imported.</summary>
        public static Sprite Logo() => Background("t1");

        /// <summary>True when the role exists in the table and its sheet slice is imported.</summary>
        public static bool Has(string role) => Roles.ContainsKey(role) && Get(role).Real;

        public static Slice Get(string role)
        {
            if (cache.TryGetValue(role, out var sl)) return sl;
            if (!Roles.TryGetValue(role, out var r)) throw new KeyNotFoundException("no sprite role " + role);
            var s = Find(r.Sheet, r.Index);
            bool vfx = r.Sheet == "s07";
            sl = new Slice { S = s, FlipX = r.Flip, Real = s != null };
            if (s == null) sl.S = vfx ? Placeholder2D.Puff : Placeholder2D.Blob(role, r.Fallback, role == "hole" || role == "ice");
            var size = (Vector2)sl.S.bounds.size;
            if (SheetRef.TryGetValue(r.Sheet, out var rf))
            {
                var refS = sl.Real ? Find(r.Sheet, rf.index) : null;
                float refH = refS ? refS.bounds.size.y : size.y;
                sl.Scale = rf.height / Mathf.Max(0.001f, refH);
            }
            else
            {
                float basis = r.Fit == Fit.Height ? size.y : r.Fit == Fit.Width ? size.x : Mathf.Max(size.x, size.y);
                sl.Scale = r.Target / Mathf.Max(0.001f, basis);
            }
            cache[role] = sl;
            return sl;
        }

        /// <summary>Every role name (tests, the import check).</summary>
        public static IEnumerable<string> All => Roles.Keys;

        /// <summary>Gate colourway nearest an option colour (hue / brightness): berry, glacier, pine, sunny.</summary>
        public static string GateFor(string hex)
        {
            var c = Pip.Game.Art.Tex.C(hex ?? "#3FA9F5");
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < 0.2f) return "gate.glacier";
            float deg = h * 360;
            if (deg < 25 || deg >= 290) return "gate.berry";
            if (deg < 75) return "gate.sunny";
            if (deg < 170) return "gate.pine";
            return "gate.glacier";
        }
    }
}
