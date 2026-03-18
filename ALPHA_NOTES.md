# Mental Health Hero - V0.1 Alpha Notes

## Build Purpose
This alpha validates the core gameplay loop end-to-end and captures early gameplay/usability feedback before beta hardening.

## What This Alpha Can Do
1. Onboarding and class select (Warrior, Ranger, Mage).
2. Home dashboard flow to Tasks, Dungeon, Upgrades, Cosmetics, and Pomodoro.
3. Task completion grants XP with daily task constraints.
4. Dungeon run with player movement/combat, enemy waves, and boss spawn.
5. Enemy archetypes with directional idle/move animation support.
6. Boss directional idle/move animation support.
7. Class combat with directional animation and class FX trigger flow.
8. Progression updates (XP/level/skill points) and upgrade purchasing.
9. Cosmetic tint system (tint category active for MVP):
   - Includes default no-tint option and starter crimson tint.
10. Pomodoro flow with XP reward logic.
11. Firebase-backed data persistence for user/progression systems used in alpha.

## Alpha Test Goal (Primary)
Complete one full intended run loop:
1. Onboard and choose class.
2. Complete tasks and gain XP.
3. Start dungeon run, clear waves, defeat boss.
4. Receive rewards.
5. Spend/equip tint cosmetic.
6. Start another run and confirm equipped cosmetic persists.

## Current Scope Boundaries
1. iOS device build/distribution is not part of this alpha gate yet.
2. Cosmetics beyond tint are scaffolded but not active.
3. Enemy attack/death animation polish is limited (focus is functional alpha loop).
4. Final balancing/tuning is not locked.

## Known Limitations / Expected Rough Edges
1. Visual polish and VFX consistency are still in-progress.
2. Some scene/UI wiring may still require manual validation after content import changes.
3. Back-end authoritative function flow may be partially deferred depending on Firebase plan setup.

## Tester Guidance
1. Prioritize reporting blockers, soft-locks, data loss, or progression inconsistencies.
2. Note exact reproduction steps for any bug.
3. Include scene and action context (for example: TaskScene complete -> HomeScene -> DungeonScene start).

## Out of Scope for V0.1 Alpha
1. Full iOS release build pipeline validation.
2. Final art pass for all sprites/animations.
3. Expanded cosmetic categories (armor sets, attack FX variants, pets, music).
4. Content/balance finalization for long-term retention.

## Version
- Tag/commit target: `V0.1 Alpha`
- Date: 2026-03-18
