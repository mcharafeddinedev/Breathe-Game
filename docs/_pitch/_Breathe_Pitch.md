Last Updated: Feb 26, 2026 (v7 -- status)

# Pitch: Breathe — Breath-Controlled Sailboat Race

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## One-Line Pitch

Player breath is the only input -- it controls the wind that powers a sailboat racing alongside AI companions, using a custom-built breath-sensing device. No one ever loses.

## Vision

This project explores alternative-control gameplay by mapping embodied breath input to a core game mechanic. The intent is to create a responsive, memorable interaction that cannot be replicated by standard button-only input, paired with a custom analog sensing device designed specifically for this purpose.

The physical metaphor is clear and immediate: **breath = wind = fan spin = sail power.**

Every game in this project uses breath as the sole input. No buttons, no sticks, no secondary controls. The player's entire focus is on breathing, and the game world reacts visibly and immediately to their effort.

## Why Breath-Only

Breath is the only input for all gameplay. This is a deliberate design choice:

- **Focus:** No secondary inputs to split the player's attention. Breathing IS the game.
- **Accessibility:** Playable by anyone who can blow -- no controller familiarity or hand-eye coordination required.
- **Clear physical metaphor:** Blow harder, sail faster. The connection is immediate and obvious.
- **Spectator clarity:** Onlookers see the fan spinning and the boat responding. The interaction is legible from across a room.
- **Healthcare potential:** With breath as the sole input, the system captures pure breathing data -- effort intensity, duration, patterns, and trends -- without noise from split-attention inputs.

## Why Alternative Control

Standard controllers abstract player intent behind buttons and sticks. This project removes that abstraction entirely -- the player's physical breathing effort is the only input. The gesture (blowing) directly maps to the game action (wind filling a sail). Spectators can see the fan spinning, making the input visible and legible from across a room. The device requires no mouth contact with any shared surface, enabling multi-user play at demos and presentations.

## Experience Goals

- Build an intuitive breath-to-motion interaction loop with immediate feedback
- Preserve a strong physical metaphor that connects the player's body to the game world
- Deliver approachable gameplay with clear readability for both player and spectators
- **Every session ends positively** -- no failure states, no "Game Over," no losing. The player always finishes and always feels good about their effort
- Encourage repeat play through celebration of personal progress, not punishment for underperformance
- Design a foundation that can expand to additional breath-driven game modes
- Use rich animations and visual feedback to make breath-only gameplay feel dynamic and engaging

## Core Gameplay

- **Format:** Sailboat race alongside AI companions (~60 seconds)
- **Setting:** Treacherous waters with environmental conditions and physical obstacles
- **Input:** Breath only -- breath intensity adds speed on top of a constant base movement. The boat is always moving, even between breaths. No steering.
- **Course obstacles:** Environmental conditions (headwinds, tailwinds, waves, calm water) change how much effort the player needs to gain speed -- this drives natural breath variation. Physical obstacles (rocks, icebergs) are avoided automatically by the player's boat, but AI boats occasionally clip them and get a charming cartoon dizzy-spin stun before recovering.
- **AI companions:** Two computer-controlled boats race alongside the player, adapting to the player's pace so the race is always exciting. The player always finishes first.
- **No-fail design:** There is no "Game Over," no losing, no failure state. Every race ends with a celebration screen showing personal stats -- total breath time, peak effort, longest sustained blow, course time, and personal best comparisons. Games adapt to the player's actual breathing ability. A player who feels good about their experience will want to play again.
- **Visual "juice":** With only one input, the game relies heavily on visual, audio, and UI feedback to feel exciting -- sail animations, wake spray, speed lines, camera effects, environmental zone changes, AI obstacle stun animations, and a confetti-filled finish line.
- **Art direction:** Stylized, Wind Waker-inspired aesthetic -- playful and visually engaging
- **Visual feedback:** Sail filling and unfilling, water spray, speed lines, camera effects, AI boat positions shifting -- animations carry the engagement

## Expansion: Minigame Collection (Stretch Goal)

The project is designed to expand into a collection of breath-only minigames, all sharing the same breath-input system and the same no-fail design -- every minigame ends positively, every session celebrates what the player accomplished. Each minigame emphasizes a different breathing pattern (sustained effort, burst calibration, gradual ramps, modulated control, rhythmic pulsing) while maintaining the overall Wind Waker-inspired art direction. The sailboat race is the core game; additional games build variety around the same mechanic. Environments range from serene lakeside nature scenes to open skies, each designed to be visually rich and responsive to the player's breathing.

## Input Strategy

- **Primary:** Custom breath-reactive hardware device (developer-built)
- **Fallback:** Software-based breath approximation via microphone
- **Development path:** Simulated analog input for prototyping without hardware

All input sources feed through a single abstraction layer so gameplay logic remains stable regardless of which source is active.

## Product Direction

The immediate goal is a polished, playable Windows PC game that proves the breath-control concept works and feels good. The no-fail, encouraging design means players of all ages and ability levels can enjoy the experience -- especially younger audiences and those with respiratory conditions. The long-term direction includes potential healthcare-adjacent applications -- the breath-only design captures meaningful breathing data (effort intensity, duration, patterns, session-to-session trends) that could serve as an engaging breathing exercise and monitoring tool in clinical and pediatric settings. A player who enjoys the experience will want to play again, enabling longitudinal breath-effort tracking over multiple sessions. Entertainment quality is the primary design anchor; healthcare utility is a secondary benefit that the design naturally supports.

## Current Status

The project is in prototype development. A playable vertical slice is being built around the design described above — breath input, sailboat race, AI companions, and no-fail celebration. No internal timelines or implementation details are published in this document.

## Related Documents

- Project overview and milestones: [PROJECT_MASTER.md](../_project-overview/PROJECT_MASTER.md)
- Core mechanic and architecture: [CORE_MECHANIC_PLAN.md](../mechanic-and-architecture/CORE_MECHANIC_PLAN.md)
- Hardware requirements: [HARDWARE_OVERVIEW.md](../hardware/HARDWARE_OVERVIEW.md)
