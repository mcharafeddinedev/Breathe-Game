BREATHE — Prototype Development Summary
From Concept to Complete Prototype


1. WHAT BREATHE IS

Breathe is a breath-controlled sailboat race I built in Unity. The player blows toward a physical fan device, and that breath becomes in-game wind pushing a sailboat through a race course. Two AI boats race alongside. Nobody loses.

    breath > fan spin > wind power > sail speed

No controller, no buttons, no steering. Breath is the only input. The boat steers itself — the player just controls speed. Every session ends with a celebration and a record of the player's breathing data (effort, duration, patterns) — a natural byproduct of breath being the sole input.


2. PLANNING

I started with documentation before writing any code. I put together a game pitch, a project master plan with milestones, a core mechanic plan for the breath-to-power pipeline, and a hardware overview for the custom input device. I also locked in scope guardrails early: one solid sailboat race is the deliverable, breath responsiveness matters more than visuals, and extra game modes are stretch goals.


3. FOUNDATION AND INPUT SYSTEM

The first code I wrote was the project scaffolding — a Unity 2D URP project with ScriptableObject config assets for all gameplay tuning (breath settings, course parameters, AI behavior, zone properties, course shapes, minigame metadata). I set these up as data assets so I could tweak how everything feels from the Inspector without recompiling. I also built shared signal processing functions (normalization, smoothing, dead zone, clamping) and split the codebase into five assemblies with one-way dependencies so systems couldn't accidentally couple to each other.

Right after that I built the breath input system. I made a common interface (IBreathInput) that outputs a 0-to-1 intensity value, then wrote three implementations behind it:

  - Fan hardware — reads serial data from an Arduino wired to a DC motor with a propeller
  - Microphone — isolates low-frequency breath sound from a laptop mic, with calibration
  - Simulated — keyboard/gamepad ramps that mimic breathing, for development without hardware

All three go through the same processing pipeline and produce the same output. The rest of the game reads from a manager and doesn't know which source is active. This let me build and test the whole game on keyboard input while the hardware was still coming together.


4. SAILBOAT AND VISUAL FEEDBACK

Next I built the wind system and sailboat controller. The wind system takes breath intensity and applies a response curve that boosts light breath (so players who can't blow hard still see a real response). The sailboat turns wind power into speed — it always moves forward at a base speed even with zero input, so nobody gets stuck. Breath adds speed on top.

I built the sail to visually inflate and tilt based on wind power, since it's the main feedback the player has. I also added boat effects — wind streaks, splashes, wake trails — all scaling with speed. The boat auto-follows waypoints along the course, no steering needed.

I reorganized the script folders into their final layout at this point too.


5. HARDWARE

I built the physical device alongside the game. A small DC hobby motor with a propeller acts as a generator — blowing spins it, and it produces voltage proportional to speed. An Arduino reads that voltage and sends it to the game over USB serial at 20 Hz.

The firmware does a baseline calibration on startup (20 readings to find the zero point), then applies asymmetric smoothing — fast attack so harder blowing registers immediately, slow decay so the signal holds while the blades naturally coast down. I iterated on this a few times, and later retuned the parameters after moving from breadboard wiring to soldered connections, since the electrical characteristics changed.


6. FULL PROTOTYPE

This is where everything came together into a complete, playable race. I built all the remaining systems and wired them up:

I wrote a state machine to manage the game flow — menu, level select, calibration, countdown, race, post-race, celebration. A course manager handles generating the course, running the race, and detecting the finish.

The Unity scene is almost empty on purpose. I generate nearly everything at runtime: a sine-wave ocean with parallax scrolling, buoy-marked lanes with randomly-selected course shapes, waypoints, obstacles (rocks in AI lanes, player lane stays clear), six types of environmental zones, and decorative island scenery. I went procedural because it kept the scene simple, made every race different, and meant I didn't need finished art assets to prototype.

The six zone types (headwind, tailwind, crosswind, cyclone, doldrums, choppy water) each push the player toward a different breathing pattern. Every zone has a max escape timer — nobody gets permanently stuck.

I built two AI opponents with Perlin noise speed variation, fake breathing cycles on their sails, rubber-banding to keep races close, finish-line slowdown so the player wins, and obstacle stun effects with a wobble animation. In a rare edge case where the player barely breathes at all, one AI can win — but the player still finishes second at worst.

Scoring and breath analytics run in the background during play. The game tracks race stats (breathing time, peak intensity, longest blow, completion time, zones conquered, personal bests) and does breath pattern analysis (sustained segments, bursts, intensity averages, activity ratios, pattern classification, zone response rates). All of this is captured just by playing — no separate measurement step.

I set up a session logger that writes each finished round as an NDJSON record to a daily log file, and a safety monitor that watches for prolonged high-effort breathing and suggests rest breaks.

For UI, I built a main menu with input source selection, level select, mic calibration screen, tutorial popup, countdown, a HUD with wind meter and progress bar, race results, a celebration screen with personal stats, a post-race sail-away animation, and finish-line confetti.

If the race runs past about 60 seconds and the player hasn't finished, the finish line moves to them. They cross it no matter what — that's the last-resort no-fail safety net.

The sailboat race implements a minigame interface so future games can plug in without changing existing code. I also built a debug overlay for development that shows live telemetry (input data, boat speeds, AI state, zone info, frame rate).


7. FINAL HARDWARE PASS

The last thing I did was add input source selection to the main menu so players can pick fan, mic, or simulated at runtime, improve the fan signal processing for the device's real-world behavior, and retune for soldered connections.


8. KEY DECISIONS

I put all breath input behind a single interface, which meant I could develop the game without the hardware and swap in the real device later without changing gameplay code.

All tuning lives in ScriptableObject assets, not in scripts, so I can iterate on feel without recompiling and keep different config profiles for different audiences.

I generate everything procedurally at runtime instead of placing things in the scene, which cut the art pipeline out of the critical path and kept the scene small.

The no-fail design isn't just a design doc goal — it's enforced in code. The soft time cap moves the finish line, every zone has an escape timer, and the AI rubber-bands and slows at the finish.


9. WHERE THIS LEAVES THINGS

The prototype is a complete, playable vertical slice. Breath input works and feels responsive, the fan device produces a reliable signal, the no-fail design holds up, and breathing data gets captured passively. The architecture supports swappable input sources, config profiles, and future game modes without touching gameplay code.

Next phases — art, audio, more minigames, and external playtesting — build on this working foundation.
