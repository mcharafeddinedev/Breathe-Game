BREATHE — Production Development Summary
From Prototype to Multi-Minigame Collection


1. WHERE PRODUCTION PICKS UP

The prototype was a complete sailboat race — one scene, breath input working end-to-end with custom hardware. Everything was built specifically for that one race. Production started with the goal of generalizing what worked into shared infrastructure that supports multiple breath-controlled minigames without breaking the existing sailboat scene.


2. SHARED INFRASTRUCTURE

I created an IMinigame interface and MinigameBase abstract class so every minigame plugs into the same lifecycle — registration, breath analytics, session logging, stat reporting, and result display all handled generically. MinigameDefinition ScriptableObjects hold per-game config (name, description, breath pattern, card color, countdown text). The sailboat scene was refactored to use this infrastructure, and it still functions as before.


3. RENAMING AND CLEANUP

Sailboat-specific naming was generalized. WindSystem became BreathPowerSystem (the universal 0-to-1 power variable). The result overlay was consolidated into a single data-driven screen. Duplicate and obsolete UI code was removed.


4. MAIN MENU AND LEVEL SELECT

There is now a new main menu scene with programmatic Canvas UI. The level select generates a game card for each minigame in the roster with hover animations and breath pattern text that fades in on mouseover. Menu navigation uses mouse input; gameplay scenes use breath input only.


5. BREATH ANALYTICS AND INPUT TUNING

Playtesting revealed the fan propeller coasts for 1–3 seconds after the player stops blowing, inflating duration measurements. I added spin-down compensation to BreathAnalytics — estimated coast time is subtracted from each segment before classification. Raw and adjusted values are both logged.

Input sensitivity was also retuned. MaxExpectedRPM was set too low, causing different effort levels to saturate at 1.0. Raising it to match the fan's actual output restored meaningful dynamic range.


6. UI POLISH

Issues caught during playtesting: countdown numbers fading to black instead of dissolving cleanly (fixed), results overlay appearing after a legacy delay instead of immediately (fixed), zone popup text clipping behind a background panel (removed the panel), buoy colors updated to alternating warm tones, and per-minigame post-countdown buffer added.


7. DATA-DRIVEN RESULTS

The result overlay reads all display data from the active IMinigame — stat tiers control layout priority, card color from the definition themes the panel. The overlay has no knowledge of any specific game. Scoring keys are scoped per minigame ID so personal bests don't collide.


8. DOCUMENTATION

All docs updated to reflect the multi-minigame architecture and current project state. Folder structure flattened. Language reviewed for accuracy and tone.


9. MINIGAME DEVELOPMENT

With infrastructure in place, I started building the actual minigames. Each one targets a different breathing pattern while sharing the same input pipeline, state machine, result overlay, and analytics.

Stargaze is the most complete after Sailboat. The player blows to push clouds off the night sky, revealing constellations underneath. It runs three rounds with increasing difficulty — clouds drift back when breath stops, and each round needs more sustained effort. When the sky clears, the camera zooms in on the constellation with star name labels and a short educational caption about its mythology and brightest stars. A continue button (triggered by mouse, keyboard, or breath) advances to the next round.

Balloon is a working prototype — the player inflates a balloon by breathing at a controlled intensity. Bubbles is a working prototype — the player blows into a bubble wand at a "sweet spot" intensity to produce bubbles.

Skydive is now a working prototype. The player guides a parachuting skydiver onto a bullseye target by blowing to fire directional thrusters on a jetpack. Wind pushes the diver in one direction per turn, and the player opposes it with breath. The scene is built entirely at runtime — a procedural stick-figure diver in a red jumpsuit, a parachute canopy with strings, a pixel-art forest of ~260 trees, a distant city skyline, scrolling clouds, and a multi-layer parallax system that gives a sense of continuous forward motion between turns. A two-stage boost transition scrolls the old target off-screen and brings the new one in without breaking the illusion of movement. The background keeps scrolling infinitely during the results screen.

Stone Skip has its gameplay script written and scene set up but hasn't been playtested yet.


10. BREATH AS A UI TRIGGER

Tutorial popups accept breath input as a continue trigger alongside mouse and keyboard, so the player can advance through the how-to-play flow hands-free. Menu navigation itself uses standard mouse input — breath-driven menu scrolling was prototyped and removed in favor of simplicity.


11. SPIN-DOWN DETECTION

The fan propeller coasts for 1-3 seconds after the player stops blowing. This was already handled in analytics, but it was affecting gameplay too — the sailboat kept sailing after the player stopped. I added spin-down detection to BreathPowerSystem: if power drops by 12% or more within one second, it snaps to zero. It tracks the lowest raw intensity during the coast and resumes the moment it detects a genuine new breath (raw intensity rising above the trough). This is transparent to all consumers of BreathPower.


12. CURRENT STATE

Sailboat, Stargaze, Balloon, Bubbles, and Skydive are all working prototypes with procedural art. Stone Skip is the last minigame that needs concept validation and visual buildout. Still no audio or final art — those are next after gameplay is locked.
