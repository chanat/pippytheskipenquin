// The top band and the ground of the 2D slope. Backdrop2D: the b1 sky / mountain picture across the
// top (its bottom on the crest, sliding a little against Pip's x like the web's parallax layers, warm
// tint on the sunset world) with three soft clouds drifting over it, plus the blizzard haze. Slope2D:
// the b2 snow tile filling the slope and scrolling up with pz, and the b3 edge strip tiled down the
// left side (mirrored on the right), scrolling with it. Sky, ground and strips fall back to flat
// colours / nothing when the sheets are missing.
using Pip.Core;
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public sealed class Backdrop2D : MonoBehaviour
    {
        SpriteRenderer sky, haze;
        readonly SpriteRenderer[] clouds = new SpriteRenderer[3];
        string world = "meadow";
        bool blizzard;

        public void Init()
        {
            sky = Kit2D.SR(transform, "sky b1", View2D.Sky);
            sky.sprite = SpriteTable.Background("b1");
            for (int i = 0; i < 3; i++) clouds[i] = Kit2D.SR(transform, "cloud", View2D.Clouds);
            haze = Kit2D.SR(transform, "blizzard haze", View2D.Haze);
        }

        public void SetWorld(LevelDef L)
        {
            world = L.World ?? "meadow";
            blizzard = L.Weather == "blizzard";
        }

        /// <summary>px: Pip's x (or the title's slow drift); t: seconds.</summary>
        public void Tick(float t, float px)
        {
            float H = View2D.H;
            if (sky.sprite != null)
            {
                var b = sky.sprite.bounds.size;
                float w = View2D.W * 1.3f, h = w * b.y / b.x;
                // Bottom of the picture just under the crest line, so the hills meet the snow.
                Kit2D.Put(sky, new Slice { S = sky.sprite, Scale = 1 }, View2D.W / 2 - px * 0.05f, View2D.SkyH + 6, w, h, Kit2D.Bottom);
                sky.color = world == "sunset" ? Tex.C("#FFE2D2") : Color.white;
            }
            for (int i = 0; i < 3; i++)
            {
                float cx = (t * (5 + i * 3) + i * 170) % (View2D.W + 180) - 90 - px * 0.03f;
                float s = 0.75f + (i % 2) * 0.25f;
                Kit2D.Put(clouds[i], new Slice { S = Placeholder2D.Cloud, Scale = 1 }, cx, 26 + i * 19, 90 * s, 45 * s, Kit2D.Centre);
                Kit2D.Alpha(clouds[i], 0.75f);
            }
            haze.gameObject.SetActive(blizzard);
            if (blizzard)
            {
                Kit2D.Put(haze, new Slice { S = Placeholder2D.Haze, Scale = 1 }, View2D.W / 2, H / 2, View2D.W * 1.4f, H * 1.2f, Kit2D.Centre);
                Kit2D.Alpha(haze, 0.9f + Mathf.Sin(t * 1.7f) * 0.1f, Tex.C("#F2F7FF"));
            }
        }
    }

    public sealed class Slope2D : MonoBehaviour
    {
        // Strip: 70 % of the first pass's scale on the 56 % crop (inner edge at x ≈ 59, was 106, so the
        // slope only got wider); Out: how far the strip's outer edge sits past the screen edge.
        const float Tile = 260, Strip = 81, Out = 22, BankW = 28, BankPeriod = 128;
        SpriteRenderer ground, left, right, bankL, bankR;
        Sprite stripSprite;

        public void Init()
        {
            var tile = SpriteTable.Background("b2");
            ground = Kit2D.SR(transform, "snow b2", View2D.Ground);
            ground.sprite = tile ? tile : Placeholder2D.White;
            ground.drawMode = SpriteDrawMode.Tiled;
            ground.tileMode = SpriteTileMode.Continuous;
            if (!tile) ground.color = Tex.C("#FBFDFF");
            stripSprite = SpriteTable.Background("b3");
            left = Kit2D.SR(transform, "edge b3 left", View2D.Edges);
            right = Kit2D.SR(transform, "edge b3 right", View2D.Edges);
            foreach (var r in new[] { left, right })
            {
                r.sprite = stripSprite;
                r.drawMode = SpriteDrawMode.Tiled;
                r.enabled = stripSprite != null;
            }
            right.flipX = true;
            // A drawn snow bank over each strip's crisp inner cut.
            bankL = Kit2D.SR(transform, "snow bank left", View2D.Edges + 1);
            bankR = Kit2D.SR(transform, "snow bank right", View2D.Edges + 1);
            foreach (var r in new[] { bankL, bankR })
            {
                r.sprite = Placeholder2D.Bank;
                r.drawMode = SpriteDrawMode.Tiled;
                r.enabled = stripSprite != null;
            }
            bankR.flipX = true;
        }

        public void Tick(double pz)
        {
            float H = View2D.H;
            float top = View2D.SkyH - 30;
            // Ground: tiles of Tile units moving up with pz; the sheet's tile is drawn at its own aspect.
            var gs = ground.sprite.bounds.size;
            float k = Tile / gs.x;
            float scroll = (float)(pz % Tile);
            if (scroll < 0) scroll += Tile;
            float coverH = H - top + Tile * 2, coverW = View2D.W * 1.4f;
            ground.transform.localScale = new Vector3(k, k, 1);
            ground.size = new Vector2(coverW / k, coverH / k);
            ground.transform.localPosition = View2D.P(View2D.W / 2, top - Tile + coverH / 2 - scroll);

            if (stripSprite == null) return;
            var ss = stripSprite.bounds.size;
            float ks = Strip / ss.x, period = ss.y * ks;
            float sScroll = (float)(pz % period);
            if (sScroll < 0) sScroll += period;
            float sh = H - top + period * 2;
            foreach (var r in new[] { left, right })
            {
                // The right side runs half a tile out of phase, so the two banks never mirror each other.
                float phase = r == right ? period / 2 : 0;
                float sc = (sScroll + phase) % period;
                r.transform.localScale = new Vector3(ks, ks, 1);
                r.size = new Vector2(ss.x, sh / ks);
                float x = r == left ? -Out + Strip / 2 : View2D.W + Out - Strip / 2;
                r.transform.localPosition = View2D.P(x, top - period + sh / 2 - sc);
            }
            float bScroll = (float)(pz % BankPeriod);
            if (bScroll < 0) bScroll += BankPeriod;
            float bh = H - top + BankPeriod * 2;
            var bs = bankL.sprite.bounds.size;
            foreach (var r in new[] { bankL, bankR })
            {
                float phase = r == bankR ? BankPeriod * 0.37f : 0;
                float bsc = (bScroll + phase) % BankPeriod;
                r.transform.localScale = new Vector3(BankW / bs.x, BankPeriod / bs.y, 1);
                r.size = new Vector2(bs.x, bh / (BankPeriod / bs.y));
                // Centred a little inside the cut: snow covers the last few units of the strip.
                float inner = Strip - Out;
                float x = r == bankL ? inner - 4 : View2D.W - inner + 4;
                r.transform.localPosition = View2D.P(x, top - BankPeriod + bh / 2 - bsc);
            }
        }
    }
}
