using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CaveDweller.Combat;

namespace CaveDweller.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public abstract class BaseEnemy : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] protected int maxHealth = 36;
        [SerializeField] protected int currentHealth = 36;
        [SerializeField] private int contactDamage = 20;

        [Header("Death Settings")]
        [SerializeField] private float destroyDelay = 0.5f;

        [Header("Damage Visual Settings")]
        [Tooltip("Flash color effect when enemy takes damage.")]
        [SerializeField] private bool enableHitFlash = true;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("Ground Check Settings")]
        [SerializeField] protected LayerMask groundLayer;
        [SerializeField] protected float groundCheckDistance = 0.15f;

        [Header("Friction")]
        [Tooltip("Shared frictionless material (Project asset). If empty, the collider keeps its current material and a warning is logged once so broken content never hides behind a silent runtime fix.")]
        [SerializeField] private PhysicsMaterial2D frictionlessMaterial;

        protected Rigidbody2D cachedRigidbody;
        protected Collider2D cachedCollider;
        protected SpriteRenderer cachedSpriteRenderer;
        protected Animator cachedAnimator;
        protected Color originalColor;
        protected bool isDying;
        private Coroutine hitFlashRoutine;

        protected int animSpeedHash;
        protected int animAttackTriggerHash;
        protected int animShootTriggerHash;
        protected int animIsAttackingHash;
        protected int animIsWalkingHash;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
        public int ContactDamage => contactDamage;
        public LayerMask GroundLayer => groundLayer;
        public Rigidbody2D CachedRigidbody => cachedRigidbody;
        public Collider2D CachedCollider => cachedCollider;
        public Animator Animator => cachedAnimator;
        protected Rigidbody2D rb => cachedRigidbody;
        protected Collider2D col => cachedCollider;
        protected SpriteRenderer spriteRenderer => cachedSpriteRenderer;

        protected virtual void Awake()
        {
            if (maxHealth < 1)
            {
                maxHealth = 36;
            }

            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedCollider = GetComponent<Collider2D>();
            cachedSpriteRenderer = GetComponent<SpriteRenderer>();
            if (cachedSpriteRenderer == null)
            {
                cachedSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            cachedAnimator = GetComponent<Animator>();
            if (cachedAnimator == null)
            {
                cachedAnimator = GetComponentInChildren<Animator>();
            }

            animSpeedHash = Animator.StringToHash("Speed");
            animAttackTriggerHash = Animator.StringToHash("Attack");
            animShootTriggerHash = Animator.StringToHash("Shoot");
            animIsAttackingHash = Animator.StringToHash("IsAttacking");
            animIsWalkingHash = Animator.StringToHash("IsWalking");

            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.color = Color.white;
                originalColor = Color.white;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.freezeRotation = true;
            }

            if (cachedCollider != null)
            {
                if (frictionlessMaterial != null)
                {
                    cachedCollider.sharedMaterial = frictionlessMaterial;
                }
                else if (cachedCollider.sharedMaterial == null)
                {
                    Debug.LogWarning("[BaseEnemy] frictionlessMaterial is not assigned and collider has no material. Friction left as-is.", this);
                }
            }

            currentHealth = maxHealth;
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

            PlayHitFlash();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Hit feedback hook. Subclasses with custom visuals (e.g. stealth alpha)
        /// override this instead of starting a second, competing flash coroutine.
        /// </summary>
        protected virtual void PlayHitFlash()
        {
            if (!enableHitFlash || cachedSpriteRenderer == null) return;
            if (hitFlashRoutine != null) StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = StartCoroutine(FlashOnDamageRoutine());
        }

        public virtual void Die()
        {
            if (isDying)
            {
                return;
            }

            isDying = true;

            CaveDweller.Core.SoundManager.Instance?.PlayEnemyDieSFX();

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

        /// <summary>
        /// Single damage path for every enemy attack (melee, lunge, contact):
        /// attack animation + melee SFX + damage.
        /// </summary>
        public virtual void DealDamageToPlayer(IDamageable target, int amount)
        {
            if (target == null || target.IsDead) return;
            if (cachedAnimator != null)
            {
                cachedAnimator.SetTrigger(animAttackTriggerHash);
                cachedAnimator.SetBool(animIsAttackingHash, true);
                StartCoroutine(ResetAttackBoolRoutine());
            }
            CaveDweller.Core.SoundManager.Instance?.PlayMonsterMeleeSFX();
            target.TakeDamage(amount);
        }

        /// <summary>Finds the IDamageable on an object, its parents, or its children.</summary>
        protected static IDamageable ResolveDamageable(GameObject obj)
        {
            if (obj == null) return null;
            if (obj.TryGetComponent<IDamageable>(out var damageable)) return damageable;
            damageable = obj.GetComponentInParent<IDamageable>();
            if (damageable != null) return damageable;
            return obj.GetComponentInChildren<IDamageable>();
        }

        private IEnumerator ResetAttackBoolRoutine()
        {
            yield return new WaitForSeconds(0.4f);
            if (cachedAnimator != null)
            {
                cachedAnimator.SetBool(animIsAttackingHash, false);
            }
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead || collision == null || collision.gameObject == null) return;
            if (!collision.gameObject.CompareTag("Player")) return;

            DealDamageToPlayer(ResolveDamageable(collision.gameObject), contactDamage);
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

            cachedSpriteRenderer.color = hitFlashColor;

            yield return new WaitForSeconds(0.1f);

            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.color = originalColor;
            }
            hitFlashRoutine = null;
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
