using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CaveDweller.Combat;

namespace CaveDweller.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public abstract class BaseEnemy : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] protected int maxHealth = 140;
        [SerializeField] protected int currentHealth = 100;
        [SerializeField] private int contactDamage = 20;

        [Header("Death Settings")]
        [SerializeField] private float destroyDelay = 0.5f;

        [Header("Ground Check Settings")]
        [SerializeField] protected LayerMask groundLayer;
        [SerializeField] protected float groundCheckDistance = 0.15f;

        protected Rigidbody2D cachedRigidbody;
        protected Collider2D cachedCollider;
        protected SpriteRenderer cachedSpriteRenderer;
        protected Color originalColor;
        protected bool isDying;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
        public int ContactDamage => contactDamage;
        public LayerMask GroundLayer => groundLayer;
        public Rigidbody2D CachedRigidbody => cachedRigidbody;
        public Collider2D CachedCollider => cachedCollider;
        protected Rigidbody2D rb => cachedRigidbody;
        protected Collider2D col => cachedCollider;
        protected SpriteRenderer spriteRenderer => cachedSpriteRenderer;

        protected virtual void Awake()
        {
            if (maxHealth < 1)
            {
                maxHealth = 140;
            }

            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedCollider = GetComponent<Collider2D>();
            cachedSpriteRenderer = GetComponent<SpriteRenderer>();
            if (cachedSpriteRenderer == null)
            {
                cachedSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (cachedSpriteRenderer != null)
            {
                originalColor = cachedSpriteRenderer.color;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.freezeRotation = true;
            }

            if (currentHealth <= 0 || currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        protected virtual void Start()
        {
            if (maxHealth < 1)
            {
                maxHealth = 1;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        protected virtual void Update()
        {
#if UNITY_EDITOR
            HandleDebugInput();
#endif
        }

        public virtual void TakeDamage(int amount)
        {
            if (IsDead)
            {
                return;
            }

            if (amount <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);

            if (cachedSpriteRenderer != null)
            {
                StartCoroutine(FlashOnDamageRoutine());
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public virtual void Die()
        {
            if (isDying)
            {
                return;
            }

            isDying = true;

            if (cachedCollider != null)
            {
                cachedCollider.enabled = false;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.linearVelocity = Vector2.zero;
                cachedRigidbody.simulated = false;
            }

            enabled = false;

            Destroy(gameObject, destroyDelay);
        }

        public virtual void DealDamageToPlayer(IDamageable target, int amount)
        {
            if (target == null || target.IsDead) return;
            target.TakeDamage(amount);
        }

        public virtual void DealDamageToPlayer(GameObject targetObj, int amount)
        {
            if (targetObj == null) return;
            var d = targetObj.GetComponent<IDamageable>();
            if (d == null) d = targetObj.GetComponentInParent<IDamageable>();
            if (d == null) d = targetObj.GetComponentInChildren<IDamageable>();
            if (d == null || d.IsDead) return;
            d.TakeDamage(amount);
        }

        public virtual void DealDamageToPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                return;
            }

            IDamageable damageable = playerObj.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = playerObj.GetComponentInParent<IDamageable>();
            }

            if (damageable == null)
            {
                damageable = playerObj.GetComponentInChildren<IDamageable>();
            }

            if (damageable == null)
            {
                return;
            }

            if (damageable.IsDead)
            {
                return;
            }

            damageable.TakeDamage(contactDamage);
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.gameObject == null)
            {
                return;
            }

            if (!collision.gameObject.CompareTag("Player"))
            {
                return;
            }

            DealDamageToPlayer();
        }

        protected bool CheckGrounded()
        {
            if (cachedCollider == null)
            {
                return false;
            }

            Bounds bounds = cachedCollider.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y);
            Vector2 size = new Vector2(bounds.size.x * 0.8f, 0.05f);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);
            return hit.collider != null;
        }

        protected bool CheckWallAhead(float direction, float distance)
        {
            if (cachedCollider == null)
            {
                return false;
            }

            Bounds bounds = cachedCollider.bounds;
            Vector2 origin = bounds.center;
            Vector2 size = new Vector2(0.05f, bounds.size.y * 0.8f);
            Vector2 dir = direction >= 0f ? Vector2.right : Vector2.left;
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, dir, distance, groundLayer);
            return hit.collider != null;
        }

        private IEnumerator FlashOnDamageRoutine()
        {
            if (cachedSpriteRenderer == null)
            {
                yield break;
            }

            Color flashColor = Color.red;
            cachedSpriteRenderer.color = flashColor;

            yield return new WaitForSeconds(0.1f);

            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.color = originalColor;
            }
        }

        private void HandleDebugInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.kKey.wasPressedThisFrame)
            {
                TakeDamage(10);
            }
#else
            if (Input.GetKeyDown(KeyCode.K))
            {
                TakeDamage(10);
            }
#endif
        }

        protected virtual void OnValidate()
        {
            if (maxHealth < 1)
            {
                maxHealth = 1;
            }

            if (contactDamage < 0)
            {
                contactDamage = 0;
            }

            if (destroyDelay < 0f)
            {
                destroyDelay = 0f;
            }

            if (groundCheckDistance < 0f)
            {
                groundCheckDistance = 0f;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
    }
}
