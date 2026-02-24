Last Updated: Feb 23, 2026

# Hardware Overview -- Breath Input Device

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Purpose

This document describes the high-level hardware design for the project's custom breath-sensing input device. It covers functional requirements and integration expectations without disclosing procurement specifics or assembly procedures.

## What the Device Does

The device captures how hard a player is blowing and converts that into a digital signal that the game reads in real time. The stronger the breath, the stronger the signal. The game maps that signal to wind power, sail behavior, and boat speed.

## Signal Path

Player breath -> physical rotation -> pulse detection -> microcontroller sampling -> USB serial telemetry -> Unity game engine input mapping.

## Component Requirements (Vendor-Agnostic)

| Category | Functional Requirement |
|----------|----------------------|
| **Air-reactive rotor** | Spins reliably and proportionally from directed breath at short range |
| **Rotation sensing** | Produces repeatable, countable pulses per revolution at low RPM |
| **Microcontroller** | Reads rotation pulses and streams serial telemetry over USB |
| **Data link** | Stable USB serial connection to the host PC |
| **Mounting** | Holds the rotor at a consistent orientation and comfortable distance from the player |

## Technical Targets

- Update rate high enough for responsive real-time gameplay (target: 20 Hz or better)
- End-to-end control latency below perceptible threshold for interactive play
- Stable idle baseline near zero when no breath is applied
- Predictable, proportional increase in signal strength with increasing breath effort

## Hygiene and Safety

- No direct mouth contact with any shared surface -- the player blows toward the device from a short distance
- Multiple users can play back-to-back with zero cleanup between sessions
- Wiring is insulated and secured to prevent accidental disconnection during demos or playtesting

## Integration

- The game engine reads serial text telemetry from the device, then applies normalization and smoothing
- Game logic consumes the signal through a source-agnostic interface, so the hardware path, microphone fallback, and simulated input path are all interchangeable without gameplay code changes

## Implementation Details

Detailed procurement lists, wiring specifics, assembly procedures, firmware, and calibration workflows are maintained privately outside this public documentation set. Contact the repository owner for further information.
