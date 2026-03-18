# Day 12.5 Notes - Ranger and Warrior Attack FX Wiring

## Goal
Wire Ranger and Warrior ability FX so Basic, Movement, and AOE attack effects trigger from the shared class FX prefab system and rotate with player orientation.

## Required Assets Per Class
- 3 animation clips:
  - `<Class>_BasicFX.anim`
  - `<Class>_MoveFX.anim`
  - `<Class>_AoeFX.anim`
- 1 animator controller:
  - `<Class>FX.controller`
- 1 prefab:
  - `<Class>_FX.prefab`

## Animator Setup
- Controller has triggers:
  - `PlayBasic`
  - `PlayMove`
  - `PlayAoe`
- State machine:
  - Default state = empty/idle FX state
  - Trigger transitions to each ability clip
  - Return transitions back to idle after clip completes
- Transition settings:
  - Keep transition durations near 0 for responsive FX timing
  - Disable unnecessary exit-time waits for trigger transitions

## Prefab Setup
- Prefab root should include:
  - `Animator` with `<Class>FX.controller` assigned
  - `SpriteRenderer` for FX sprite playback
- Keep prefab transform local rotation at 0; runtime orientation is applied by `HeroController2D`.

## HeroController2D Wiring Checklist
- In each class archetype row:
  - `classFxPrefab` -> assign corresponding class FX prefab
  - `basicFxTriggerName` = `PlayBasic`
  - `movementFxTriggerName` = `PlayMove`
  - `aoeFxTriggerName` = `PlayAoe`
- Orientation tuning:
  - `basicFxRotationOffsetZ`
  - `movementFxRotationOffsetZ`
  - `aoeFxRotationOffsetZ`
  - `snapFxToEightDirections` enabled

## Validation Checklist
- Ranger:
  - Basic/Move/AOE trigger correct clips
  - FX points correctly in all 8 directions
- Warrior:
  - Basic/Move/AOE trigger correct clips
  - FX points correctly in all 8 directions
- No class cross-over (Ranger never plays Warrior FX and vice versa)

## Known Status
- Mage FX fully wired and used as reference implementation.
- Ranger/Warrior FX wiring in progress.
