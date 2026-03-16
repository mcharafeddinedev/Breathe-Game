Last Updated: Mar 15, 2026 (v7 -- prototype codebase complete)

# Breathe -- Breath-Controlled Sailboat Race

A Unity-based alternative-control game where the player's breath physically drives in-game wind power. Blow toward a custom-built fan device to fill the sail and push a small sailboat through treacherous waters. The boat auto-navigates the course — breath is the only input, acting as the accelerator. No steering, no buttons, no secondary controls. If built correctly, this concept could be applied as a medical measurement or approximation device for pediatric settings.

**The physical metaphor: breathing ==> fan spin ==> wind speed/sail power.**

## Why This Exists

This project explores whether breath can serve as a compelling and responsive primary game input. The hardware is developer-built (a repurposed PC fan + microcontroller sensing rig), the game is designed specifically around that input, and the interaction cannot be replicated with a standard controller.

The breath-only design keeps the player focused entirely on breathing, makes the game accessible to anyone who can blow, and captures meaningful breathing data for potential healthcare-adjacent applications -- breath engagement and monitoring tools for clinical and pediatric settings. The current scope is a polished, playable Windows PC game that proves the concept works.

## Project Summary

- **Engine:** Unity (C#, 2D URP template)
- **Platform:** Windows PC
- **Input:** Breath only -- custom breath-reactive hardware (fan RPM via Arduino serial), microphone fallback, simulated analog for development
- **Art direction:** Stylized, Wind Waker-inspired aesthetic
- **Game format:** Sailboat race alongside AI companions (no-fail -- player always finishes), with expansion into a collection of breath-only, no-fail minigames (STARGAZE, BALLOON, SKYDIVE, STONE SKIP, and more)
- **Current phase:** Prototype production — a playable vertical slice is being built (breath → wind → sailboat race → celebration)

## Documentation

- [How it works — technical systems overview](docs/HOW_IT_WORKS.md)
- [Project overview and milestones](docs/_project-overview/PROJECT_MASTER.md)
- [Game pitch and design framing](docs/_pitch/_Breathe_Pitch.md)
- [Core mechanic and architecture](docs/mechanic-and-architecture/CORE_MECHANIC_PLAN.md)
- [Hardware requirements overview](docs/hardware/HARDWARE_OVERVIEW.md)
- [Docs folder guide](docs/README.md)

## What Makes This Different

- **Breath is the only input:** No buttons, no controller, no secondary controls. The player's physical breathing effort is the input. Blow harder, sail faster.
- **No one ever loses:** No "Game Over," no failure states. Every session ends with a celebration of the player's personal effort and progress. Games adapt to the player's actual breathing ability. This matters for kids, clinical patients, and anyone who should feel good about playing.
- **Visible input:** Spectators can see the fan spinning -- the input is immediately obvious and legible from across a room.
- **Hygienic design:** No mouth contact with shared hardware. The player blows toward the fan from a short distance. Designed for multi-user demos and presentations.
- **Swappable architecture:** Game logic reads from a single breath-input interface. The actual signal source (fan, microphone, simulated) can be changed without touching gameplay code.
- **Healthcare potential:** The breath-only, no-fail design captures pure breathing data -- effort intensity, duration, patterns, and trends -- while encouraging repeat play. A player who enjoys the experience will keep coming back, enabling longitudinal tracking across sessions.
- **Minigame collection:** Designed to expand into a WarioWare-style collection of distinct breath-only minigames, each targeting a different breathing pattern (sustained, burst, ramp, modulated, rhythmic). Variety sustains engagement across sessions.

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository [`LICENSE`](LICENSE) (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).
