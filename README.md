Last Updated: Feb 23, 2026

# Breathe -- Breath-Controlled Sailboat Survival Game

A Unity-based alternative-control game where the player's breath physically drives in-game wind power. Blow toward a custom-built fan device to fill the sail and push a small sailboat through treacherous waters. Steer with a keyboard or controller. Survive as long as possible, or on a short timer in a strict stat tracking mode (for breathing pattern/capacity measurements). If built correctly, this could be concept could be applied as a medical measurement device for pediatric settings.

**The physical metaphor: breathing ==> fan spin ==> wind speed/sail power.**

## Why This Exists

This project explores whether breath can serve as a compelling and responsive primary game input. The hardware is developer-built (a repurposed PC fan + microcontroller sensing rig), the game is designed specifically around that input, and the interaction cannot be replicated with a standard controller.

The long-term vision includes healthcare-adjacent applications -- breath measurement and engagement tools for clinical and pediatric settings -- but the current scope is a polished, playable Windows PC game that proves the concept works.

## Project Summary

- **Engine:** Unity (C#, 2D URP template)
- **Platform:** Windows PC
- **Primary input:** Custom breath-reactive hardware (fan RPM via Arduino serial)
- **Fallback inputs:** Microphone amplitude, simulated analog (for development)
- **Art direction:** Stylized, Wind Waker-inspired aesthetic
- **Game format:** Infinite survival scorer with obstacle navigation
- **Current phase:** Prototype production

## Documentation

- [Project overview and milestones](docs/_project-overview/PROJECT_MASTER.md)
- [Game pitch and design framing](docs/_pitch/_Breathe_Pitch.md)
- [Core mechanic and architecture](docs/mechanic-and-architecture/CORE_MECHANIC_PLAN.md)
- [Hardware requirements overview](docs/hardware/HARDWARE_OVERVIEW.md)
- [Docs folder guide](docs/README.md)

## What Makes This Different

- **Embodied input:** The player's physical breathing effort directly maps to gameplay intensity. The harder you blow, the faster the boat moves.
- **Visible input:** Spectators can see the fan spinning -- the input is immediately obvious and legible from across a room.
- **Hygienic design:** No mouth contact with shared hardware. The player blows toward the fan from a short distance. Multiple users can play back-to-back with zero cleanup.
- **Swappable architecture:** Game logic reads from a single breath-input interface. The actual signal source (fan, microphone, simulated) can be changed without touching gameplay code.

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository [`LICENSE`](LICENSE) (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).
