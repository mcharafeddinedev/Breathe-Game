Last Updated: Feb 23, 2026

# Pitch: Breathe -- Sailboat Survival Game

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## One-Line Pitch

Player breath directly controls wind power in a stylized sailboat survival experience, powered by a custom-built breath-sensing device.

## Vision

This project explores alternative-control gameplay by mapping embodied breath input to a core game mechanic. The intent is to create a responsive, memorable interaction that cannot be replicated by standard button-only input, paired with a custom analog sensing device designed specifically for this purpose.

The physical metaphor is clear and immediate: **breath = wind = fan spin = sail power.**

## Why Alternative Control

Standard controllers abstract player intent behind buttons and sticks. This project removes that abstraction for the primary mechanic -- the player's physical breathing effort is the input. The gesture (blowing) directly maps to the game action (wind filling a sail). Spectators can see the fan spinning, making the input visible and legible from across a room. The device is fully hygienic (no mouth contact), enabling back-to-back multi-user play with zero cleanup.

## Experience Goals

- Build an intuitive breath-to-motion interaction loop with immediate feedback
- Preserve a strong physical metaphor that connects the player's body to the game world
- Deliver approachable gameplay with clear readability for both player and spectators
- Design a foundation that can expand to additional breath-driven game modes

## Core Gameplay

- **Format:** Infinite survival scorer
- **Setting:** Treacherous waters with icebergs, debris, shipwrecks, and mines
- **Primary control:** Breath intensity drives wind power and boat speed
- **Secondary control:** Keyboard or controller steering for navigation
- **Art direction:** Stylized, Wind Waker-inspired aesthetic -- playful and visually engaging

## Input Strategy

- **Primary:** Custom breath-reactive hardware device (developer-built)
- **Fallback:** Software-based breath approximation via microphone
- **Development path:** Simulated analog input for prototyping without hardware

All input sources feed through a single abstraction layer so gameplay logic remains stable regardless of which source is active.

## Product Direction

The immediate goal is a polished, playable Windows PC game that proves the breath-control concept works and feels good. The long-term direction includes broader accessibility applications and potential healthcare-adjacent adaptation opportunities -- breath measurement and engagement tools for clinical and pediatric settings -- while maintaining entertainment quality as the primary design anchor.

## Related Documents

- Project overview and milestones: [PROJECT_MASTER.md](../_project-overview/PROJECT_MASTER.md)
- Core mechanic and architecture: [CORE_MECHANIC_PLAN.md](../mechanic-and-architecture/CORE_MECHANIC_PLAN.md)
- Hardware requirements: [HARDWARE_OVERVIEW.md](../hardware/HARDWARE_OVERVIEW.md)
