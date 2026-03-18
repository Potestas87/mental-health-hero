using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class HeroController2D : MonoBehaviour
{
    [System.Serializable]
    public sealed class PlayerClassArchetype
    {
        public string classId = "warrior";
        public int maxHp = 100;
        public float moveSpeed = 4f;
        public int attackDamage = 20;
        public float attackRange = 1.2f;
        public float attackCooldownSeconds = 0.3f;
        public float basicAttackMovementLockSeconds = 0.35f;
        public GameObject classFxPrefab;
        public string basicFxTriggerName = "PlayBasic";
        public string movementFxTriggerName = "PlayMove";
        public string aoeFxTriggerName = "PlayAoe";
        public float basicFxRotationOffsetZ;
        public float movementFxRotationOffsetZ;
        public float aoeFxRotationOffsetZ;
        public bool snapFxToEightDirections = true;
        public float attackEffectOffset = 0.9f;
        public float attackEffectLifetime = 0.25f;
        public float movementAbilityCooldownSeconds = 4f;
        public float movementAbilityDashDistance = 2.5f;
        public int movementAbilityDamage = 12;
        public float movementAbilityDamageRadius = 1.1f;
        public float movementAbilityEffectOffset = 0.9f;
        public float movementAbilityEffectLifetime = 0.3f;
        public float aoeAbilityCooldownSeconds = 8f;
        public int aoeAbilityDamage = 14;
        public float aoeAbilityRadius = 1.8f;
        public float aoeAbilityMovementLockSeconds = 0.45f;
        public float aoeAbilityEffectOffset = 0f;
        public float aoeAbilityEffectLifetime = 0.4f;
        public Sprite idleSprite;
        public RuntimeAnimatorController animatorController;
        public AnimationClip idleClip;
        public AnimationClip runClip;
        public AnimationClip attackClip;
        public bool invertHorizontalFacing;
        public float idleAnimSpeed = 1f;
        public float runAnimSpeed = 1f;
        public float attackAnimSpeed = 1f;
    }

    private const string AttackTriggerName = "Attack";
    private const string SpeedParam = "Speed";
    private const string MoveXParam = "MoveX";
    private const string MoveYParam = "MoveY";
    private const string FaceXParam = "FaceX";
    private const string FaceYParam = "FaceY";

    [Header("Class Archetypes")]
    public List<PlayerClassArchetype> classArchetypes = new List<PlayerClassArchetype>
    {
        new PlayerClassArchetype
        {
            classId = "warrior",
            maxHp = 130,
            moveSpeed = 3.5f,
            attackDamage = 24,
            attackRange = 1.15f,
            attackCooldownSeconds = 0.34f,
            basicAttackMovementLockSeconds = 0.42f,
            movementAbilityCooldownSeconds = 4.5f,
            movementAbilityDashDistance = 2.2f,
            movementAbilityDamage = 18,
            movementAbilityDamageRadius = 1.15f,
            aoeAbilityCooldownSeconds = 10f,
            aoeAbilityDamage = 24,
            aoeAbilityRadius = 2.0f,
            aoeAbilityMovementLockSeconds = 0.58f
        },
        new PlayerClassArchetype
        {
            classId = "ranger",
            maxHp = 90,
            moveSpeed = 5.2f,
            attackDamage = 19,
            attackRange = 1.5f,
            attackCooldownSeconds = 0.24f,
            basicAttackMovementLockSeconds = 0.22f,
            movementAbilityCooldownSeconds = 3.4f,
            movementAbilityDashDistance = 3.0f,
            movementAbilityDamage = 12,
            movementAbilityDamageRadius = 0.95f,
            aoeAbilityCooldownSeconds = 9f,
            aoeAbilityDamage = 16,
            aoeAbilityRadius = 1.7f,
            aoeAbilityMovementLockSeconds = 0.38f
        },
        new PlayerClassArchetype
        {
            classId = "mage",
            maxHp = 80,
            moveSpeed = 4.1f,
            attackDamage = 16,
            attackRange = 2.25f,
            attackCooldownSeconds = 0.2f,
            basicAttackMovementLockSeconds = 0.2f,
            movementAbilityCooldownSeconds = 3.2f,
            movementAbilityDashDistance = 2.7f,
            movementAbilityDamage = 10,
            movementAbilityDamageRadius = 1.25f,
            aoeAbilityCooldownSeconds = 8f,
            aoeAbilityDamage = 22,
            aoeAbilityRadius = 2.25f,
            aoeAbilityMovementLockSeconds = 0.5f
        }
    };

    [Header("Animation Template (Optional)")]
    public RuntimeAnimatorController baseAnimatorController;
    public AnimationClip baseIdleStateClip;
    public AnimationClip baseRunStateClip;
    public AnimationClip baseAttackStateClip;

    [Header("Animation Speed Detection")]
    public string idleClipKeyword = "idle";
    public string runClipKeyword = "run";
    public string movementClipKeyword = "movement";
    public string attackClipKeyword = "attack";
    public bool useSpriteFlipForFacing;

    [Header("Runtime Combat Stats")]
    public float moveSpeed = 4f;
    public int maxHp = 100;
    public int attackDamage = 20;
    public float attackRange = 1.2f;
    public float attackCooldownSeconds = 0.3f;
    public LayerMask enemyLayerMask;

    [Header("Optional References")]
    public DungeonRunManager runManager;
    public Transform attackOrigin;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Vector2 _moveInput;
    private int _currentHp;
    private float _nextAttackTime;
    private float _nextMovementAbilityTime;
    private float _nextAoeAbilityTime;
    private float _movementLockUntilTime;
    private bool _invertHorizontalFacing;
    private float _idleAnimSpeed = 1f;
    private float _runAnimSpeed = 1f;
    private float _attackAnimSpeed = 1f;
    private Vector2 _facingDirection = Vector2.down;
    private float _attackEffectOffset = 0.9f;
    private float _attackEffectLifetime = 0.25f;
    private float _movementAbilityCooldownSeconds = 4f;
    private float _movementAbilityDashDistance = 2.5f;
    private int _movementAbilityDamage = 12;
    private float _movementAbilityDamageRadius = 1.1f;
    private float _movementAbilityEffectOffset = 0.9f;
    private float _movementAbilityEffectLifetime = 0.3f;
    private float _aoeAbilityCooldownSeconds = 8f;
    private int _aoeAbilityDamage = 14;
    private float _aoeAbilityRadius = 1.8f;
    private float _aoeAbilityEffectOffset;
    private float _aoeAbilityEffectLifetime = 0.4f;
    private float _basicAttackMovementLockSeconds = 0.35f;
    private float _aoeAbilityMovementLockSeconds = 0.45f;
    private GameObject _classFxPrefab;
    private string _basicFxTriggerName = "PlayBasic";
    private string _movementFxTriggerName = "PlayMove";
    private string _aoeFxTriggerName = "PlayAoe";
    private float _basicFxRotationOffsetZ;
    private float _movementFxRotationOffsetZ;
    private float _aoeFxRotationOffsetZ;
    private bool _snapFxToEightDirections = true;

    public int MaxHp => maxHp;
    public int CurrentHp => _currentHp;
    public string ActiveClass { get; private set; } = "warrior";

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _currentHp = maxHp;
    }

    private void Update()
    {
        var movementLocked = Time.time < _movementLockUntilTime;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            _moveInput = Vector2.zero;

            if (!movementLocked)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) _moveInput.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) _moveInput.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) _moveInput.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) _moveInput.y += 1f;
            }
        }

        _moveInput = _moveInput.normalized;
        if (_moveInput.sqrMagnitude > 0.0001f)
        {
            _facingDirection = _moveInput;
        }

        if (_spriteRenderer != null && useSpriteFlipForFacing)
        {
            if (_moveInput.x < -0.01f) _spriteRenderer.flipX = !_invertHorizontalFacing;
            else if (_moveInput.x > 0.01f) _spriteRenderer.flipX = _invertHorizontalFacing;
        }

        if (_animator != null)
        {
            _animator.SetFloat(SpeedParam, _moveInput.sqrMagnitude);
            _animator.SetFloat(MoveXParam, _moveInput.x);
            _animator.SetFloat(MoveYParam, _moveInput.y);
            _animator.SetFloat(FaceXParam, _facingDirection.x);
            _animator.SetFloat(FaceYParam, _facingDirection.y);
            ApplyAnimatorSpeedForCurrentClip();
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            PerformAttack();
        }

        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            UseMovementAbility();
        }

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            UseAoeAbility();
        }
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = _moveInput * moveSpeed;
    }

    public void PerformAttack()
    {
        if (Time.time < _nextAttackTime)
        {
            return;
        }

        _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldownSeconds);
        BeginMovementLock(_basicAttackMovementLockSeconds);
        TriggerAttackAnimationAndEffect(_attackEffectOffset, _attackEffectLifetime, _basicFxTriggerName);

        var origin = attackOrigin != null ? (Vector2)attackOrigin.position : (Vector2)transform.position;
        var hits = Physics2D.OverlapCircleAll(origin, attackRange, enemyLayerMask);

        for (var i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponent<EnemyController2D>();
            if (enemy != null)
            {
                enemy.TakeDamage(attackDamage);
            }
        }
    }

    public void UseMovementAbility()
    {
        if (Time.time < _nextMovementAbilityTime)
        {
            return;
        }

        _nextMovementAbilityTime = Time.time + Mathf.Max(0.1f, _movementAbilityCooldownSeconds);

        var dir = _moveInput.sqrMagnitude > 0.0001f ? _moveInput : _facingDirection;
        if (dir.sqrMagnitude <= 0.0001f)
        {
            dir = Vector2.down;
        }
        dir = dir.normalized;
        _facingDirection = dir;

        var dashDistance = Mathf.Max(0.1f, _movementAbilityDashDistance);
        var startPos = _rb.position;
        var endPos = startPos + dir * dashDistance;
        _rb.position = endPos;

        SpawnClassEffect(dir, _movementAbilityEffectOffset, _movementAbilityEffectLifetime, _movementFxTriggerName);
        DamageEnemiesInCircle(endPos, _movementAbilityDamageRadius, _movementAbilityDamage);
    }

    public void UseAoeAbility()
    {
        if (Time.time < _nextAoeAbilityTime)
        {
            return;
        }

        _nextAoeAbilityTime = Time.time + Mathf.Max(0.1f, _aoeAbilityCooldownSeconds);
        BeginMovementLock(_aoeAbilityMovementLockSeconds);
        TriggerAttackAnimationAndEffect(_aoeAbilityEffectOffset, _aoeAbilityEffectLifetime, _aoeFxTriggerName);

        var center = (Vector2)transform.position;
        DamageEnemiesInCircle(center, _aoeAbilityRadius, _aoeAbilityDamage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _currentHp = Mathf.Max(0, _currentHp - amount);
        if (runManager != null)
        {
            runManager.OnPlayerHpChanged(_currentHp, maxHp);
        }

        if (_currentHp <= 0)
        {
            if (runManager != null)
            {
                runManager.PlayerDied();
            }
        }
    }

    public void ResetHealthToFull()
    {
        _currentHp = maxHp;
        if (runManager != null)
        {
            runManager.OnPlayerHpChanged(_currentHp, maxHp);
        }
    }

    public void ApplyClassProfile(string classId)
    {
        var normalized = string.IsNullOrEmpty(classId) ? "warrior" : classId.Trim().ToLowerInvariant();

        if (!TryGetClassArchetype(normalized, out var archetype))
        {
            if (!TryGetClassArchetype("warrior", out archetype))
            {
                Debug.LogWarning("HeroController2D: warrior archetype missing. Falling back to current runtime stats.");
                ActiveClass = "warrior";
                ResetHealthToFull();
                return;
            }
        }

        ActiveClass = string.IsNullOrWhiteSpace(archetype.classId) ? "warrior" : archetype.classId.Trim().ToLowerInvariant();
        _invertHorizontalFacing = archetype.invertHorizontalFacing;
        _idleAnimSpeed = Mathf.Max(0.05f, archetype.idleAnimSpeed);
        _runAnimSpeed = Mathf.Max(0.05f, archetype.runAnimSpeed);
        _attackAnimSpeed = Mathf.Max(0.05f, archetype.attackAnimSpeed);
        _attackEffectOffset = Mathf.Max(0f, archetype.attackEffectOffset);
        _attackEffectLifetime = Mathf.Max(0.05f, archetype.attackEffectLifetime);
        _classFxPrefab = archetype.classFxPrefab;
        _basicFxTriggerName = string.IsNullOrWhiteSpace(archetype.basicFxTriggerName) ? "PlayBasic" : archetype.basicFxTriggerName.Trim();
        _movementFxTriggerName = string.IsNullOrWhiteSpace(archetype.movementFxTriggerName) ? "PlayMove" : archetype.movementFxTriggerName.Trim();
        _aoeFxTriggerName = string.IsNullOrWhiteSpace(archetype.aoeFxTriggerName) ? "PlayAoe" : archetype.aoeFxTriggerName.Trim();
        _basicFxRotationOffsetZ = archetype.basicFxRotationOffsetZ;
        _movementFxRotationOffsetZ = archetype.movementFxRotationOffsetZ;
        _aoeFxRotationOffsetZ = archetype.aoeFxRotationOffsetZ;
        _snapFxToEightDirections = archetype.snapFxToEightDirections;
        _movementAbilityCooldownSeconds = Mathf.Max(0.1f, archetype.movementAbilityCooldownSeconds);
        _movementAbilityDashDistance = Mathf.Max(0.1f, archetype.movementAbilityDashDistance);
        _movementAbilityDamage = Mathf.Max(1, archetype.movementAbilityDamage);
        _movementAbilityDamageRadius = Mathf.Max(0.2f, archetype.movementAbilityDamageRadius);
        _movementAbilityEffectOffset = Mathf.Max(0f, archetype.movementAbilityEffectOffset);
        _movementAbilityEffectLifetime = Mathf.Max(0.05f, archetype.movementAbilityEffectLifetime);
        _aoeAbilityCooldownSeconds = Mathf.Max(0.1f, archetype.aoeAbilityCooldownSeconds);
        _aoeAbilityDamage = Mathf.Max(1, archetype.aoeAbilityDamage);
        _aoeAbilityRadius = Mathf.Max(0.2f, archetype.aoeAbilityRadius);
        _aoeAbilityEffectOffset = archetype.aoeAbilityEffectOffset;
        _aoeAbilityEffectLifetime = Mathf.Max(0.05f, archetype.aoeAbilityEffectLifetime);
        _basicAttackMovementLockSeconds = Mathf.Max(0f, archetype.basicAttackMovementLockSeconds);
        _aoeAbilityMovementLockSeconds = Mathf.Max(0f, archetype.aoeAbilityMovementLockSeconds);
        maxHp = Mathf.Max(1, archetype.maxHp);
        moveSpeed = Mathf.Max(0.5f, archetype.moveSpeed);
        attackDamage = Mathf.Max(1, archetype.attackDamage);
        attackRange = Mathf.Max(0.4f, archetype.attackRange);
        attackCooldownSeconds = Mathf.Max(0.05f, archetype.attackCooldownSeconds);
        ApplyArchetypeVisuals(archetype);
        ResetHealthToFull();
    }

    private void BeginMovementLock(float durationSeconds)
    {
        var lockUntil = Time.time + Mathf.Max(0f, durationSeconds);
        if (lockUntil > _movementLockUntilTime)
        {
            _movementLockUntilTime = lockUntil;
        }
    }

    private void TriggerAttackAnimationAndEffect(float offset, float lifetime, string fxTriggerName)
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AttackTriggerName);
        }

        SpawnClassEffect(_facingDirection, offset, lifetime, fxTriggerName);
    }

    private void SpawnClassEffect(Vector2 direction, float forwardOffset, float lifetime, string fxTriggerName)
    {
        if (_classFxPrefab == null)
        {
            return;
        }

        var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (_snapFxToEightDirections)
        {
            angle = SnapAngleToEightDirections(angle);
            var radians = angle * Mathf.Deg2Rad;
            dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        var spawnPos = (Vector2)transform.position + (dir * forwardOffset);
        var rotation = Quaternion.Euler(0f, 0f, angle + GetFxRotationOffset(fxTriggerName));

        // Use a rotated parent so clip-authored child transform keys do not cancel facing orientation.
        var fxHolder = new GameObject("ClassFxHolder");
        fxHolder.transform.SetPositionAndRotation(spawnPos, rotation);
        var spawned = Instantiate(_classFxPrefab, fxHolder.transform);
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.identity;

        var fxAnimator = spawned.GetComponent<Animator>();
        if (fxAnimator != null && !string.IsNullOrWhiteSpace(fxTriggerName))
        {
            fxAnimator.SetTrigger(fxTriggerName);
        }

        Destroy(fxHolder, Mathf.Max(0.05f, lifetime));
    }

    private static float SnapAngleToEightDirections(float angle)
    {
        var normalized = Mathf.Repeat(angle, 360f);
        return Mathf.Round(normalized / 45f) * 45f;
    }

    private float GetFxRotationOffset(string fxTriggerName)
    {
        if (string.Equals(fxTriggerName, _basicFxTriggerName, System.StringComparison.Ordinal))
        {
            return _basicFxRotationOffsetZ;
        }

        if (string.Equals(fxTriggerName, _movementFxTriggerName, System.StringComparison.Ordinal))
        {
            return _movementFxRotationOffsetZ;
        }

        if (string.Equals(fxTriggerName, _aoeFxTriggerName, System.StringComparison.Ordinal))
        {
            return _aoeFxRotationOffsetZ;
        }

        return 0f;
    }

    private void DamageEnemiesInCircle(Vector2 center, float radius, int damage)
    {
        var hits = Physics2D.OverlapCircleAll(center, Mathf.Max(0.1f, radius), enemyLayerMask);
        for (var i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponent<EnemyController2D>();
            if (enemy != null)
            {
                enemy.TakeDamage(Mathf.Max(1, damage));
            }
        }
    }

    private bool TryGetClassArchetype(string classId, out PlayerClassArchetype archetype)
    {
        archetype = null;
        if (classArchetypes == null || classArchetypes.Count == 0)
        {
            return false;
        }

        var normalized = string.IsNullOrWhiteSpace(classId) ? "warrior" : classId.Trim().ToLowerInvariant();
        for (var i = 0; i < classArchetypes.Count; i++)
        {
            var item = classArchetypes[i];
            if (item == null)
            {
                continue;
            }

            var itemId = string.IsNullOrWhiteSpace(item.classId) ? "warrior" : item.classId.Trim().ToLowerInvariant();
            if (itemId == normalized)
            {
                archetype = item;
                return true;
            }
        }

        return false;
    }

    private void ApplyArchetypeVisuals(PlayerClassArchetype archetype)
    {
        if (_spriteRenderer != null && archetype.idleSprite != null)
        {
            _spriteRenderer.sprite = archetype.idleSprite;
        }

        if (_animator == null)
        {
            return;
        }

        if (archetype.animatorController != null)
        {
            _animator.runtimeAnimatorController = archetype.animatorController;
            return;
        }

        if (baseAnimatorController == null)
        {
            return;
        }

        var hasClipOverrides = archetype.idleClip != null || archetype.runClip != null || archetype.attackClip != null;
        if (!hasClipOverrides)
        {
            _animator.runtimeAnimatorController = baseAnimatorController;
            return;
        }

        var overrideController = new AnimatorOverrideController(baseAnimatorController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        if (baseIdleStateClip != null && archetype.idleClip != null)
        {
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(baseIdleStateClip, archetype.idleClip));
        }

        if (baseRunStateClip != null && archetype.runClip != null)
        {
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(baseRunStateClip, archetype.runClip));
        }

        if (baseAttackStateClip != null && archetype.attackClip != null)
        {
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(baseAttackStateClip, archetype.attackClip));
        }

        if (overrides.Count > 0)
        {
            overrideController.ApplyOverrides(overrides);
        }

        // Fallback by clip name in case base clip object refs differ from controller internals.
        TrySetOverrideByName(overrideController, baseIdleStateClip, archetype.idleClip, "idle");
        TrySetOverrideByName(overrideController, baseRunStateClip, archetype.runClip, "run");
        TrySetOverrideByName(overrideController, baseAttackStateClip, archetype.attackClip, "attack");

        _animator.runtimeAnimatorController = overrideController;
        Debug.Log(
            "HeroController2D: Applied class visuals for " + ActiveClass +
            " | idle=" + (archetype.idleClip != null ? archetype.idleClip.name : "null") +
            " run=" + (archetype.runClip != null ? archetype.runClip.name : "null") +
            " attack=" + (archetype.attackClip != null ? archetype.attackClip.name : "null")
        );
    }

    private void TrySetOverrideByName(
        AnimatorOverrideController overrideController,
        AnimationClip baseClip,
        AnimationClip replacementClip,
        string label)
    {
        if (overrideController == null || baseClip == null || replacementClip == null)
        {
            return;
        }

        try
        {
            overrideController[baseClip.name] = replacementClip;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning(
                "HeroController2D: Failed " + label + " clip override by name. base=" + baseClip.name +
                " replacement=" + replacementClip.name + " error=" + ex.Message
            );
        }
    }

    private void ApplyAnimatorSpeedForCurrentClip()
    {
        if (_animator == null)
        {
            return;
        }

        var clips = _animator.GetCurrentAnimatorClipInfo(0);
        if (clips == null || clips.Length == 0 || clips[0].clip == null)
        {
            _animator.speed = 1f;
            return;
        }

        var clipName = clips[0].clip.name.ToLowerInvariant();
        if (ContainsKeyword(clipName, attackClipKeyword))
        {
            _animator.speed = _attackAnimSpeed;
            return;
        }

        if (ContainsKeyword(clipName, runClipKeyword) || ContainsKeyword(clipName, movementClipKeyword))
        {
            _animator.speed = _runAnimSpeed;
            return;
        }

        if (ContainsKeyword(clipName, idleClipKeyword))
        {
            _animator.speed = _idleAnimSpeed;
            return;
        }

        _animator.speed = 1f;
    }

    private static bool ContainsKeyword(string clipName, string keyword)
    {
        return !string.IsNullOrWhiteSpace(keyword) && clipName.Contains(keyword.ToLowerInvariant());
    }

    public void ApplyUpgradeBonuses(ICollection<string> purchasedNodeIds)
    {
        if (purchasedNodeIds == null || purchasedNodeIds.Count == 0)
        {
            return;
        }

        var hpBonus = 0;
        var damageBonus = 0;
        var moveSpeedBonus = 0f;
        var rangeBonus = 0f;
        var cooldownMultiplier = 1f;

        foreach (var nodeId in purchasedNodeIds)
        {
            if (!UpgradeCatalog.TryGetNode(nodeId, out var node))
            {
                continue;
            }

            if (!string.Equals(node.ClassId, ActiveClass, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hpBonus += node.BonusMaxHp;
            damageBonus += node.BonusAttackDamage;
            moveSpeedBonus += node.BonusMoveSpeed;
            rangeBonus += node.BonusAttackRange;
            cooldownMultiplier *= node.AttackCooldownMultiplier;
        }

        maxHp = Mathf.Max(1, maxHp + hpBonus);
        attackDamage = Mathf.Max(1, attackDamage + damageBonus);
        moveSpeed = Mathf.Max(0.5f, moveSpeed + moveSpeedBonus);
        attackRange = Mathf.Max(0.4f, attackRange + rangeBonus);
        attackCooldownSeconds = Mathf.Max(0.05f, attackCooldownSeconds * cooldownMultiplier);

        ResetHealthToFull();
    }

    public void ApplyCosmeticTint(string tintId)
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.color = CosmeticsCatalog.GetTintColor(tintId);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        var origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
