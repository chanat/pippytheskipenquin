// World 1 level table, same values as prototype/web/js/levels.js.
// Difficulty rises level by level: speed, obstacle density, narrower safe corridor,
// then new hazards (logs → Snow Puffs → rolling snowballs → ice + blizzard → rival race).
using System;

namespace Pip.Core
{
    public sealed class LevelDef
    {
        public int Id;
        public string Name, En, Tip;
        public double Len, V0, V1;
        public string Weather, World;
        public double Density, Corridor, Rocks;
        public string[] Q = Array.Empty<string>();
        public int Opts = 2;
        public int Logs, Puffs, Balls, Ice, Forks, Holes, Bears, Combos;
        public string[] Items = Array.Empty<string>();
        public bool Rival, Endless;
        public uint Seed;
    }

    public static class Levels
    {
        public static readonly LevelDef[] World1 =
        {
            new LevelDef { Id = 1, Name = "แรกไถล", En = "First Slide", Tip = "แตะค้างแล้วเลื่อนซ้าย-ขวาเพื่อเลี้ยว เก็บเกล็ดหิมะ หลบต้นไม้", Len = 9000, V0 = 210, V1 = 250, Weather = "sunny", World = "meadow", Density = 0.22, Corridor = 130, Rocks = 0.2, Q = new[] { "picture", "picture" }, Opts = 2, Seed = 101 },
            new LevelDef { Id = 2, Name = "กระโดดดึ๋ง", En = "Hop Hop", Tip = "แตะสั้นๆ หรือปัดขึ้น เพื่อกระโดดข้ามขอนไม้ ก้อนหิน และหลุมน้ำแข็ง · แตะซ้ำกลางอากาศ = ตีลังกา ลอยได้นานขึ้น", Len = 10500, V0 = 225, V1 = 270, Weather = "sunny", World = "meadow", Density = 0.3, Corridor = 120, Rocks = 0.5, Q = new[] { "math1", "math1", "math1" }, Opts = 2, Logs = 10, Holes = 3, Seed = 202 },
            new LevelDef { Id = 3, Name = "ประตูตัวเลข", En = "Number Gates", Tip = "ระวังปุยหิมะจอมป่วน! มันเดินไปมา กระโดดข้ามได้ · กองหิมะสั่นๆ = หมีกำลังจะโผล่ เลี้ยวหลบ! · เก็บผลึกสีฟ้า = ชนอะไรก็พัง", Len = 12000, V0 = 235, V1 = 285, Weather = "snow", World = "meadow", Density = 0.36, Corridor = 110, Rocks = 0.5, Q = new[] { "math1", "math1", "math1", "math1", "math1" }, Opts = 2, Logs = 8, Puffs = 9, Items = new[] { "crystal" }, Bears = 3, Combos = 2, Seed = 303 },
            new LevelDef { Id = 4, Name = "สองทางเลือก", En = "Two Paths", Tip = "ทางแยก! ซ้ายเกล็ดหิมะเยอะแต่มีขอนไม้ ขวาปลอดภัยกว่า · ก้อนหิมะกลิ้งมาจากข้างทาง", Len = 13000, V0 = 245, V1 = 300, Weather = "snow", World = "meadow", Density = 0.38, Corridor = 100, Rocks = 0.5, Q = new[] { "math1", "math1", "math1" }, Opts = 2, Logs = 8, Puffs = 6, Balls = 9, Forks = 2, Items = new[] { "magnet", "crystal", "shield" }, Holes = 3, Bears = 3, Combos = 2, Seed = 404 },
            new LevelDef { Id = 5, Name = "ตัวไหนไม่เข้าพวก", En = "Odd One Out", Tip = "พายุหิมะ มองให้ดี · แผ่นน้ำแข็งทำให้ลื่น · เก็บพลังพิเศษช่วยได้", Len = 14000, V0 = 250, V1 = 310, Weather = "blizzard", World = "meadow", Density = 0.42, Corridor = 95, Rocks = 0.5, Q = new[] { "odd", "odd", "odd", "odd", "odd" }, Opts = 3, Logs = 8, Puffs = 9, Balls = 7, Ice = 10, Items = new[] { "shield", "slow", "crystal", "magnet" }, Holes = 3, Bears = 3, Combos = 3, Seed = 505 },
            new LevelDef { Id = 6, Name = "แข่งกับเบนนี่", En = "Race vs Benny", Tip = "ด่านบอส! แซงเบนนี่ให้ถึงเส้นชัยก่อน ทุกอย่างรวมกัน", Len = 16000, V0 = 260, V1 = 325, Weather = "snow", World = "sunset", Density = 0.46, Corridor = 90, Rocks = 0.5, Q = new[] { "mixed", "mixed", "mixed", "mixed", "mixed", "mixed" }, Opts = 3, Logs = 10, Puffs = 11, Balls = 9, Ice = 7, Forks = 2, Items = new[] { "shield", "crystal", "slow", "magnet" }, Rival = true, Holes = 4, Bears = 4, Combos = 4, Seed = 606 },
        };

        /// <summary>Daily run: everyone gets the same seeded course for the UTC day, so a global
        /// leaderboard compares like with like. Difficulty and speed climb with distance.</summary>
        public static readonly LevelDef Endless = new LevelDef
        {
            Id = 0, Endless = true, Name = "ไถลไม่รู้จบ", En = "Daily Endless",
            Tip = "เส้นทางเดียวกันทั้งโลกในวันนี้ ไปให้ไกลที่สุด ยิ่งไกลยิ่งเร็วและยากขึ้น",
            Len = 90000, V0 = 230, V1 = 440, Weather = "snow", World = "meadow",
        };

        public static uint DailySeed(DateTime utc) => (uint)(utc.Year * 10000 + utc.Month * 100 + utc.Day);

        /// <summary>The looping songs (Resources/Audio/music_&lt;id&gt;_loop): snowday is the home theme (title,
        /// map, World 1), showdown every boss race, then one per world from World 2 on; aurora also
        /// carries the Daily Endless.</summary>
        public static readonly string[] Songs = { "snowday", "pine", "crystal", "lantern", "aurora", "showdown" };

        /// <summary>Six levels per world, the sixth a boss race.</summary>
        public const int PerWorld = 6;

        /// <summary>Title, level map and the World 1 levels.</summary>
        public const string HomeSong = "snowday";

        /// <summary>By world, with its own track for every boss level (Id % 6 == 0); endless → aurora.</summary>
        public static string SongFor(LevelDef L)
        {
            if (L.Endless) return "aurora";
            if (L.Id % PerWorld == 0) return "showdown";
            int world = Math.Max(0, (L.Id - 1) / PerWorld);
            return Songs[Math.Min(world, 4)];
        }
    }
}
