// Game audio. Music is the Higgsfield soundtrack (Tools/make-music.py → Assets/Resources/Audio):
// one seamless 90-s loop per song (music_<id>_loop, ids in Levels.Songs) that keeps playing across
// screens — asking for the song already on is a no-op, a different one crossfades in over 0.6 s —
// plus one-shot jingles (clear / fail replace the song; star / question play on top of it). Effects
// are the web prototype's procedural Web Audio rendered to clips (tools/render-audio, sfx_*). The ski
// carve hiss, spray and wind are synthesized here from filtered noise and driven every frame like
// A.ski() in audio.js; so is the boost whoosh (Unity-only), a band-passed noise swell looped while
// the player holds boost.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pip.Game
{
    public sealed class AudioService : MonoBehaviour
    {
        const int SampleRate = 44100;
        const float Master = 0.17f; // web master level into the compressor
        const float DuckLevel = 0.45f / 0.8f; // web music bus 0.8 → 0.45 while a question shows
        // The demos are normalized to −16 dBFS RMS (jingles −14); these bring them back to about the
        // web mix's music level (≈ −24 dBFS) under the effects. Untested by ear — tune on device.
        const float MusicLevel = 0.4f, JingleLevel = 0.35f;
        const float Crossfade = 0.6f;

        bool musicOn = true;
        public bool SfxOn = true;

        /// <summary>Off stops the music; on resumes the song last asked for (the current screen's).</summary>
        public bool MusicOn
        {
            get => musicOn;
            set
            {
                if (musicOn == value) return;
                musicOn = value;
                if (!value) { FadeOutSong(0.12f); return; }
                if (wanted != null) Music(wanted);
            }
        }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> sfx = new List<AudioSource>();
        readonly List<Song> fading = new List<Song>();
        Song song;
        string wanted; // the looping song the current screen wants (survives MusicOn off/on)
        readonly AudioSource[] jingles = new AudioSource[3]; // stars overlap, each at its own pitch
        int nextJingle;
        float duck = 1, duckTarget = 1;

        sealed class Song
        {
            public string Id;
            public GameObject Go;
            public AudioSource Src;
            public float Fade = 1, Gain = MusicLevel;
            public bool Stopping;
        }

        /// <summary>The song now playing (not fading out), or null.</summary>
        public string Current => song?.Id;
        /// <summary>True while the current song is a loop that is playing (or scheduled to).</summary>
        public bool CurrentLoops => song != null && song.Src.loop;
        /// <summary>True while a song with this id is fading out.</summary>
        public bool IsFading(string id) => fading.Exists(f => f.Id == id);
        /// <summary>Where the music volume is heading: 1, or the duck level while a question shows.</summary>
        public float DuckTarget => duckTarget;
        public const float Ducked = DuckLevel;

        // Ski layer: three looping filtered-noise voices, plus the boost whoosh.
        AudioSource hiss, spray, wind, whoosh;
        bool skiOn;
        float hG, flG, hF = 1200, spG, wG, gwG, wF = 400, flRate = 0.0004f;
        float hGt, flGt, hFt = 1200, spGt, wGt, gwGt, wFt = 400, flRateT = 0.0004f;

        void Awake()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("Audio")) clips[c.name] = c;
            for (int i = 0; i < 10; i++) sfx.Add(Source("sfx " + i));
            hiss = Source("ski hiss", Noise("hiss", Filter.Band, 1200, 0.7f), true);
            spray = Source("ski spray", Noise("spray", Filter.High, 5500, 0.6f), true);
            wind = Source("wind", Noise("wind", Filter.Band, 400, 1.8f), true);
            whoosh = Source("boost whoosh", Whoosh(), true);
            for (int i = 0; i < jingles.Length; i++) jingles[i] = Source("jingle " + i);
        }

        AudioSource Source(string name, AudioClip clip = null, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            s.clip = clip;
            s.loop = loop;
            if (loop) { s.volume = 0; }
            return s;
        }

        AudioClip Clip(string name) => clips.TryGetValue(name, out var c) ? c : null;

        // ---------- music ----------

        /// <summary>Song ids (Levels.Songs) loop until another is asked for: the same id keeps playing,
        /// a new one starts while the old fades out over 0.6 s. clear / fail play once in its place.</summary>
        public void Music(string id)
        {
            // Worlds 2–4's songs are parked in Assets/Audio/Future~ (not shipped yet): their levels get the home theme.
            string use = SongOrFallback(id, n => Clip(n) != null);
            if (use != id)
            {
                Debug.LogWarning("music: no clip for " + id + ", playing " + use);
                id = use;
            }
            var loop = Clip("music_" + id + "_loop");
            var once = loop == null ? Clip("music_" + id) : null;
            if (loop == null && once == null) { Debug.LogWarning("music: no clip for " + id); return; }
            if (loop != null) wanted = id;
            else wanted = null; // after a result jingle nothing resumes until the next screen asks
            if (!musicOn) return;
            if (loop != null && song != null && song.Id == id) return;
            FadeOutSong(loop != null ? Crossfade : 0.25f);
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "music: {0}", id);
            var s = new Song { Id = id, Go = new GameObject("song " + id), Gain = loop != null ? MusicLevel : JingleLevel };
            s.Go.transform.SetParent(transform, false);
            s.Src = s.Go.AddComponent<AudioSource>();
            s.Src.playOnAwake = false;
            s.Src.spatialBlend = 0;
            s.Src.clip = loop ?? once;
            s.Src.loop = loop != null;
            s.Src.volume = s.Gain * (loop != null ? duck : 1);
            s.Src.PlayScheduled(AudioSettings.dspTime + 0.06);
            song = s;
        }

        /// <summary>The song id to play for id: id itself when its loop (or one-shot) clip exists, else
        /// Levels.HomeSong for a song in Levels.Songs whose clip isn't shipped; other ids pass through.</summary>
        public static string SongOrFallback(string id, System.Predicate<string> hasClip)
        {
            if (hasClip("music_" + id + "_loop") || hasClip("music_" + id)) return id;
            return System.Array.IndexOf(Pip.Core.Levels.Songs, id) >= 0 ? Pip.Core.Levels.HomeSong : id;
        }

        /// <summary>A one-shot jingle over the music (star, question). pitch 1 = as recorded.</summary>
        public void Jingle(string id, float pitch = 1)
        {
            if (!musicOn) return;
            var c = Clip("music_" + id);
            if (c == null) return;
            var j = jingles[nextJingle];
            nextJingle = (nextJingle + 1) % jingles.Length;
            j.pitch = pitch;
            j.clip = c;
            j.volume = JingleLevel;
            j.Play();
        }

        public void StopMusic()
        {
            wanted = null;
            FadeOutSong(0.12f);
        }

        void FadeOutSong(float time)
        {
            if (song == null) return;
            song.Stopping = true;
            fading.Add(song);
            StartCoroutine(FadeOut(song, time));
            song = null;
        }

        IEnumerator FadeOut(Song s, float time)
        {
            float t = 0, from = s.Src.volume;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                // Equal-power fade so a crossfade doesn't dip in the middle.
                s.Fade = Mathf.Cos(Mathf.Clamp01(t / time) * Mathf.PI / 2);
                if (s.Src) s.Src.volume = from * s.Fade;
                yield return null;
            }
            fading.Remove(s);
            Destroy(s.Go);
        }

        public void Duck(bool on) => duckTarget = on ? DuckLevel : 1;

        // ---------- effects ----------

        float lastCoin = -1;

        public void Sfx(string id)
        {
            if (!SfxOn) return;
            if (id == "coin")
            {
                if (Time.unscaledTime - lastCoin < 0.05f) return;
                lastCoin = Time.unscaledTime;
            }
            var c = Clip("sfx_" + id);
            if (c == null) return;
            AudioSource pick = null;
            foreach (var s in sfx) if (!s.isPlaying) { pick = s; break; }
            if (pick == null)
            {
                pick = sfx[0];
                sfx.RemoveAt(0);
                sfx.Add(pick);
            }
            pick.pitch = id == "coin" ? Random.Range(0.99f, 1.01f) : 1;
            pick.clip = c;
            pick.volume = 1;
            pick.Play();
        }

        // ---------- ski + wind ----------

        /// <summary>Per frame during a run: speed01 / carve01 in 0..1, onSnow false while airborne or tumbling.</summary>
        public void Ski(float speed01, float carve01, bool onSnow)
        {
            if (!SfxOn) { SkiStop(); return; }
            if (!skiOn)
            {
                skiOn = true;
                foreach (var s in new[] { hiss, spray, wind }) if (!s.isPlaying) { s.time = Random.value * s.clip.length * 0.9f; s.Play(); }
            }
            float sp = Mathf.Clamp01(speed01), cv = Mathf.Clamp01(carve01), snow = onSnow ? 1 : 0;
            hGt = snow * Mathf.Min(1, sp * 4) * (0.13f + 0.1f * sp) * (1 + 0.35f * cv);
            flGt = hGt * (0.15f + 0.25f * cv);
            hFt = 500 + 1500 * sp + 1500 * cv;
            spGt = snow * cv * cv * (0.25f + 0.75f * sp) * 0.06f;
            flRateT = 0.0002f + 0.0006f * sp;
            wGt = (0.03f + 0.27f * sp * sp) * (onSnow ? 1 : 1.2f);
            gwGt = wGt * 0.4f;
            wFt = 260 + 1000 * sp;
        }

        /// <summary>Per frame: the run's boost level (0..1). The whoosh loop plays at half that volume; 0 stops it.</summary>
        public void Boost(float level)
        {
            if (!SfxOn || level <= 0.001f)
            {
                if (whoosh.isPlaying) whoosh.Stop();
                whoosh.volume = 0;
                return;
            }
            whoosh.volume = 0.5f * Mathf.Clamp01(level);
            if (!whoosh.isPlaying) whoosh.Play();
        }

        public void SkiStop()
        {
            if (!skiOn) return;
            skiOn = false;
            hGt = flGt = spGt = wGt = gwGt = 0;
        }

        static float Approach(float cur, float target, float tau, float dt) => cur + (target - cur) * (1 - Mathf.Exp(-dt / Mathf.Max(0.001f, tau)));

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            duck = Approach(duck, duckTarget, 0.08f, dt);
            if (song != null && song.Src.loop) song.Src.volume = song.Gain * duck;

            hG = Approach(hG, hGt, hGt > hG ? 0.07f : 0.03f, dt);
            flG = Approach(flG, flGt, 0.1f, dt);
            hF = Approach(hF, hFt, 0.1f, dt);
            spG = Approach(spG, spGt, 0.06f, dt);
            flRate = Approach(flRate, flRateT, 0.2f, dt);
            wG = Approach(wG, wGt, skiOn ? 0.3f : 0.12f, dt);
            gwG = Approach(gwG, gwGt, skiOn ? 0.3f : 0.12f, dt);
            wF = Approach(wF, wFt, 0.35f, dt);
            float t = Time.unscaledTime;
            // Grainy scrape on the hiss and slow gusts on the wind (the web uses very slow noise "LFOs").
            float flutter = (Mathf.PerlinNoise(t * flRate * SampleRate, 0.3f) * 2 - 1) * flG;
            float gust = Mathf.PerlinNoise(t * 0.9f, 7.1f) * 2 - 1;
            float gustF = (Mathf.PerlinNoise(t * 0.6f, 3.7f) * 2 - 1) * 120;
            hiss.volume = Mathf.Max(0, hG + flutter);
            hiss.pitch = Mathf.Clamp(hF / 1200f, 0.35f, 3f);
            spray.volume = spG;
            wind.volume = Mathf.Max(0, wG + gwG * gust);
            wind.pitch = Mathf.Clamp((wF + gustF) / 400f, 0.5f, 3f);
            if (!skiOn && hG + spG + wG < 0.0005f)
                foreach (var s in new[] { hiss, spray, wind }) if (s.isPlaying) s.Stop();
        }

        // ---------- noise beds ----------

        enum Filter { Band, High }

        /// <summary>3 s of white noise through a Web Audio-style biquad, looped seamlessly.</summary>
        static AudioClip Noise(string name, Filter type, float f, float q)
        {
            var clip = AudioClip.Create(name, SampleRate * 3, 1, SampleRate, false);
            clip.SetData(Filtered(name, SampleRate * 3, SampleRate / 5, type, f, q), 0);
            return clip;
        }

        /// <summary>The boost whoosh: 0.8 s of band-passed noise (900 Hz, wide) whose level swells from 55%
        /// to full and back each pass, so the loop breathes like rushing air and joins without a seam.</summary>
        static AudioClip Whoosh()
        {
            int n = SampleRate * 4 / 5;
            var data = Filtered("whoosh", n, SampleRate / 10, Filter.Band, 900, 0.8f);
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Sin(Mathf.PI * i / n);
                data[i] *= 0.55f + 0.45f * s * s;
            }
            var clip = AudioClip.Create("whoosh", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>n samples of seeded white noise through the biquad, at the master level, with an
        /// extra tail of fade samples crossfaded into the head so the loop point is seamless.</summary>
        static float[] Filtered(string name, int n, int fade, Filter type, float f, float q)
        {
            var raw = new float[n + fade];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < raw.Length; i++) raw[i] = (float)(rng.NextDouble() * 2 - 1);
            double w0 = 2 * Mathf.PI * f / SampleRate, cs = System.Math.Cos(w0), sn = System.Math.Sin(w0);
            double b0, b1, b2, a0, a1, a2;
            if (type == Filter.Band)
            {
                double alpha = sn / (2 * q);
                b0 = alpha; b1 = 0; b2 = -alpha; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
            }
            else
            {
                double alpha = sn / (2 * System.Math.Pow(10, q / 20)); // Web Audio high-pass Q is in dB
                b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
            }
            var y = new float[raw.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                double x0 = raw[i];
                double y0 = (b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x0; y2 = y1; y1 = y0;
                y[i] = (float)y0 * Master;
            }
            // Crossfade the extra tail into the head so the loop point is seamless.
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = y[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = y[n + i] * (1 - k) + y[i] * k;
            }
            return data;
        }
    }
}
