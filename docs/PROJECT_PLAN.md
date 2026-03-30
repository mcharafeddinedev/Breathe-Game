Last Updated: Mar 29, 2026

# Project Plan

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Project Snapshot

- **Project:** Breath-controlled minigame collection — SAILBOAT, STARGAZE, BALLOON, SKYDIVE, STONE SKIP, BUBBLES, FROG LEAP
- **Engine:** Unity 6 (C#, 2D URP)
- **Platform:** Windows PC
- **Input:** Breath only — custom hardware (fan RPM via Arduino), microphone fallback, simulated for development
- **Design constraints:** Breath-only input (no secondary controls), no-fail design (no loss conditions)
- **Current phase:** Production — shared minigame infrastructure built, expanding into multi-scene collection

## Milestone Roadmap

**1 - Preproduction** -- Concept validation and planning
- Approved pitch, documented architecture and risk framework

**2 - Prototype** -- Core loop implementation
- Playable vertical slice with breath input driving sailboat race
- Status: Complete

**3 - Production** -- Content and polish *(current phase)*
- Full minigame collection playable, art, audio, complete game loop
- Status: In progress — shared infrastructure built, sailboat verified, remaining minigames in development

**4 - Testing** -- External playtesting
- Documented test sessions with multiple external testers, bug fixes

**5 - Finalization** -- Final build and post-project documentation

## Scope Guardrails

- The sailboat race must remain working as new minigames are added
- Breath-input responsiveness and reliability take priority over visual polish
- New minigames are built on the shared infrastructure — no one-off systems
- If schedule pressure occurs: prioritize mechanics and stability over aesthetics

## Document Map

- [Pitch and design framing](PITCH.md)
- [Core mechanic and architecture](CORE_MECHANIC_PLAN.md)
- [Hardware overview](HARDWARE.md)
- [Technical systems](HOW_IT_WORKS.md)
- [Production log](PRODUCTION_LOG.md)
- [Prototype log](PROTOTYPE_LOG.md)
