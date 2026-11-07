// Progress and settings on disk (PlayerPrefs). Tests swap in a memory store so they never touch
// the player's saved game.
using System.Collections.Generic;
using Pip.Core;
using UnityEngine;

namespace Pip.Game
{
    public static class SaveData
    {
        public interface IStore
        {
            int GetInt(string key, int fallback);
            float GetFloat(string key, float fallback);
            void SetInt(string key, int value);
            void SetFloat(string key, float value);
            void Save();
        }

        sealed class Prefs : IStore
        {
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
            public void Save() => PlayerPrefs.Save();
        }

        public sealed class Memory : IStore
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            public int GetInt(string key, int fallback) => Values.TryGetValue(key, out var v) ? (int)v : fallback;
            public float GetFloat(string key, float fallback) => Values.TryGetValue(key, out var v) ? v : fallback;
            public void SetInt(string key, int value) => Values[key] = value;
            public void SetFloat(string key, float value) => Values[key] = value;
            public void Save() { }
        }

        public static IStore Store = new Prefs();

        public static Progress LoadProgress()
        {
            var p = new Progress();
            foreach (var L in Levels.World1)
            {
                int s = Store.GetInt("stars." + L.Id, 0), b = Store.GetInt("best." + L.Id, 0);
                if (s > 0) p.Stars[L.Id] = s;
                if (b > 0) p.Best[L.Id] = b;
            }
            p.EndlessBest = Store.GetInt("endlessBest", 0);
            return p;
        }

        public static void SaveProgress(Progress p)
        {
            foreach (var kv in p.Stars) Store.SetInt("stars." + kv.Key, kv.Value);
            foreach (var kv in p.Best) Store.SetInt("best." + kv.Key, kv.Value);
            Store.SetInt("endlessBest", p.EndlessBest);
            Store.Save();
        }

        public static Settings LoadSettings() => new Settings
        {
            Speed = Store.GetFloat("set.speed", 1),
            Music = Store.GetInt("set.music", 1) != 0,
            Sfx = Store.GetInt("set.sfx", 1) != 0,
            HighGfx = Store.GetInt("set.gfx", 1) != 0,
        };

        public static void SaveSettings(Settings s)
        {
            Store.SetFloat("set.speed", (float)s.Speed);
            Store.SetInt("set.music", s.Music ? 1 : 0);
            Store.SetInt("set.sfx", s.Sfx ? 1 : 0);
            Store.SetInt("set.gfx", s.HighGfx ? 1 : 0);
            Store.Save();
        }
    }
}
