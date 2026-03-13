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
            attackCooldownSeconds = 0.34f
        },
        new PlayerClassArchetype
        {
            classId = "ranger",
            maxHp = 90,
            moveSpeed = 5.2f,
            attackDamage = 19,
            attackRange = 1.5f,
            attackCooldownSeconds = 0.24f
        },
        new PlayerClassArchetype
        {
            classId = "mage",
            maxHp = 80,
            moveSpeed = 4.1f,
            attackDamage = 16,
            attackRange = 2.25f,
            attackCooldownSeconds = 0.2f
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
    private bool _invertHorizontalFacing;
    private float _idleAnimSpeed = 1f;
    private float _runAnimSpeed = 1f;
    private float _attackAnimSpeed = 1f;

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
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            _moveInput = Vector2.zero;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) _moveInput.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) _moveInput.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) _moveInput.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) _moveInput.y += 1f;
        }

        _moveInput = _moveInput.normalized;

        if (_spriteRenderer != null)
        {
            if (_moveInput.x < -0.01f) _spriteRenderer.flipX = !_invertHorizontalFacing;
            else if (_moveInput.x > 0.01f) _spriteRenderer.flipX = _invertHorizontalFacing;
        }

        if (_animator != null)
        {
            _animator.SetFloat("Speed", _moveInput.sqrMagnitude);
            ApplyAnimatorSpeedForCurrentClip();
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            if (_animator != null)
            {
                _animator.SetTrigger(AttackTriggerName);
            }
            PerformAttack();
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
        maxHp = Mathf.Max(1, archetype.maxHp);
        moveSpeed = Mathf.Max(0.5f, archetype.moveSpeed);
        attackDamage = Mathf.Max(1, archetype.attackDamage);
        attackRange = Mathf.Max(0.4f, archetype.attackRange);
        attackCooldownSeconds = Mathf.Max(0.05f, archetype.attackCooldownSeconds);
        ApplyArchetypeVisuals(archetype);
        ResetHealthToFull();
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
        if (baseIdleStateClip != null && archetype.idleClip != null)
        {
            overrideController[baseIdleStateClip.name] = archetype.idleClip;
        }

        if (baseRunStateClip != null && archetype.runClip != null)
        {
            overrideController[baseRunStateClip.name] = archetype.runClip;
        }

        if (baseAttackStateClip != null && archetype.attackClip != null)
        {
            overrideController[baseAttackStateClip.name] = archetype.attackClip;
        }

        _animator.runtimeAnimatorController = overrideController;
        Debug.Log(
            "HeroController2D: Applied class visuals for " + ActiveClass +
            " | idle=" + (archetype.idleClip != null ? archetype.idleClip.name : "null") +
            " run=" + (archetype.runClip != null ? archetype.runClip.name : "null") +
            " attack=" + (archetype.attackClip != null ? archetype.attackClip.name : "null")
        );
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
