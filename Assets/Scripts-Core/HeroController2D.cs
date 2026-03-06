using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class HeroController2D : MonoBehaviour
{
    public float moveSpeed = 4f;
    public int maxHp = 100;
    public int attackDamage = 20;
    public float attackRange = 1.2f;
    public LayerMask enemyLayerMask;

    [Header("Optional References")]
    public DungeonRunManager runManager;
    public Transform attackOrigin;

    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private int _currentHp;

    public int MaxHp => maxHp;
    public int CurrentHp => _currentHp;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        var origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
