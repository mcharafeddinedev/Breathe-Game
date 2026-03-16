Last Updated: Mar 15, 2026 (v5 -- prototype codebase complete)

# Public Documentation

This folder contains the public-facing documentation for the Breathe project — a breath-controlled sailboat race built in Unity with custom alternative-control hardware. All gameplay uses breath as the sole input. No one ever loses.

**Current state:** The project is in **prototype development**. A playable vertical slice is being built: breath input driving a sailboat race with AI companions and a positive, no-fail finish. No implementation details or internal timelines are disclosed here.

These documents describe the project's concept, architecture intent, milestones, and hardware requirements at a level appropriate for portfolio review, academic evaluation, and general interest.

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Structure

- `HOW_IT_WORKS.md` -- technical systems overview: how the game runs, what gets generated at runtime, how breath input flows through the software, AI behavior, environmental zones, scoring and breath analytics, hardware signal path, and design principles enforced by the code.
- `_project-overview/`
  - `PROJECT_MASTER.md` -- project scope, milestone roadmap, scope guardrails, and no-fail design rationale.
- `_pitch/`
  - `_Breathe_Pitch.md` -- public pitch narrative, design goals, breath-only rationale, no-fail design, minigame collection vision, and product direction.
- `mechanic-and-architecture/`
  - `CORE_MECHANIC_PLAN.md` -- breath-to-gameplay architecture, input abstraction, and quality targets.
- `hardware/`
  - `HARDWARE_OVERVIEW.md` -- vendor-agnostic hardware requirements, hygiene design, data capture capabilities, and integration targets.

## What's Not Here

Implementation details, build instructions, procurement specifics, code snippets, competitive landscape research, clinical evidence analysis, minigame design details, and operational procedures are maintained privately. These public documents focus on what the project is and why it's built this way.
