#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""make-music.py — builds the game's music clips from the Higgsfield mp3 demos.

    python3 unity/Tools/make-music.py [--src design/higgsfield/full] [--out unity/Assets/Resources/Audio]

Loops (90-s songs → music_<id>_loop.wav): afconvert to 16-bit 44.1 kHz stereo, trim trailing
silence (last sample above -50 dBFS), cut a fade-out tail (anything after the last 0.25-s window
within 6 dB of the song's body RMS), crossfade the final 1.0 s into the first 1.0 s (equal power) so
the wrap is seamless, then normalize to -16 dBFS RMS. Jingles (music_<id>.wav): trimmed the same way
(no fade-tail cut, no crossfade), a 10-ms tail fade, normalized to -14 dBFS RMS. Normalizing never
clips: a block peak limiter takes at most 3 dB off the loudest peaks and the makeup gain is lowered
if more would be needed (the report shows the RMS actually reached). Needs only /usr/bin/afconvert
(macOS) and the Python stdlib. Re-run after replacing a demo, then reimport in Unity.
"""
import argparse, array, math, os, subprocess, sys, tempfile, wave

SR = 44100
LOOPS = [  # song id → source demo (m1b-snow-day-b is the unused alternate)
    ("snowday", "m1a-snow-day-a"), ("pine", "m2-pine-trail"), ("crystal", "m3-crystal-glide"),
    ("lantern", "m4-lantern-lullaby"), ("aurora", "m5-aurora-rush"), ("showdown", "m6-rival-showdown"),
]
JINGLES = [("clear", "j1-level-clear"), ("star", "j2-star"), ("fail", "j3-fall"), ("question", "j4-question")]
LOOP_RMS, JINGLE_RMS = -16.0, -14.0
SILENCE_DB, FADE_TAIL_DB, XFADE_S = -50.0, 6.0, 1.0
CEIL = 32767 * 0.98  # about -0.2 dBFS
MAX_LIMIT_DB = 3.0


def db(x):
    return 20 * math.log10(max(x, 1e-12))


def decode(mp3, tmp):
    out = os.path.join(tmp, os.path.basename(mp3)[:-4] + ".wav")
    subprocess.run(["/usr/bin/afconvert", "-f", "WAVE", "-d", "LEI16@44100", "-c", "2", mp3, out], check=True)
    w = wave.open(out)
    assert w.getnchannels() == 2 and w.getsampwidth() == 2 and w.getframerate() == SR, mp3
    a = array.array("h", w.readframes(w.getnframes()))
    w.close()
    if sys.byteorder != "little":
        a.byteswap()
    return [float(v) for v in a[0::2]], [float(v) for v in a[1::2]]


def rms(L, R, i0=0, i1=None):
    i1 = len(L) if i1 is None else i1
    n = max(1, i1 - i0)
    s = sum(v * v for v in L[i0:i1]) + sum(v * v for v in R[i0:i1])
    return math.sqrt(s / (2 * n)) / 32768


def trim_silence(L, R):
    thr = 32768 * 10 ** (SILENCE_DB / 20)
    end = len(L)
    while end > 0 and abs(L[end - 1]) <= thr and abs(R[end - 1]) <= thr:
        end -= 1
    return L[:end], R[:end]


def cut_fade_tail(L, R):
    """Drops a fade-out / ringing ending: keeps up to the last 0.25-s window within 6 dB of the body."""
    body = db(rms(L, R))
    win = SR // 4
    end = len(L)
    while end - win > len(L) // 2 and db(rms(L, R, end - win, end)) < body - FADE_TAIL_DB:
        end -= win
    return L[:end], R[:end]


def crossfade_loop(L, R):
    """Equal-power crossfade of the last XFADE_S into the first: the clip wraps without a seam."""
    f = int(XFADE_S * SR)
    n = len(L) - f
    oL, oR = L[:n], R[:n]
    for i in range(f):
        k = i / f
        gi, go = math.sin(k * math.pi / 2), math.cos(k * math.pi / 2)
        oL[i] = L[i] * gi + L[n + i] * go
        oR[i] = R[i] * gi + R[n + i] * go
    return oL, oR


def normalize(L, R, target_db):
    """Gain to target RMS; a block limiter (2.5-ms blocks, ±1 block look-around, 80-ms release) keeps
    peaks under CEIL, taking at most MAX_LIMIT_DB off — beyond that the makeup gain is lowered."""
    peak = max(max(abs(v) for v in L), max(abs(v) for v in R))
    g = 10 ** ((target_db - db(rms(L, R))) / 20)
    g = min(g, CEIL / peak * 10 ** (MAX_LIMIT_DB / 20))
    blk = 110
    nb = (len(L) + blk - 1) // blk
    need = []
    for b in range(nb):
        p = max(max(abs(v) for v in L[b * blk:(b + 1) * blk]), max(abs(v) for v in R[b * blk:(b + 1) * blk])) * g
        need.append(min(1.0, CEIL / p) if p > 0 else 1.0)
    win = [min(need[max(0, b - 1):b + 2]) for b in range(nb)]
    rise = math.exp(blk / (0.08 * SR) * math.log(10 ** (MAX_LIMIT_DB / 20)))  # full recovery in ≈80 ms
    gb, prev = [], 1.0
    for b in range(nb):
        prev = min(win[b], prev * rise)
        gb.append(prev)
    oL, oR = [0.0] * len(L), [0.0] * len(R)
    for i in range(len(L)):
        c = i / blk - 0.5
        b0 = min(nb - 1, max(0, int(math.floor(c))))
        b1 = min(nb - 1, b0 + 1)
        t = min(1.0, max(0.0, c - b0))
        gg = g * (gb[b0] + (gb[b1] - gb[b0]) * t)
        oL[i] = max(-CEIL, min(CEIL, L[i] * gg))
        oR[i] = max(-CEIL, min(CEIL, R[i] * gg))
    return oL, oR, db(min(gb))


def write(path, L, R):
    a = array.array("h", [0] * (2 * len(L)))
    a[0::2] = array.array("h", (int(round(v)) for v in L))
    a[1::2] = array.array("h", (int(round(v)) for v in R))
    if sys.byteorder != "little":
        a.byteswap()
    w = wave.open(path, "wb")
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(a.tobytes())
    w.close()


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.dirname(os.path.dirname(here))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", default=os.path.join(repo, "design", "higgsfield", "full"))
    ap.add_argument("--out", default=os.path.join(repo, "unity", "Assets", "Resources", "Audio"))
    args = ap.parse_args()
    with tempfile.TemporaryDirectory() as tmp:
        for kind, items in (("loop", LOOPS), ("jingle", JINGLES)):
            for sid, src in items:
                L, R = decode(os.path.join(args.src, src + ".mp3"), tmp)
                raw = len(L) / SR
                L, R = trim_silence(L, R)
                if kind == "loop":
                    L, R = cut_fade_tail(L, R)
                    L, R = crossfade_loop(L, R)
                    name = "music_%s_loop.wav" % sid
                else:
                    f = min(len(L), int(0.01 * SR))
                    for i in range(f):
                        k = 1 - (i + 1) / f
                        L[len(L) - f + i] *= k
                        R[len(R) - f + i] *= k
                    name = "music_%s.wav" % sid
                L, R, lim = normalize(L, R, LOOP_RMS if kind == "loop" else JINGLE_RMS)
                write(os.path.join(args.out, name), L, R)
                h = int(0.5 * SR)
                print("%-26s %-20s %6.2fs -> %6.3fs  rms %6.1f dBFS  peak %5.1f  limit %4.1f dB  head/tail 0.5s %6.1f / %6.1f" % (
                    name, src, raw, len(L) / SR, db(rms(L, R)),
                    db(max(max(abs(v) for v in L), max(abs(v) for v in R)) / 32768), lim,
                    db(rms(L, R, 0, h)), db(rms(L, R, len(L) - h))))


if __name__ == "__main__":
    main()
