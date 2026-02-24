Last Updated: Feb 23, 2026

# Project Master Plan

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Project Snapshot

- **Project:** Breath-controlled sailboat survival game with expansion potential for additional breath-driven minigames
- **Engine:** Unity (C#)
- **Template:** 2D URP (prototype baseline, with planned 2.5D art pass)
- **Platform:** Windows PC
- **Primary input:** Custom breath-sensing hardware translating breath intensity into in-game wind power
- **Current phase:** Prototype production

## Technical Approach

The game is built around a source-agnostic breath-input architecture. Gameplay logic reads from a single interface regardless of whether the signal comes from the custom fan device, a microphone fallback, or a simulated analog path used during development. This keeps the codebase stable and the input source swappable.

## Milestone Roadmap

| Milestone | Focus | Key Outcome |
|-----------|-------|-------------|
| **1 - Preproduction** | Concept validation and planning | Approved pitch, documented architecture and risk framework |
| **2 - Prototype** | Core loop implementation | Playable vertical slice with breath input driving sailboat movement |
| **3 - Production** | Content and polish | Art style applied, audio, obstacle variety, complete game loop |
| **4 - Testing** | External playtesting | Documented test sessions with multiple external testers, bug fixes |
| **5 - Finalization** | Final build and review | Polished build, post-project documentation |

## Scope Guardrails

- One strong sailboat gameplay loop with functional breath control is the required deliverable
- Breath-input responsiveness and reliability take priority over visual polish
- Additional game modes are stretch content only -- no expansion until the core loop is validated
- If schedule pressure occurs: prioritize mechanics and stability over aesthetics

## Document Map

- **Pitch Document:** [_Breathe_Pitch.md](../_pitch/_Breathe_Pitch.md)
- **Core Mechanic Plan:** [CORE_MECHANIC_PLAN.md](../mechanic-and-architecture/CORE_MECHANIC_PLAN.md)
- **Hardware Overview:** [HARDWARE_OVERVIEW.md](../hardware/HARDWARE_OVERVIEW.md)
