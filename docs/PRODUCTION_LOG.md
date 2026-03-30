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


9. CURRENT STATE

Six minigame definitions are configured: Sailboat (fully playable), Balloon, Stone Skip, Bubbles, Skydive, and Stargaze. The shared infrastructure is verified and game-agnostic. Next up is building out the remaining minigame scenes, followed by art and audio.
