Last Updated: Mar 29, 2026

# How It Works — Technical Systems Overview

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## What This Document Covers

How the game runs, how the input works, how the architecture is organized, and what design principles the code enforces.

Scripts live under `BREATHE_Game/Assets/Scripts/`, organized into five assemblies with strict one-way dependencies:

- **Breathe.Utility** — Math helpers, converters, animation utilities
- **Breathe.Data** — ScriptableObject configs and data definitions
- **Breathe.Input** — Breath input interface and implementations
- **Breathe.Gameplay** — Game logic, AI, course, environment, overlays
- **Breathe.UI** — Menu screens, HUD, result display

---

## The Big Picture

The player blows toward a custom-built fan device. The fan's rotation is measured by a microcontroller and transmitted to the game over USB. That signal becomes a normalized breath power level (0–1) that drives different mechanics depending on the active minigame — filling a sail, inflating a balloon, skipping a stone.

**breath → fan spin → breath power → minigame action.**

One input, zero failure. The player controls only the intensity of their breath. The game navigates itself, guarantees a positive outcome, and ends every session with a celebration of personal effort.

---

## Procedural Generation

The Unity scene is intentionally minimal — just the boats and manager objects. Everything else is generated at runtime: ocean surface, buoy-marked race course (randomly selected from a layout pool each run), obstacles, environmental zones, background scenery, boat visual effects, and UI overlays. Object pooling prevents garbage collection spikes during gameplay.

This keeps the scene maintainable, makes every run different, and eliminates the art pipeline as a bottleneck during development.

---

## Breath Input Pipeline

Three input sources feed through a common interface (`IBreathInput`), all producing the same normalized 0–1 signal:

- **Custom hardware** (`FanBreathInput`) — DC motor/propeller generates voltage proportional to breath effort, read via Arduino over USB serial on a background thread
- **Microphone** (`MicBreathInput`) — Low-frequency breath energy isolated from ambient noise, with per-session calibration
- **Simulated** (`SimulatedBreathInput`) — Keyboard/gamepad ramp for development without hardware

All sources pass through the same processing: range normalization, EMA smoothing, dead zone, and clamping (`SignalProcessing.cs`). Tuning parameters live in a `BreathConfig` ScriptableObject. Gameplay code reads from `BreathInputManager` and is unaware of the active source.

---

## Breath Power and Propulsion

`BreathPowerSystem` is the universal power variable that all minigames read from. It applies a response curve that compresses the lower input range — light breath produces a proportionally larger effect, keeping the experience rewarding for players with limited lung capacity.

What changes per minigame is interpretation. In the sailboat, breath power becomes wind. The boat always moves at a base speed (the player is never stuck), and breath adds speed on top. The sail visually inflates in proportion to effort. Environmental zones modify the multiplier, driving natural breath variation. All tuning lives in ScriptableObject assets.

---

## Race Structure

A session follows a state machine (`GameStateManager`): Menu → Level Select → Calibration (mic only) → Tutorial → Countdown → Playing → Celebration. Each minigame provides its own tutorial content, countdown text, and result stats through the `IMinigame` interface.

The minigame abstraction is handled through `IMinigame` (the contract) and `MinigameBase` (shared boilerplate: analytics lifecycle, session logging, registration). The result overlay, scoring, data persistence, and debug tools all work generically for any game that implements the contract. Per-game metadata is defined in `MinigameDefinition` ScriptableObject assets.

A soft time cap guarantees the player always finishes — the finish line moves to the player if the session runs long.

---

## AI Companions (Sailboat)

Two AI boats race alongside the player with randomized personalities (speed, turning, reaction time). Key behaviors: Perlin noise speed variation for natural movement, simulated breathing cycles on their sails, rubber-banding to keep races close, finish-line slowdown so the player wins, and obstacle stun effects. In a rare edge case with very low player effort, one AI may be allowed to win to preserve competitive credibility. Tuning lives in `AIConfig`.

---

## Environmental Zones (Sailboat)

Six zone types spawn along the course — headwind, tailwind, crosswind, cyclone, doldrums, and choppy water — each demanding a different breathing response. Every zone is escapable within a time limit even without breathing; effort accelerates the escape. Zones are placed with weighted random selection and enforced spacing. Both player and AI boats respond to zones through a shared interface.

---

## Scoring and Analytics

Every session captures race performance (breath time, peak intensity, longest blow, completion time, zones conquered) and breath pattern analysis (sustained segments, bursts, average intensity, activity ratio, pattern classification). Personal bests are stored per minigame ID and compared on the result screen.

Hardware spin-down compensation adjusts for the fan propeller's 1–3 second coast after the player stops blowing. Raw and adjusted values are both recorded. These metrics describe relative effort and personal trends — not calibrated measurements.

Each completed round is saved as an NDJSON record in a daily log file. A safety monitor suggests rest breaks after prolonged high-effort breathing.

---

## Hardware Signal Path

```
Player breath
  → Fan propeller spins
  → DC motor generates proportional voltage
  → Arduino reads voltage, transmits "RPM:####" over USB serial
  → FanBreathInput reads on background thread
  → Signal normalized, smoothed, mapped to 0–1 intensity
  → BreathInputManager exposes to all game systems
```

The game also supports microphone and simulated input through the same interface, so it functions completely without hardware connected.

---

## Configuration

All gameplay tuning lives in ScriptableObject assets — editable in the Inspector without code changes. Multiple config profiles can coexist and be swapped by reassigning a reference. Assets: `BreathConfig`, `CourseConfig`, `AIConfig`, `EnvironmentalZoneConfig`, `MinigameDefinition` (one per game), `CourseLayout`.

---

## Design Principles

These are enforced in code:

- **The player always finishes.** Soft time cap moves the finish line if needed.
- **Every zone is escapable.** Time alone is sufficient; breath helps escape faster.
- **AI never permanently outpaces the player.** Rubber-banding and finish slowdown guarantee a positive outcome.
- **The game works without custom hardware.** Microphone and simulated input are always available.
- **Stats are captured during gameplay.** No separate measurement mode — data capture is a byproduct of playing.
- **Every run is different.** Course layout, obstacles, and zones are randomized.
- **The scene works without prefabs.** Everything is generated at runtime from code.

---

## Related Documents

- [Project overview and milestones](PROJECT_PLAN.md)
- [Game pitch and design framing](PITCH.md)
- [Core mechanic and architecture](CORE_MECHANIC_PLAN.md)
- [Hardware requirements overview](HARDWARE.md)
