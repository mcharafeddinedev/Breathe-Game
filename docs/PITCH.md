Last Updated: Mar 31, 2026

# Pitch: Breathe — Breath-Controlled Minigame Collection

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## One-Line Pitch

A collection of minigames where the player's breath is the only input — blow into a custom-built fan device to race sailboats, skip stones, inflate balloons, and more. No one ever loses.

## Vision

Breath maps directly to a core game mechanic — not as a novelty, but as the entire interaction. A responsive, physical connection between the player and the game that standard controllers can't replicate.

**breath = fan spin = game power.**

Every game uses breath as the sole input. No buttons, no sticks, no secondary controls. The player's focus is entirely on breathing, and the game world reacts visibly and immediately to their effort.

## Why Breath-Only

- **Focus:** No secondary inputs to split attention. Breathing IS the game.
- **Accessibility:** Playable by anyone who can blow — no controller familiarity required.
- **Clear physical metaphor:** Blow harder, sail faster. The connection is immediate.
- **Spectator clarity:** Onlookers see the fan spinning and the game responding. Legible from across a room.
- **Hygienic:** No mouth contact with shared hardware. Multiple people can take turns.

## Why Alternative Control

Standard controllers abstract player intent behind buttons and sticks. This project removes that abstraction — the player's physical effort is the input, and it's visible. The gesture (blowing) maps directly to the game action (wind filling a sail, stone flying farther, balloon getting bigger).

## Experience Goals

- Intuitive breath-to-motion loop with immediate feedback
- Strong physical metaphor connecting the player's body to the game world
- Approachable gameplay readable by both player and spectators
- Every session ends positively — no failure states, no "Game Over"
- Repeat play encouraged through celebration of personal progress
- Rich visual and audio feedback to make breath-only gameplay feel dynamic

## Core Gameplay (Sailboat)

A ~60-second sailboat race alongside two AI companions through procedurally generated waters with environmental hazards. Breath intensity adds speed on top of a constant base movement — the boat is always moving, even between breaths. No steering. Environmental conditions (headwinds, tailwinds, waves, calm water) drive natural breath variation. AI adapts to the player's pace. The player always finishes first.

Every race ends with a celebration screen showing personal stats and comparisons to previous bests. Art direction is stylized and Wind Waker-inspired.

## Minigame Collection

Six breath-only minigames share the same input system and no-fail philosophy. Each targets a different breathing pattern — sustained effort, burst control, gradual ramps, modulated intensity, rhythmic pulsing. The sailboat race is the anchor game. Stargaze has the player clearing clouds from a night sky to discover constellations, with educational captions about each one. Balloon and Bubbles explore controlled, gentle breath. The collection is designed so every game feels different but the input is always the same: just breathe.

## Input Strategy

- **Primary:** Custom breath-reactive hardware device (developer-built)
- **Fallback:** Microphone-based breath approximation
- **Development:** Simulated analog input for prototyping without hardware

All sources feed through a single abstraction layer. Gameplay logic is input-source agnostic.

## Product Direction

The immediate goal is a polished, playable Windows PC game that proves breath-control works and feels good. The no-fail design means players of all ages and abilities can enjoy the experience. Because breath is the only input, every session naturally produces data about the player's effort and patterns — useful for showing personal progress over time. The project is a game first. Whether the data it captures could also be interesting in other contexts is worth exploring, but it is not the driving design goal.

## Current Status

In production. Sailboat, Stargaze, Balloon, Bubbles, and Skydive are all working prototypes with procedural art. Stone Skip is the remaining minigame — scripted but needs concept validation and visual work. Menu navigation uses mouse input. During gameplay, tutorial popups accept breath as a continue trigger so the player can stay hands-free while learning the game.

## Related Documents

- Project overview and milestones: [PROJECT_PLAN.md](PROJECT_PLAN.md)
- Core mechanic and architecture: [CORE_MECHANIC_PLAN.md](CORE_MECHANIC_PLAN.md)
- Hardware requirements: [HARDWARE.md](HARDWARE.md)
