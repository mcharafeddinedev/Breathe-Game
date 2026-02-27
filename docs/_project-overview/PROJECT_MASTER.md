Last Updated: Feb 26, 2026 (v4 -- prototype status)

# Project Master Plan

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Project Snapshot

- **Project:** Breath-controlled sailboat race with expansion potential for additional breath-only minigames
- **Engine:** Unity (C#)
- **Template:** 2D URP (prototype baseline, with planned 2.5D art pass)
- **Platform:** Windows PC
- **Primary input:** Custom breath-sensing hardware translating breath intensity into in-game wind power
- **Design constraints:** Breath-only input (no secondary controls) and no-fail design (no loss conditions, always positive outcomes)
- **Current phase:** Prototype production — the playable vertical slice is being built (breath → wind → sailboat race → celebration). Core architecture and design are locked; implementation is in progress.

## Technical Approach

The game is built around a source-agnostic breath-input architecture. Gameplay logic reads from a single interface regardless of whether the signal comes from the custom fan device, a microphone fallback, or a simulated analog path used during development. This keeps the codebase stable and the input source swappable.

The breath-only design means every signal the system receives is breathing data, enabling both responsive gameplay and meaningful breath-effort data capture for potential healthcare applications.

The no-fail design ensures no player ever experiences a loss condition or negative outcome. Games adapt to the player's breathing ability, scores celebrate personal progress rather than absolute performance, and every session ends positively. This encourages repeat play -- critical for longitudinal tracking in clinical contexts and for keeping younger audiences engaged.

## Milestone Roadmap

**1 - Preproduction** -- Concept validation and planning
- Key outcome: Approved pitch, documented architecture and risk framework

**2 - Prototype** -- Core loop implementation
- Key outcome: Playable vertical slice with breath input driving sailboat race

**3 - Production** -- Content and polish
- Key outcome: Art style applied, audio, course variety, complete game loop

**4 - Testing** -- External playtesting
- Key outcome: Documented test sessions with multiple external testers, bug fixes

**5 - Finalization** -- Final build and review
- Key outcome: Polished build, post-project documentation

## Scope Guardrails

- One strong sailboat race loop with functional breath-only control and no-fail design is the required deliverable
- Breath-input responsiveness and reliability take priority over visual polish
- Additional game modes are stretch content only -- no expansion until the core loop is validated
- If schedule pressure occurs: prioritize mechanics and stability over aesthetics

## Document Map

- **Pitch Document:** [_Breathe_Pitch.md](../_pitch/_Breathe_Pitch.md)
- **Core Mechanic Plan:** [CORE_MECHANIC_PLAN.md](../mechanic-and-architecture/CORE_MECHANIC_PLAN.md)
- **Hardware Overview:** [HARDWARE_OVERVIEW.md](../hardware/HARDWARE_OVERVIEW.md)
