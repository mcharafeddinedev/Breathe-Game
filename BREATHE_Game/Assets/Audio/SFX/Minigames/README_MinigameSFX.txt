Procedural placeholder WAVs (44.1kHz mono) for MinigameSfxProfile.
Replace with real SFX; keep filenames to preserve Unity references.

Cues:
  GameplayStart, GameplayAmbienceLoop, TimeWarning, Success, GoalComplete,
  PrimaryAction, SecondaryAction, TertiaryAction, SpecialEvent

Per-game profile: <Game>/<Game>SfxProfile.asset
Assign on each MinigameDefinition → Minigame Sfx Profile.

Regenerate: python tools/generate_minigame_sfx.py
