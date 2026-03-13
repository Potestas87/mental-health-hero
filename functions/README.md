# Cloud Functions (MVP authoritative run logic)

This backend enforces run integrity server-side:
- `startRun`: enforces daily life cap and creates one active run.
- `endRun`: validates active run, computes XP/shards server-side, applies first boss bonus, writes run log idempotently.

## Setup

1. Install Firebase CLI globally (if not already):
   - `npm i -g firebase-tools`
2. Login:
   - `firebase login`
3. Select project:
   - `firebase use <your-project-id>`
4. Install function deps:
   - `cd functions && npm install`
5. Deploy:
   - `firebase deploy --only functions`

## Callable contract

### startRun
Input: `{}`
Output: `{ ok: true, runId: string, dayKey: string }`

### endRun
Input:
- `runId: string` (required)
- `result: "win" | "loss" | "quit"`
- `floorsCleared: number` (0-3)
- `bossDefeated: boolean`

Output:
- `ok: true`
- `alreadyEnded: boolean`
- `xpAwarded: number`
- `shardsAwarded: number`
- `level: number`
- `xp: number`
- `skillPoints: number`
