---
name: tld
description: Draw gameplay-to-music flow diagrams with the tldraw plugin. Use when designing off/on/duck flows, pause or disconnect branches, Smart Track Start behavior, or when the user asks for a tldraw scenario or `/tld`.
---

# Tld

## Purpose

Use this skill to turn gameplay/music ideas into clear tldraw diagrams that match the current Undefault backend model.

## Source of truth vs sketches

Runtime behavior is driven by `GsiHost/appsettings.json` (`RulesEngine.ActionMap`), `control-profiles.json`, and optional legacy track profiles (see [`docs/backend-architecture.md`](../../../docs/backend-architecture.md)). There is no YAML scenario orchestrator. Diagrams are provisional design sketches until an approved issue maps them to code or config.

## Default Workflow

1. Read the current backend/docs context that the scenario depends on.
2. Call `diagram_drawing_read_me` before drawing.
3. Draw the scenario on a new blank canvas unless the user asks to extend an existing board.
4. Use explicit state and transition labels:
   - gameplay trigger
   - backend event
   - profile command
   - playback result
   - missing primitive if the scenario needs behavior the backend does not have yet
5. Mark current support, missing primitives, and open decisions.

## Undefault Scenario Conventions

“Scenario” here means a storyboard of gameplay -> backend event -> profile command. It matches `ActionMap` and control profiles, not an external YAML engine.

Use these concepts consistently:

- `pause`: music off
- `resume`: music back on
- `duck`: set playback to a target volume percent
- `restore_volume`: restore the saved pre-duck volume
- `freeze time`: pre-live round window (note: timed ramps require dedicated backend support beyond control profiles)
- `live round`: post-freeze gameplay state

When a scenario is repo-specific, reflect the current backend boundaries:

- control profiles currently support `pause`, `resume`, `duck`, `restore_volume`
- `duck` already saves the current volume for later restore
- `duck` uses an absolute `VolumePercent`, so it can lower or raise playback to a chosen value
- gradual ramps, timers, and pause/disconnect-aware orchestration should be marked explicitly if they require new backend behavior beyond `pause` / `resume` / `duck` / `restore_volume`

## Drawing Rules

- Use a title text shape at the top.
- Use rectangles or pills for stable states.
- Use diamonds for decisions or guards.
- Use notes for status remarks such as `supported now`, `needs new primitive`, or `open decision`.
- Label arrows with the event or condition that causes the transition.
- Leave enough space so arrow labels stay readable.

## Recommended Scenario Layout

For gameplay/music flows, prefer this left-to-right structure:

1. gameplay signal
2. detector or scenario gate
3. music state transitions
4. notes for implementation status

## Output Expectations

After drawing, summarize:

- what the scenario does
- which parts are already supported by the backend
- which parts would require new backend primitives or approved scope

## Examples

Good triggers for this skill:

- `draw a tldraw scenario for round_start -> off -> duck`
- `use /tld for freeze time music ramp`
- `show pause/disconnect branches for gameplay music`
- `diagram Smart Track Start with current backend boundaries`
