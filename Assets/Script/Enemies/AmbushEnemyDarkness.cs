using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CaveDweller.Combat;
using CaveDweller.Lighting;

namespace CaveDweller.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class AmbushEnemyDarkness : BaseEnemy, ILightDetectable
    {
        [Header("Ambush Settings")]
        [SerializeField] private float proximityRadius = 2.5f;
        [SerializeField] private float lungeSpeed = 10f;
        [SerializeField] private float lungeDuration = 0.35f;
        [SerializeField] private float lungeCooldown = 2f;
        [SerializeField] private float returnToDormantDelay = 2f;

        [Header("Stealth Visuals")]
        [SerializeField] private float dormantAlpha = 0.35f;
        [SerializeField] private float awakenedAlpha = 1f;
        [SerializeField] private Light2D ambushLight;

        [Header("Detection")]
        [SerializeField] private LayerMask obstacleLayer;

        [Header("References")]
        [SerializeField] private Transform playerTransform;

        private bool isAwakened;
        private bool isLunging;
        private float lastLungeTime = -Mathf.Infinity;
        private float originalGravity;
        private Coroutine dormantRoutine;
        private Coroutine lungeRoutine;
        private Coroutine hitFlashRoutine;
        private bool hasOriginalColor;

        public float ProximityRadius => proximityRadius;
        public float LungeSpeed => lungeSpeed;
        public float LungeDuration => lungeDuration;
        public float LungeCooldown => lungeCooldown;
        public bool IsAwakened => isAwakened;
        public bool IsLunging => isLunging;
        public Transform PlayerTransform => playerTransform;
        public Light2D AmbushLight => ambushLight;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = 20;
            currentHealth = 20;

            if (rb != null)
                originalGravity = rb.gravityScale;

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                hasOriginalColor = true;
            }

            if (ambushLight == null)
                ambushLight = GetComponentInChildren<Light2D>(true);

            if (playerTransform == null)
                TryResolvePlayer();

            ApplyDormantVisualImmediate();

            if (ambushLight != null)
                ambushLight.enabled = false;
        }

        protected override void Start()
        {
            base.Start();
            if (playerTransform == null)
                TryResolvePlayer();
        }

        protected override void Update()
        {
            if (IsDead) return;
            if (playerTransform == null)
                TryResolvePlayer();

            HandleDebugInput();

            if (!isAwakened)
                CheckProximityWake();
            else
                TryLungeAtPlayer();
        }

        private void HandleDebugInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.lKey.wasPressedThisFrame) OnIlluminated(1f);
            if (kb.kKey.wasPressedThisFrame) OnDarkened();
#else
            if (Input.GetKeyDown(KeyCode.L)) OnIlluminated(1f);
            if (Input.GetKeyDown(KeyCode.K)) OnDarkened();
#endif
        }

        private void TryResolvePlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTransform = go.transform;
        }

        private void CheckProximityWake()
        {
            if (playerTransform == null) return;
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist <= proximityRadius && !HasObstacleBetweenPlayer())
                Wake();
        }

        private bool HasObstacleBetweenPlayer()
        {
            if (playerTransform == null) return false;
            if (obstacleLayer.value == 0) return false;
            Vector2 origin = transform.position;
            Vector2 target = playerTransform.position;
            Vector2 dir = target - origin;
            float dist = dir.magnitude;
            if (dist <= 0.05f) return false;
            RaycastHit2D hit = Physics2D.Raycast(origin, dir.normalized, dist, obstacleLayer);
            return hit.collider != null;
        }

        private bool IsGrounded()
        {
            if (col == null) return true;
            if (groundLayer.value == 0) return true;
            Bounds b = col.bounds;
            Vector2 origin = new Vector2(b.center.x, b.min.y);
            Vector2 size = new Vector2(b.size.x * 0.8f, 0.05f);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);
            return hit.collider != null;
        }

        private bool IsPathClear(Vector2 direction, float distance)
        {
            if (col == null) return true;
            if (obstacleLayer.value == 0) return true;
            Bounds b = col.bounds;
            Vector2 origin = b.center;
            Vector2 size = new Vector2(b.size.x * 0.9f, b.size.y * 0.9f);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, direction, distance, obstacleLayer);
            return hit.collider == null;
        }

        public void OnIlluminated(float intensity)
        {
            if (IsDead) return;
            if (intensity <= 0f) return;
            if (isAwakened) return;
            Wake();
        }

        public void OnDarkened()
        {
            if (IsDead) return;
            if (!isAwakened) return;
            if (isLunging) return;
            if (playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                if (dist <= proximityRadius) return;
            }
            if (dormantRoutine != null) StopCoroutine(dormantRoutine);
            dormantRoutine = StartCoroutine(ReturnToDormantAfterDelay());
        }

        private void Wake()
        {
            if (isAwakened) return;
            isAwakened = true;
            if (dormantRoutine != null) { StopCoroutine(dormantRoutine); dormantRoutine = null; }
            ApplyAwakenedVisual();
            if (ambushLight != null) ambushLight.enabled = true;
        }

        private void Sleep()
        {
            isAwakened = false;
            isLunging = false;
            if (lungeRoutine != null) { StopCoroutine(lungeRoutine); lungeRoutine = null; }
            if (rb != null)
            {
                rb.gravityScale = originalGravity;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            ApplyDormantVisualImmediate();
            if (ambushLight != null) ambushLight.enabled = false;
        }

        private void TryLungeAtPlayer()
        {
            if (playerTransform == null) return;
            if (isLunging) return;
            if (Time.time - lastLungeTime < lungeCooldown) return;
            Vector2 toPlayer = (Vector2)(playerTransform.position - transform.position);
            float dist = toPlayer.magnitude;
            if (dist < 0.2f) return;
            if (dist > proximityRadius + 6f) return;
            Vector2 dir = toPlayer.normalized;
            if (!IsPathClear(dir, Mathf.Min(dist, 1.5f))) return;
            if (!IsGrounded()) return;
            if (lungeRoutine != null) StopCoroutine(lungeRoutine);
            lungeRoutine = StartCoroutine(PerformLunge(dir));
        }

        private IEnumerator PerformLunge(Vector2 direction)
        {
            isLunging = true;
            lastLungeTime = Time.time;
            if (rb != null)
            {
                rb.gravityScale = originalGravity;
                rb.linearVelocity = direction * lungeSpeed;
            }
            if (spriteRenderer != null) FlipToDirection(direction.x);
            yield return new WaitForSeconds(lungeDuration);
            if (rb != null)
            {
                rb.gravityScale = originalGravity;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            isLunging = false;
            lungeRoutine = null;
        }

        private IEnumerator ReturnToDormantAfterDelay()
        {
            yield return new WaitForSeconds(returnToDormantDelay);
            if (IsDead) { dormantRoutine = null; yield break; }
            if (playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                if (dist <= proximityRadius) { dormantRoutine = null; yield break; }
            }
            if (!isLunging) Sleep();
            dormantRoutine = null;
        }

        private void ApplyDormantVisualImmediate()
        {
            if (spriteRenderer == null) return;
            Color c = hasOriginalColor ? originalColor : spriteRenderer.color;
            c.a = dormantAlpha;
            spriteRenderer.color = c;
        }

        private void ApplyAwakenedVisual()
        {
            if (spriteRenderer == null) return;
            Color c = hasOriginalColor ? originalColor : spriteRenderer.color;
            c.a = awakenedAlpha;
            spriteRenderer.color = c;
        }

        private void FlipToDirection(float xDir)
        {
            if (spriteRenderer == null) return;
            if (xDir > 0.05f) spriteRenderer.flipX = false;
            else if (xDir < -0.05f) spriteRenderer.flipX = true;
        }

        public override void TakeDamage(int amount)
        {
            if (IsDead) return;
            base.TakeDamage(amount);
            if (IsDead) return;
            if (spriteRenderer != null)
            {
                if (hitFlashRoutine != null) StopCoroutine(hitFlashRoutine);
                hitFlashRoutine = StartCoroutine(DamageFlashRoutine());
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (spriteRenderer == null) { hitFlashRoutine = null; yield break; }
            Color current = spriteRenderer.color;
            Color flash = Color.white; flash.a = current.a;
            spriteRenderer.color = flash;
            yield return new WaitForSeconds(0.08f);
            if (spriteRenderer != null && !IsDead)
            {
                float targetAlpha = isAwakened ? awakenedAlpha : dormantAlpha;
                Color restored = hasOriginalColor ? originalColor : Color.white;
                restored.a = targetAlpha;
                spriteRenderer.color = restored;
            }
            hitFlashRoutine = null;
        }

        public override void Die()
        {
            if (dormantRoutine != null) { StopCoroutine(dormantRoutine); dormantRoutine = null; }
            if (lungeRoutine != null) { StopCoroutine(lungeRoutine); lungeRoutine = null; }
            if (hitFlashRoutine != null) { StopCoroutine(hitFlashRoutine); hitFlashRoutine = null; }
            if (rb != null) rb.gravityScale = originalGravity;
            base.Die();
        }

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead) return;
            if (collision == null || collision.gameObject == null) return;
            var d = collision.gameObject.GetComponent<IDamageable>();
            if (d == null) d = collision.gameObject.GetComponentInParent<IDamageable>();
            if (d != null) DealDamageToPlayer(d, ContactDamage);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsDead) return;
            if (other == null) return;
            var d = other.GetComponent<IDamageable>();
            if (d == null) d = other.GetComponentInParent<IDamageable>();
            if (d != null) DealDamageToPlayer(d, ContactDamage);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isAwakened ? new Color(1f, 0.3f, 0.2f, 0.35f) : new Color(0.3f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
        }
#endif
    }
}
