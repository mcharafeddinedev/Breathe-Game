Last Updated: Mar 29, 2026

# Hardware Overview — Breath Input Device

## IP and Usage Notice

This repository is public for review and portfolio visibility only. All materials are proprietary and protected under the repository `LICENSE` (`All Rights Reserved`). No reuse, redistribution, commercial use, or derivative works are permitted without prior written permission from the copyright holder(s).

## Purpose

This document describes the high-level hardware design for the project's custom breath-sensing input device. This will cover functional requirements and integration expectations without disclosing procurement specifics or assembly procedures.

## What the Device Does

The device captures how hard a player is blowing and converts that into a digital signal that the game reads in real time. The stronger the breath, the stronger the signal. The game maps that signal to wind power, sail behavior, and boat speed.

## Signal Path

Player breath -> physical rotation -> pulse detection -> microcontroller sampling -> USB serial telemetry -> Unity game engine input mapping.

## Component Requirements (Vendor-Agnostic)

**Air-reactive rotor** -- Spins reliably and proportionally from directed breath at short range

**Rotation sensing** -- Produces repeatable, countable pulses per revolution at low RPM

**Microcontroller** -- Reads rotation pulses and streams serial telemetry over USB

**Data link** -- Stable USB serial connection to the host PC

**Mounting** -- Holds the rotor at a consistent orientation and comfortable distance from the player

## Technical Targets

- Update rate high enough for responsive real-time gameplay (target: 20 Hz or better)
- End-to-end control latency below perceptible threshold for interactive play
- Stable idle baseline near zero when no breath is applied
- Predictable, proportional increase in signal strength with increasing breath effort

## Hygiene and Safety

- No direct mouth contact with any shared surface -- the player blows toward the device from a short distance
- This is a significant hygiene advantage for multi-user scenarios such as demos and presentations
- Residual airborne droplet contamination from directed breath at close range is acknowledged as an engineering consideration, with planned mitigations including a sealed electronics enclosure and cleanable forward-facing surfaces
- Wiring is insulated and secured to prevent accidental disconnection during demos or playtesting

## Data Capture

The breath-only game design (no secondary controller inputs) means every signal the device receives is breathing data. The device captures:

- Real-time breath intensity proportional to effort
- Effort duration and patterns over time
- Session-to-session trend data

These measurements drive responsive gameplay and allow the game to show the player their own effort and progress over time.

## Integration

- The game engine reads serial text telemetry from the device, then applies normalization and smoothing
- Game logic consumes the signal through a source-agnostic interface, so the hardware path, microphone fallback, and simulated input path are all interchangeable without gameplay code changes

## Implementation Status

The custom hardware is built and integrated. The game supports multiple input paths (custom device, microphone fallback, and simulated input for development) through a single abstraction so gameplay logic is unchanged regardless of source. Procurement lists, wiring specifics, assembly procedures, firmware, and calibration workflows are maintained privately.
