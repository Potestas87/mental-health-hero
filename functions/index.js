const { onCall, HttpsError } = require('firebase-functions/v2/https');
const admin = require('firebase-admin');

admin.initializeApp();

const db = admin.firestore();

const REGION = 'us-central1';
const LEVEL_CAP = 20;
const MAX_RUNS_PER_DAY = 2;
const TOTAL_FLOORS = 3;

function getXpRequired(level) {
  const safeLevel = Math.max(1, level);
  return 40 + (safeLevel - 1) * 18;
}

function applyXp(currentLevel, currentXp, currentSkillPoints, xpToAdd) {
  let level = Math.max(1, Math.min(LEVEL_CAP, Number(currentLevel) || 1));
  let xp = Math.max(0, Number(currentXp) || 0);
  let skillPoints = Math.max(0, Number(currentSkillPoints) || 0);

  if (xpToAdd > 0 && level < LEVEL_CAP) {
    xp += xpToAdd;

    while (level < LEVEL_CAP) {
      const required = getXpRequired(level);
      if (xp < required) {
        break;
      }
      xp -= required;
      level += 1;
      skillPoints += 1;
    }
  }

  if (level >= LEVEL_CAP) {
    level = LEVEL_CAP;
    xp = 0;
  }

  return { level, xp, skillPoints };
}

function getServerDayKey() {
  const now = new Date();
  const year = now.getUTCFullYear();
  const month = String(now.getUTCMonth() + 1).padStart(2, '0');
  const day = String(now.getUTCDate()).padStart(2, '0');
  return `${year}_${month}_${day}`;
}

function requireAuth(request) {
  const uid = request.auth?.uid;
  if (!uid) {
    throw new HttpsError('unauthenticated', 'Authentication required.');
  }
  return uid;
}

exports.startRun = onCall({ region: REGION }, async (request) => {
  const uid = requireAuth(request);

  const userRef = db.collection('users').doc(uid);
  const dayKey = getServerDayKey();
  const dailyRef = userRef.collection('daily_state').doc(dayKey);
  const activeRunRef = userRef.collection('active_runs').doc('current');

  const result = await db.runTransaction(async (tx) => {
    const [userSnap, dailySnap, activeRunSnap] = await Promise.all([
      tx.get(userRef),
      tx.get(dailyRef),
      tx.get(activeRunRef),
    ]);

    if (!userSnap.exists) {
      throw new HttpsError('failed-precondition', 'User profile missing.');
    }

    const userData = userSnap.data() || {};
    const currentActiveRunId = userData.activeRunId || null;
    if (currentActiveRunId) {
      throw new HttpsError('failed-precondition', 'Run already active.');
    }

    const runsUsed = Number((dailySnap.data() || {}).runsUsed || 0);
    if (runsUsed >= MAX_RUNS_PER_DAY) {
      throw new HttpsError('resource-exhausted', 'No lives remaining today.');
    }

    const runId = db.collection('_').doc().id;
    const startedAt = admin.firestore.FieldValue.serverTimestamp();

    tx.set(dailyRef, {
      runsUsed: runsUsed + 1,
      serverDayKey: dayKey,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    tx.set(activeRunRef, {
      runId,
      status: 'active',
      startedAt,
      dayKey,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    tx.set(userRef, {
      activeRunId: runId,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    return { runId, dayKey };
  });

  return {
    ok: true,
    runId: result.runId,
    dayKey: result.dayKey,
  };
});

exports.endRun = onCall({ region: REGION }, async (request) => {
  const uid = requireAuth(request);

  const input = request.data || {};
  const runId = String(input.runId || '').trim();
  const result = String(input.result || 'quit').trim().toLowerCase();
  const floorsCleared = Math.max(0, Math.min(TOTAL_FLOORS, Number(input.floorsCleared) || 0));
  const bossDefeated = Boolean(input.bossDefeated);

  if (!runId) {
    throw new HttpsError('invalid-argument', 'runId is required.');
  }

  if (!['win', 'loss', 'quit'].includes(result)) {
    throw new HttpsError('invalid-argument', 'result must be win, loss, or quit.');
  }

  const userRef = db.collection('users').doc(uid);
  const activeRunRef = userRef.collection('active_runs').doc('current');

  const txResult = await db.runTransaction(async (tx) => {
    const [userSnap, activeRunSnap] = await Promise.all([
      tx.get(userRef),
      tx.get(activeRunRef),
    ]);

    if (!userSnap.exists) {
      throw new HttpsError('failed-precondition', 'User profile missing.');
    }

    const userData = userSnap.data() || {};
    const activeRunId = userData.activeRunId || null;

    if (!activeRunId || activeRunId !== runId || !activeRunSnap.exists) {
      throw new HttpsError('failed-precondition', 'No matching active run.');
    }

    const activeRunData = activeRunSnap.data() || {};
    const dayKey = activeRunData.dayKey || getServerDayKey();
    const dailyRef = userRef.collection('daily_state').doc(dayKey);
    const runLogRef = userRef.collection('run_logs').doc(runId);

    const [dailySnap, runLogSnap] = await Promise.all([
      tx.get(dailyRef),
      tx.get(runLogRef),
    ]);

    // Idempotency: if this run was already ended, return the existing rewards.
    if (runLogSnap.exists) {
      const existing = runLogSnap.data() || {};
      return {
        alreadyEnded: true,
        xpAwarded: Number(existing.xpAwarded || 0),
        shardsAwarded: Number(existing.shardsAwarded || 0),
        level: Number(existing.levelAfter || userData.level || 1),
        xp: Number(existing.xpAfter || userData.xp || 0),
        skillPoints: Number(existing.skillPointsAfter || userData.skillPoints || 0),
      };
    }

    const dailyData = dailySnap.data() || {};
    const bossClearCount = Number(dailyData.bossClearCount || 0);

    const floorXp = floorsCleared * 8;
    const bossXp = bossDefeated ? 20 : 0;
    const xpAwarded = floorXp + bossXp;

    const baseShards = bossDefeated ? 5 : 0;
    const firstBossBonus = bossDefeated && bossClearCount === 0 ? 3 : 0;
    const shardsAwarded = baseShards + firstBossBonus;

    const currentLevel = Number(userData.level || 1);
    const currentXp = Number(userData.xp || 0);
    const currentSkillPoints = Number(userData.skillPoints || 0);
    const currentShards = Number(userData.shards || 0);

    const xpResult = applyXp(currentLevel, currentXp, currentSkillPoints, xpAwarded);

    tx.set(userRef, {
      level: xpResult.level,
      xp: xpResult.xp,
      skillPoints: xpResult.skillPoints,
      shards: currentShards + shardsAwarded,
      activeRunId: admin.firestore.FieldValue.delete(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    const dailyUpdates = {
      serverDayKey: dayKey,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    };

    if (bossDefeated) {
      dailyUpdates.bossClearCount = bossClearCount + 1;
    }

    tx.set(dailyRef, dailyUpdates, { merge: true });

    tx.set(runLogRef, {
      runId,
      result,
      floorsCleared,
      bossDefeated,
      xpAwarded,
      shardsAwarded,
      startedAt: activeRunData.startedAt || admin.firestore.FieldValue.serverTimestamp(),
      endedAt: admin.firestore.FieldValue.serverTimestamp(),
      levelAfter: xpResult.level,
      xpAfter: xpResult.xp,
      skillPointsAfter: xpResult.skillPoints,
      dayKey,
      firstBossBonusGranted: firstBossBonus > 0,
    }, { merge: true });

    tx.set(activeRunRef, {
      status: 'ended',
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    return {
      alreadyEnded: false,
      xpAwarded,
      shardsAwarded,
      level: xpResult.level,
      xp: xpResult.xp,
      skillPoints: xpResult.skillPoints,
    };
  });

  return {
    ok: true,
    ...txResult,
  };
});
