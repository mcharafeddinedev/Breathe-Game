Last Updated: Apr 07, 2026

# BREATHE -- Breath-Controlled Minigame Collection

A Unity minigame collection where breath is the only input. Blow toward a custom-built fan device to fill a sail, inflate a balloon, clear clouds from a night sky, skip stones across a lake, and more. No buttons, no controller, no secondary controls. No one ever loses.

**breathing ==> fan spin ==> game power**

## Why This Exists

This project explores whether breath can serve as a compelling primary game input — not as a gimmick, but as the entire interaction. The hardware is developer-built (a repurposed PC fan + microcontroller sensing rig), the games are designed specifically around that input, and the interaction can't be replicated with a standard controller.

## Project Summary

- **Engine:** Unity 6 (C#, 2D URP)
- **Platform:** Windows PC
- **Input:** Breath only -- custom hardware (fan RPM via Arduino serial), microphone fallback, simulated for development
- **Art direction:** Stylized, Wind Waker-inspired aesthetic
- **Game format:** A collection of breath-only, no-fail minigames -- SAILBOAT, STARGAZE, BALLOON, BUBBLES, SKYDIVE, STONE SKIP -- each targeting a different breathing pattern
- **Current phase:** Production -- Sailboat, Stargaze, Balloon, Bubbles, and Skydive are working prototypes with procedural art. Stone Skip needs concept validation and visual work.

## What Makes This Different

- **Breath is the only input.** The player's physical breathing effort drives the game. Blow harder, sail faster.
- **No one ever loses.** Every session ends with a celebration of personal effort and progress.
- **Visible input.** Spectators see the fan spinning -- the interaction is legible from across a room.
- **Hygienic.** No mouth contact with shared hardware. Safe for multi-user demos.
- **Swappable input sources.** Fan hardware, microphone, and simulated input all feed the same interface. Gameplay code is source-agnostic.

## Documentation

- [How it works — technical overview](docs/HOW_IT_WORKS.md)
- [Project plan and milestones](docs/PROJECT_PLAN.md)
- [Pitch and design framing](docs/PITCH.md)
- [Core mechanic and architecture](docs/CORE_MECHANIC_PLAN.md)
- [Hardware overview](docs/HARDWARE.md)
- [Production log](docs/PRODUCTION_LOG.md)
- [Prototype log](docs/PROTOTYPE_LOG.md)

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository [`LICENSE`](LICENSE) (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).
