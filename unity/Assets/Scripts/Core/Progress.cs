// Player progress and settings, as in the web prototype's ps-progress / ps-settings: best stars and
// score per level, best endless distance; a level unlocks once the one before it has a star.
// Storage lives in the game layer (PlayerPrefs); this is the pure bookkeeping.
using System.Collections.Generic;

namespace Pip.Core
{
    public sealed class Progress
    {
        public readonly Dictionary<int, int> Stars = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Best = new Dictionary<int, int>();
        public int EndlessBest;

        public int StarsOf(int id) => Stars.TryGetValue(id, out var s) ? s : 0;
        public int BestOf(int id) => Best.TryGetValue(id, out var s) ? s : 0;

        public bool Unlocked(int id) => id == 1 || StarsOf(id - 1) >= 1;

        /// <summary>First unlocked level still short of three stars (the last level once all are done).</summary>
        public LevelDef NextPlayable()
        {
            foreach (var L in Levels.World1)
                if (Unlocked(L.Id) && StarsOf(L.Id) < 3) return L;
            return Levels.World1[Levels.World1.Length - 1];
        }

        public int TotalStars
        {
            get
            {
                int n = 0;
                foreach (var L in Levels.World1) n += StarsOf(L.Id);
                return n;
            }
        }

        public static int MaxStars => Levels.World1.Length * 3;

        /// <summary>Keeps the best of each record after a run. Returns true for a new endless record.</summary>
        public bool Record(LevelDef L, RunResult r)
        {
            if (L.Endless)
            {
                if (r.Distance <= EndlessBest) return false;
                EndlessBest = r.Distance;
                return true;
            }
            Stars[L.Id] = System.Math.Max(StarsOf(L.Id), r.Stars);
            Best[L.Id] = System.Math.Max(BestOf(L.Id), r.Total);
            return false;
        }
    }

    public sealed class Settings
    {
        /// <summary>Ski speed multiplier: 0.85 slow, 1 normal, 1.15 fast.</summary>
        public double Speed = 1;
        public bool Music = true, Sfx = true;
        /// <summary>High: bloom and sun shadows. Normal: lighter for older phones.</summary>
        public bool HighGfx = true;
    }
}
