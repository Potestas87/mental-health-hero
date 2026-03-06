using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController2D : MonoBehaviour
{
    public int maxHp = 40;
    public int contactDamage = 10;
    public float moveSpeed = 2f;
    public float attackCooldownSeconds = 1f;
    public bool isBoss;

    [Header("Optional References")]
    public Transform target;
    public DungeonRunManager runManager;

    private Rigidbody2D _rb;
    private HeroController2D _hero;
    private int _currentHp;
    private float _nextAttackTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _currentHp = maxHp;
    }

    private void Start()
    {
        if (target == null)
        {
            var hero = FindObjectOfType<HeroController2D>();
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

    private void FixedUpdate()
    {
        if (target == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        var direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * moveSpeed;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.time < _nextAttackTime)
        {
            return;
        }

        var hero = collision.gameObject.GetComponent<HeroController2D>();
        if (hero == null)
        {
            return;
        }

        hero.TakeDamage(contactDamage);
        _nextAttackTime = Time.time + attackCooldownSeconds;
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
}
