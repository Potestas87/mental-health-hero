using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class HeroController2D : MonoBehaviour
{
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
    private Vector2 _moveInput;
    private int _currentHp;
    private float _nextAttackTime;

    public int MaxHp => maxHp;
    public int CurrentHp => _currentHp;
    public string ActiveClass { get; private set; } = "warrior";

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
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

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
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
        ActiveClass = normalized;

        switch (normalized)
        {
            case "ranger":
                maxHp = 90;
                moveSpeed = 5.2f;
                attackDamage = 19;
                attackRange = 1.5f;
                attackCooldownSeconds = 0.24f;
                break;
            case "mage":
                maxHp = 80;
                moveSpeed = 4.1f;
                attackDamage = 16;
                attackRange = 2.25f;
                attackCooldownSeconds = 0.2f;
                break;
            default:
                ActiveClass = "warrior";
                maxHp = 130;
                moveSpeed = 3.5f;
                attackDamage = 24;
                attackRange = 1.15f;
                attackCooldownSeconds = 0.34f;
                break;
        }

        ResetHealthToFull();
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
