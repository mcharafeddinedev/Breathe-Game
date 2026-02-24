Last Updated: Feb 23, 2026

# Core Mechanic: Breath to Power Level

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Mechanic Objective

Convert player breath intensity into a stable, real-time gameplay signal that controls in-game wind speed and sail power.

## Design Philosophy

The breath mechanic is the single most important system in the project. Everything else -- the sailboat, the obstacles, the scoring -- is built on top of a working, responsive breath-to-game pipeline. The mechanic must feel immediate, proportional, and satisfying before any other feature gets attention.

## System Architecture (Public Summary)

The breath pipeline is structured in layers:

- **Signal capture:** Acquires a raw breath-related input signal from the active source
- **Normalization:** Converts the raw signal into a bounded 0-to-1 intensity value
- **Stability:** Applies smoothing to reduce noise, jitter, and abrupt spikes
- **Gameplay integration:** Feeds the normalized intensity into wind/propulsion systems

Each layer is independent, making it possible to tune or replace components without cascading changes.

## Input Abstraction

Game systems consume a common breath-input interface. The actual signal source -- custom hardware, microphone, or simulated analog -- can be swapped at the configuration level without rewriting any gameplay logic. This decoupling is a core architectural decision that keeps the codebase stable across all input paths.

## Performance and Quality Targets

- **Responsiveness:** Low-latency control feel suitable for real-time interactive gameplay
- **Proportionality:** Predictable, intuitive mapping between physical effort and in-game effect
- **Stability:** Consistent behavior at rest (no drift) and under sustained repeated use
- **Discrete levels:** The continuous signal is also binned into discrete power levels for threshold-based game events and potential measurement applications

## Risk Areas

- Signal noise and per-session calibration variability
- Trust and fidelity differences between input sources (hardware vs microphone)
- Responsiveness characteristics may shift across different hardware configurations or environments

## Validation Criteria

- Confirm reliable real-time control response across light, moderate, and strong breath intensities
- Validate proportional intensity mapping (harder breath = stronger effect, consistently)
- Verify stable baseline at rest with minimal noise
- Confirm consistency across repeated sessions and multiple users

## Related Documents

- Project overview: [PROJECT_MASTER.md](../_project-overview/PROJECT_MASTER.md)
- Pitch framing: [_Breathe_Pitch.md](../_pitch/_Breathe_Pitch.md)
- Hardware summary: [HARDWARE_OVERVIEW.md](../hardware/HARDWARE_OVERVIEW.md)
