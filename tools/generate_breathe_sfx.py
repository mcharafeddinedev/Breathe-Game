#!/usr/bin/env python3
"""
Generate placeholder Kirby-ish / chiptune-style SFX as 16-bit mono PCM WAV files.
Targets SfxLibrary slots — output: BREATHE_Game/Assets/Audio/SFX/Global/

Requires: Python 3.9+ (stdlib only: wave, math, struct, pathlib).

Usage (from repo root):
  python tools/generate_breathe_sfx.py
"""

from __future__ import annotations

import math
import struct
import wave
from pathlib import Path

SAMPLE_RATE = 44100
MASTER = 0.42  # avoid harsh clipping; Unity can normalize per-clip


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _out_dir() -> Path:
    return _repo_root() / "BREATHE_Game" / "Assets" / "Audio" / "SFX" / "Global"


def _clamp16(x: float) -> int:
    v = int(round(x))
    return max(-32768, min(32767, v))


def _write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    frames = bytearray()
    for s in samples:
        frames.extend(struct.pack("<h", _clamp16(s * 32767.0)))
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        w.writeframes(frames)


def _lin_env(n: int, attack: int, release: int) -> list[float]:
    """0..1 envelope: linear attack, linear release, sustain 1 in between."""
    out = []
    for i in range(n):
        if i < attack:
            e = i / max(1, attack)
        elif i >= n - release:
            e = (n - 1 - i) / max(1, release)
        else:
            e = 1.0
        out.append(e)
    return out


def _exp_decay_env(n: int, attack: int = 8) -> list[float]:
    out = []
    for i in range(n):
        if i < attack:
            e = i / max(1, attack)
        else:
            t = (i - attack) / max(1.0, n - attack - 1)
            e = math.exp(-5.5 * t)
        out.append(e)
    return out


def tone_samples(
    hz: float,
    n: int,
    env: list[float],
    harmonic2: float = 0.12,
    harmonic3: float = 0.06,
) -> list[float]:
    out = []
    w = 2.0 * math.pi / SAMPLE_RATE
    for i in range(n):
        ph = w * hz * i
        s = math.sin(ph)
        s += harmonic2 * math.sin(2 * ph)
        s += harmonic3 * math.sin(3 * ph)
        out.append(s * env[i] * MASTER)
    return out


def chirp_samples(f0: float, f1: float, n: int, env: list[float]) -> list[float]:
    out = []
    w_scale = 2.0 * math.pi / SAMPLE_RATE
    phase = 0.0
    for i in range(n):
        t = i / max(1, n - 1)
        hz = f0 + (f1 - f0) * t
        phase += w_scale * hz
        s = math.sin(phase) + 0.15 * math.sin(2 * phase)
        out.append(s * env[i] * MASTER)
    return out


def noise_burst(n: int, env: list[float], brightness: float = 0.35) -> list[float]:
    # deterministic pseudo-noise (no random import for reproducibility)
    out = []
    x = 0xC0FFEE
    for i in range(n):
        x ^= x << 13
        x ^= x >> 17
        x ^= x << 5
        x &= 0xFFFFFFFF
        r = (x & 0xFFFF) / 32768.0 - 1.0
        out.append(r * brightness * env[i] * MASTER)
    return out


def two_note(
    hz1: float,
    hz2: float,
    dur1: float,
    gap: float,
    dur2: float,
    vol2: float = 0.85,
) -> list[float]:
    n1 = int(dur1 * SAMPLE_RATE)
    n_gap = int(gap * SAMPLE_RATE)
    n2 = int(dur2 * SAMPLE_RATE)
    e1 = _exp_decay_env(n1, attack=6)
    e2 = _exp_decay_env(n2, attack=5)
    s = tone_samples(hz1, n1, e1)
    s.extend([0.0] * n_gap)
    s2 = tone_samples(hz2, n2, e2)
    for i in range(len(s2)):
        s2[i] *= vol2
    s.extend(s2)
    return s


def arpeggio(hzs: list[float], note_len: float, gap: float = 0.012) -> list[float]:
    out: list[float] = []
    n_note = int(note_len * SAMPLE_RATE)
    n_gap = int(gap * SAMPLE_RATE)
    for k, hz in enumerate(hzs):
        env = _exp_decay_env(n_note, attack=4)
        seg = tone_samples(hz, n_note, env, harmonic2=0.18, harmonic3=0.08)
        out.extend(seg)
        if k < len(hzs) - 1:
            out.extend([0.0] * n_gap)
    return out


def build_all() -> dict[str, list[float]]:
    """Cue name -> samples (float -1..1)."""
    cues: dict[str, list[float]] = {}

    # --- UI ---
    cues["SFX_UIButtonConfirm"] = two_note(523.25, 659.25, 0.045, 0.008, 0.055)  # C5 -> E5
    cues["SFX_UIButtonCancel"] = two_note(392.0, 311.13, 0.06, 0.01, 0.08)  # G4 down toward Eb4-ish
    n_hover = int(0.035 * SAMPLE_RATE)
    cues["SFX_UIButtonHover"] = tone_samples(880.0, n_hover, _exp_decay_env(n_hover, attack=3), harmonic2=0.08)

    n_open = int(0.14 * SAMPLE_RATE)
    env_open = _lin_env(n_open, attack=int(0.04 * SAMPLE_RATE), release=int(0.05 * SAMPLE_RATE))
    cues["SFX_UIPanelOpen"] = chirp_samples(320.0, 880.0, n_open, env_open)

    n_close = int(0.1 * SAMPLE_RATE)
    env_close = _lin_env(n_close, attack=5, release=int(0.035 * SAMPLE_RATE))
    cues["SFX_UIPanelClose"] = chirp_samples(700.0, 240.0, n_close, env_close)

    # --- Countdown ---
    n_tick = int(0.04 * SAMPLE_RATE)
    cues["SFX_CountdownTickNumber"] = tone_samples(990.0, n_tick, _exp_decay_env(n_tick, attack=2), harmonic2=0.2)

    n_go = int(0.22 * SAMPLE_RATE)
    env_go = _exp_decay_env(n_go, attack=10)
    go = chirp_samples(180.0, 520.0, n_go, env_go)
    # brighten with quick noise tail
    n_tail = int(0.04 * SAMPLE_RATE)
    tail = noise_burst(n_tail, _exp_decay_env(n_tail, attack=1), brightness=0.22)
    for i in range(min(len(go), len(tail))):
        go[i] += tail[i]
    cues["SFX_CountdownGo"] = go

    # --- Tutorial & calibration ---
    n_tut = int(0.18 * SAMPLE_RATE)
    env_tut = _lin_env(n_tut, attack=int(0.05 * SAMPLE_RATE), release=int(0.04 * SAMPLE_RATE))
    tut = chirp_samples(400.0, 1200.0, n_tut, env_tut)
    n_spark = int(0.06 * SAMPLE_RATE)
    spark = noise_burst(n_spark, _exp_decay_env(n_spark, attack=2), brightness=0.25)
    tut.extend(spark)
    cues["SFX_TutorialPopupOpen"] = tut

    cues["SFX_TutorialPopupContinue"] = two_note(659.25, 783.99, 0.04, 0.006, 0.05)  # E5 -> G5

    n_cal_s = int(0.35 * SAMPLE_RATE)
    env_cal = _lin_env(n_cal_s, attack=20, release=int(0.08 * SAMPLE_RATE))
    cues["SFX_CalibrationStart"] = chirp_samples(220.0, 660.0, n_cal_s, env_cal)

    cues["SFX_CalibrationComplete"] = arpeggio([392.0, 493.88, 587.33, 783.99], 0.055, 0.008)

    # --- Results ---
    n_res = int(0.2 * SAMPLE_RATE)
    env_res = _lin_env(n_res, attack=int(0.06 * SAMPLE_RATE), release=int(0.07 * SAMPLE_RATE))
    cues["SFX_ResultScreenAppear"] = chirp_samples(200.0, 600.0, n_res, env_res)

    cues["SFX_ResultPersonalBest"] = arpeggio([523.25, 659.25, 783.99, 1046.5], 0.045, 0.006)

    cues["SFX_CelebrationStinger"] = arpeggio([392.0, 493.88, 587.33, 698.46, 783.99], 0.05, 0.007)

    cues["SFX_ResultContinue"] = two_note(440.0, 554.37, 0.05, 0.01, 0.06)  # A4 -> C#5

    return cues


def main() -> None:
    out = _out_dir()
    cues = build_all()
    for name, samples in cues.items():
        path = out / f"{name}.wav"
        _write_wav(path, samples)
        print(f"Wrote {path.relative_to(_repo_root())} ({len(samples) / SAMPLE_RATE:.3f}s)")
    print(f"\nDone. {len(cues)} files -> {out}")


if __name__ == "__main__":
    main()
