using UnityEngine;

public enum EnemyArchetype
{
    Chaser,
    Tank,
    Skirmisher,
    Bruiser,
    RangedProxy
}

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController2D : MonoBehaviour
{
    [Header("Archetype")]
    public EnemyArchetype archetype = EnemyArchetype.Chaser;
    public bool applyArchetypeStatsOnStart = true;

    [Header("Base Stats")]
    public int maxHp = 40;
    public int contactDamage = 10;
    public float moveSpeed = 2f;
    public float attackCooldownSeconds = 1f;
    public float attackRange = 0.9f;
    public bool isBoss;

    [Header("Boss Tuning")]
    public int bossMaxHp = 260;
    public float bossPhase2SprintSpeed = 3.8f;
    public float bossPhase2SprintDuration = 1.3f;
    public float bossPhase2CycleDuration = 3.3f;
    public float bossPhaseTransitionRecoverySeconds = 1.1f;
    public float bossPhase3CooldownMultiplier = 0.65f;

    [Header("Ranged Proxy")]
    public float rangedPreferredDistance = 2.2f;
    public float rangedBurstIntervalSeconds = 4f;
    public float rangedBurstDamageMultiplier = 1.8f;

    [Header("Optional References")]
    public Transform target;
    public DungeonRunManager runManager;

    private Rigidbody2D _rb;
    private HeroController2D _hero;
    private int _currentHp;
    private float _nextAttackTime;
    private float _nextBurstTime;
    private int _bossPhase = 1;
    private float _bossRecoveryUntilTime;

    private float _runtimeMoveSpeed;
    private int _runtimeDamage;
    private float _runtimeCooldown;
    private float _runtimeAttackRange;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        ResolveTarget();

        if (applyArchetypeStatsOnStart)
        {
            ApplyArchetypeStats();
        }

        _currentHp = maxHp;
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            ResolveTarget();
            if (target == null)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }
        }

        if (isBoss)
        {
            ApplyBossPhaseTuning();
        }

        MoveByArchetype();
        TryAttackByDistance();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _currentHp = Mathf.Max(0, _currentHp - amount);
        if (_currentHp <= 0)
        {
            if (runManager != null)
            {
                runManager.OnEnemyDefeated(isBoss);
            }

            Destroy(gameObject);
        }
    }

    private void ResolveTarget()
    {
        if (target == null)
        {
            var hero = FindFirstObjectByType<HeroController2D>();
            if (hero != null)
            {
                _hero = hero;
                target = hero.transform;
            }
        }
        else
        {
            _hero = target.GetComponent<HeroController2D>();
        }
    }

    private void ApplyArchetypeStats()
    {
        if (isBoss)
        {
            maxHp = bossMaxHp;
            contactDamage = 12;
            moveSpeed = 2.0f;
            attackCooldownSeconds = 1.0f;
            attackRange = 1.1f;
        }
        else
        {
            switch (archetype)
            {
                case EnemyArchetype.Tank:
                    maxHp = 95;
                    contactDamage = 14;
                    moveSpeed = 1.2f;
                    attackCooldownSeconds = 1.45f;
                    attackRange = 1.0f;
                    break;
                case EnemyArchetype.Skirmisher:
                    maxHp = 28;
                    contactDamage = 7;
                    moveSpeed = 3.25f;
                    attackCooldownSeconds = 0.55f;
                    attackRange = 0.8f;
                    break;
                case EnemyArchetype.Bruiser:
                    maxHp = 62;
                    contactDamage = 18;
                    moveSpeed = 1.7f;
                    attackCooldownSeconds = 1.2f;
                    attackRange = 1.0f;
                    break;
                case EnemyArchetype.RangedProxy:
                    maxHp = 34;
                    contactDamage = 8;
                    moveSpeed = 2.3f;
                    attackCooldownSeconds = 1.35f;
                    attackRange = 2.5f;
                    break;
                default:
                    maxHp = 42;
                    contactDamage = 10;
                    moveSpeed = 2.1f;
                    attackCooldownSeconds = 0.95f;
                    attackRange = 0.95f;
                    break;
            }
        }

        _runtimeMoveSpeed = moveSpeed;
        _runtimeDamage = contactDamage;
        _runtimeCooldown = attackCooldownSeconds;
        _runtimeAttackRange = attackRange;
    }

    private void ApplyBossPhaseTuning()
    {
        if (maxHp <= 0)
        {
            return;
        }

        var healthPercent = (float)_currentHp / maxHp;
        var nextPhase = healthPercent > 0.66f ? 1 : (healthPercent > 0.33f ? 2 : 3);
        if (nextPhase != _bossPhase)
        {
            _bossPhase = nextPhase;
            _bossRecoveryUntilTime = Time.time + Mathf.Max(0f, bossPhaseTransitionRecoverySeconds);
            _nextAttackTime = Mathf.Max(_nextAttackTime, _bossRecoveryUntilTime);
        }

        if (_bossPhase == 1)
        {
            _runtimeMoveSpeed = moveSpeed;
            _runtimeDamage = contactDamage;
            _runtimeCooldown = attackCooldownSeconds;
            _runtimeAttackRange = 1.1f;
            return;
        }

        if (_bossPhase == 2)
        {
            var cycle = Mathf.Repeat(Time.time, Mathf.Max(0.1f, bossPhase2CycleDuration));
            var sprinting = cycle < bossPhase2SprintDuration;
            _runtimeMoveSpeed = sprinting ? bossPhase2SprintSpeed : moveSpeed * 0.85f;
            _runtimeDamage = Mathf.RoundToInt(contactDamage * 1.2f);
            _runtimeCooldown = Mathf.Max(0.55f, attackCooldownSeconds * 0.9f);
            _runtimeAttackRange = 1.15f;
            return;
        }

        _runtimeMoveSpeed = moveSpeed * 1.2f;
        _runtimeDamage = Mathf.RoundToInt(contactDamage * 1.45f);
        _runtimeCooldown = Mathf.Max(0.45f, attackCooldownSeconds * bossPhase3CooldownMultiplier);
        _runtimeAttackRange = 1.2f;
    }

    private void MoveByArchetype()
    {
        var toTarget = (Vector2)target.position - (Vector2)transform.position;
        var distance = toTarget.magnitude;

        if (distance <= 0.001f)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isBoss && Time.time < _bossRecoveryUntilTime)
        {
            // Short recovery window after phase transition for readable pacing.
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        var direction = toTarget / distance;

        if (archetype == EnemyArchetype.RangedProxy && !isBoss)
        {
            if (distance < rangedPreferredDistance - 0.25f)
            {
                direction = -direction;
            }
            else if (distance < rangedPreferredDistance + 0.2f)
            {
                var perpendicular = new Vector2(-direction.y, direction.x);
                direction = perpendicular;
            }
        }

        _rb.linearVelocity = direction * _runtimeMoveSpeed;
    }

    private void TryAttackByDistance()
    {
        if (_hero == null)
        {
            return;
        }

        if (isBoss && Time.time < _bossRecoveryUntilTime)
        {
            return;
        }

        if (Time.time < _nextAttackTime)
        {
            return;
        }

        var distance = Vector2.Distance(transform.position, _hero.transform.position);
        if (distance > _runtimeAttackRange)
        {
            return;
        }

        var damage = _runtimeDamage;
        if (archetype == EnemyArchetype.RangedProxy && !isBoss && Time.time >= _nextBurstTime)
        {
            damage = Mathf.RoundToInt(damage * rangedBurstDamageMultiplier);
            _nextBurstTime = Time.time + Mathf.Max(1f, rangedBurstIntervalSeconds);
        }

        _hero.TakeDamage(Mathf.Max(1, damage));
        _nextAttackTime = Time.time + Mathf.Max(0.1f, _runtimeCooldown);
    }
}
