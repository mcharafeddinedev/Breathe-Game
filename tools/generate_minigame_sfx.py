#!/usr/bin/env python3
"""
Generate placeholder minigame SFX as 44.1kHz mono 16-bit WAVs + Unity .meta + MinigameSfxProfile assets.

Run from repo root:  python tools/generate_minigame_sfx.py

Replace WAVs with real recordings later; keep filenames so profile references stay valid.
"""

from __future__ import annotations

import math
import os
import struct
import uuid
import wave

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
SFX_ROOT = os.path.join(ROOT, "BREATHE_Game", "Assets", "Audio", "SFX", "Minigames")
SR = 44100

GAMES = [
    "Balloon",
    "Bubbles",
    "Skydive",
    "StoneSkip",
    "Stargaze",
    "Sailboat",
]

CUES = [
    "GameplayStart",
    "GameplayAmbienceLoop",
    "TimeWarning",
    "Success",
    "GoalComplete",
    "PrimaryAction",
    "SecondaryAction",
    "TertiaryAction",
    "SpecialEvent",
]


def clamp16(x: float) -> int:
    return int(max(-32768, min(32767, x)))


def write_wav(path: str, samples: list[float]) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    peak = max(abs(s) for s in samples) or 1.0
    scale = 0.92 / peak
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        for s in samples:
            w.writeframes(struct.pack("<h", clamp16(s * scale)))


def env_adsr(n: int, a: int, d: int, s: float, r: int) -> list[float]:
    """Length n, simple ADSR envelope 0..1."""
    out = []
    for i in range(n):
        if i < a:
            e = i / max(a, 1)
        elif i < a + d:
            e = 1.0 - (1.0 - s) * ((i - a) / max(d, 1))
        elif i < n - r:
            e = s
        else:
            t = (i - (n - r)) / max(r, 1)
            e = s * (1.0 - t)
        out.append(e)
    return out


def tone(freq: float, n: int, vol: float = 0.4) -> list[float]:
    return [vol * math.sin(2.0 * math.pi * freq * i / SR) for i in range(n)]


def synth_gameplay_start(seed: int) -> list[float]:
    n = int(0.28 * SR)
    out = [0.0] * n
    f0, f1 = 380 + seed * 12, 1150 + seed * 18
    env = env_adsr(n, int(0.02 * SR), int(0.06 * SR), 0.6, int(0.08 * SR))
    for i in range(n):
        t = i / n
        f = f0 + (f1 - f0) * (t * t)
        out[i] = env[i] * 0.45 * math.sin(2.0 * math.pi * f * i / SR)
    return out


def synth_ambience(seed: int) -> list[float]:
    """Seamless ~2.5 s loop: sum of integer-period sines."""
    duration = 2.5
    n = int(duration * SR)
    out = [0.0] * n
    f1 = 2.0 + seed * 0.08
    f2 = 5.0 + seed * 0.05
    f3 = 11.0
    for i in range(n):
        t = i / SR
        v = (
            0.12 * math.sin(2.0 * math.pi * f1 * t)
            + 0.08 * math.sin(2.0 * math.pi * f2 * t)
            + 0.05 * math.sin(2.0 * math.pi * f3 * t)
        )
        out[i] = v * 0.35
    return out


def synth_time_warning(seed: int) -> list[float]:
    beep = int(0.12 * SR)
    gap = int(0.08 * SR)
    f = 520 + seed * 10
    a = tone(f, beep, 0.42)
    silence = [0.0] * gap
    b = tone(f * 1.02, beep, 0.42)
    return a + silence + b


def synth_success(seed: int) -> list[float]:
    n = int(0.42 * SR)
    freqs = [523.25, 659.25, 783.99]
    freqs = [f * (1.0 + seed * 0.002) for f in freqs]
    env = env_adsr(n, int(0.04 * SR), int(0.12 * SR), 0.55, int(0.15 * SR))
    out = [0.0] * n
    for i in range(n):
        s = sum(math.sin(2.0 * math.pi * f * i / SR) for f in freqs) / 3.0
        out[i] = env[i] * 0.38 * s
    return out


def synth_goal_complete(seed: int) -> list[float]:
    n = int(0.55 * SR)
    out = [0.0] * n
    notes = [392, 523, 659, 784]
    notes = [n0 * (1.0 + seed * 0.001) for n0 in notes]
    seg = n // 4
    env = env_adsr(n, int(0.02 * SR), int(0.1 * SR), 0.5, int(0.18 * SR))
    for k, fq in enumerate(notes):
        for j in range(seg):
            i = k * seg + j
            if i >= n:
                break
            out[i] += 0.35 * math.sin(2.0 * math.pi * fq * i / SR)
    for i in range(n):
        out[i] *= env[i]
    return out


def synth_primary(seed: int) -> list[float]:
    n = int(0.055 * SR)
    f = 780 + seed * 25
    env = env_adsr(n, 3, int(0.02 * SR), 0.2, int(0.03 * SR))
    return [env[i] * 0.5 * math.sin(2.0 * math.pi * f * i / SR) for i in range(n)]


def synth_secondary(seed: int) -> list[float]:
    n = int(0.04 * SR)
    f = 1200 + seed * 30
    env = env_adsr(n, 2, int(0.015 * SR), 0.15, int(0.02 * SR))
    return [env[i] * 0.45 * math.sin(2.0 * math.pi * f * i / SR) for i in range(n)]


def synth_tertiary(seed: int) -> list[float]:
    n = int(0.028 * SR)
    f = 1800
    env = env_adsr(n, 2, 5, 0.1, 5)
    return [env[i] * 0.4 * math.sin(2.0 * math.pi * f * i / SR) for i in range(n)]


def synth_special(seed: int) -> list[float]:
    n = int(0.35 * SR)
    out = [0.0] * n
    # pseudo-random deterministic sparkle
    for i in range(n):
        ph = (i * 7919 + seed * 131) % 1000
        f = 2000 + (ph % 800)
        env = math.exp(-3.5 * i / n)
        out[i] = env * 0.22 * math.sin(2.0 * math.pi * f * i / SR)
    return out


SYNTH = {
    "GameplayStart": synth_gameplay_start,
    "GameplayAmbienceLoop": synth_ambience,
    "TimeWarning": synth_time_warning,
    "Success": synth_success,
    "GoalComplete": synth_goal_complete,
    "PrimaryAction": synth_primary,
    "SecondaryAction": synth_secondary,
    "TertiaryAction": synth_tertiary,
    "SpecialEvent": synth_special,
}


def wav_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def profile_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def profile_asset(name: str, mapping: dict[str, str]) -> str:
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: 9b5c337eede6b90449d2ae25a753cbd9, type: 3}",
        f"  m_Name: {name}",
        "  m_EditorClassIdentifier: Breathe.Data::Breathe.Data.MinigameSfxProfile",
        f"  _gameplayStart: {{fileID: 8300000, guid: {mapping['GameplayStart']}, type: 3}}",
        f"  _gameplayAmbienceLoop: {{fileID: 8300000, guid: {mapping['GameplayAmbienceLoop']}, type: 3}}",
        f"  _timeWarning: {{fileID: 8300000, guid: {mapping['TimeWarning']}, type: 3}}",
        f"  _success: {{fileID: 8300000, guid: {mapping['Success']}, type: 3}}",
        f"  _goalComplete: {{fileID: 8300000, guid: {mapping['GoalComplete']}, type: 3}}",
        f"  _primaryAction: {{fileID: 8300000, guid: {mapping['PrimaryAction']}, type: 3}}",
        f"  _secondaryAction: {{fileID: 8300000, guid: {mapping['SecondaryAction']}, type: 3}}",
        f"  _tertiaryAction: {{fileID: 8300000, guid: {mapping['TertiaryAction']}, type: 3}}",
        f"  _specialEvent: {{fileID: 8300000, guid: {mapping['SpecialEvent']}, type: 3}}",
        "",
    ]
    return "\n".join(lines)


def main() -> None:
    profile_guids: dict[str, str] = {}
    for gi, game in enumerate(GAMES):
        seed = gi * 7 + 3
        guids: dict[str, str] = {}
        mapping: dict[str, str] = {}
        game_dir = os.path.join(SFX_ROOT, game)
        for cue in CUES:
            g = uuid.uuid4().hex
            guids[cue] = g
            mapping[cue] = g
            fn = os.path.join(game_dir, f"{cue}.wav")
            samples = SYNTH[cue](seed)
            write_wav(fn, samples)
            meta_path = fn + ".meta"
            with open(meta_path, "w", encoding="utf-8") as f:
                f.write(wav_meta(g))

        pg = uuid.uuid4().hex
        profile_guids[game] = pg
        prof_path = os.path.join(game_dir, f"{game}SfxProfile.asset")
        with open(prof_path + ".meta", "w", encoding="utf-8") as f:
            f.write(profile_meta(pg))
        with open(prof_path, "w", encoding="utf-8") as f:
            f.write(profile_asset(f"{game}SfxProfile", mapping))

    readme = os.path.join(SFX_ROOT, "README_MinigameSFX.txt")
    with open(readme, "w", encoding="utf-8") as f:
        f.write(
            "Procedural placeholder WAVs (44.1kHz mono) for MinigameSfxProfile.\n"
            "Replace with real SFX; keep filenames to preserve Unity references.\n\n"
            "Cues:\n"
            "  GameplayStart, GameplayAmbienceLoop, TimeWarning, Success, GoalComplete,\n"
            "  PrimaryAction, SecondaryAction, TertiaryAction, SpecialEvent\n\n"
            "Per-game profile: <Game>/<Game>SfxProfile.asset\n"
            "Assign on each MinigameDefinition → Minigame Sfx Profile.\n\n"
            "Regenerate: python tools/generate_minigame_sfx.py\n"
        )

    # Print YAML lines for MinigameDefinition patches
    print("Profile GUIDs (paste into MinigameDefinition _minigameSfxProfile):")
    for game, pg in profile_guids.items():
        print(f"  {game}: {pg}")


if __name__ == "__main__":
    main()
