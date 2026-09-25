using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.Rendering.Universal;
using CaveDweller.Lighting;

namespace CaveDweller.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float speed = 22f;
        [SerializeField] private float lifetime = 0.5f;
        [SerializeField] private int damage = 20;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float skinWidth = 0.05f;

        [Header("Tracer Light (Optional)")]
        [SerializeField] private Light2D tracerLight;
        [SerializeField] private float illuminatedIntensity = 1.5f;

        [Header("Impact (Optional References)")]
        [SerializeField] private GameObject impactEffectPrefab;

        [Header("Manual Detonate Input (Debug)")]
        [SerializeField] private bool allowManualDetonate = false;

        private Rigidbody2D rb;
        private BoxCollider2D boxCol;
        private Vector2 travelDirection = Vector2.right;
        private GameObject owner;
        private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
        private bool hasHit;
        private bool isLaunched;
        private bool isDespawning;
        private Coroutine lifetimeRoutine;
        private float originalGravityScale;
        private float originalLightIntensity;
        private bool originalTriggerState;

        public float Speed => speed;
        public float Lifetime => lifetime;
        public int Damage => damage;
        public Vector2 TravelDirection => travelDirection;
        public bool HasHit => hasHit;
        public bool IsLaunched => isLaunched;
        public Light2D TracerLight => tracerLight;
        public GameObject ImpactEffectPrefab => impactEffectPrefab;

        public void Configure(float newSpeed, int newDamage, float newLifetime, LayerMask newHitMask)
        {
            speed = newSpeed;
            damage = newDamage;
            lifetime = newLifetime;
            hitMask = newHitMask;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            boxCol = GetComponent<BoxCollider2D>();

            if (tracerLight == null)
            {
                tracerLight = GetComponent<Light2D>();
            }

            if (rb != null)
            {
                originalGravityScale = rb.gravityScale;
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (boxCol != null)
            {
                originalTriggerState = boxCol.isTrigger;
                boxCol.isTrigger = true;
            }

            if (tracerLight != null)
            {
                originalLightIntensity = tracerLight.intensity;
            }
        }

        private void OnDisable()
        {
            StopLifetime();
            RestoreOwnerCollisions();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = originalGravityScale;
            }

            if (boxCol != null)
            {
                boxCol.isTrigger = originalTriggerState;
            }

            if (tracerLight != null)
            {
                tracerLight.intensity = originalLightIntensity;
            }

            owner = null;
            isLaunched = false;
            hasHit = false;
            isDespawning = false;
        }

        private void Update()
        {
            if (!allowManualDetonate || hasHit || !isLaunched)
            {
                return;
            }

            bool detonatePressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.xKey.wasPressedThisFrame)
            {
                detonatePressed = true;
            }

            var mouse = Mouse.current;
            if (!detonatePressed && mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                detonatePressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.X) || Input.GetMouseButtonDown(1))
            {
                detonatePressed = true;
            }
#endif

            if (detonatePressed)
            {
                Detonate();
            }
        }

        private void FixedUpdate()
        {
            if (!isLaunched || hasHit)
            {
                return;
            }

            if (rb == null || boxCol == null)
            {
                return;
            }

            rb.linearVelocity = travelDirection * speed;

            Vector2 size = new Vector2(
                Mathf.Max(boxCol.bounds.size.x, 0.01f),
                Mathf.Max(boxCol.bounds.size.y, 0.01f));
            float distance = speed * Time.fixedDeltaTime + skinWidth;

            RaycastHit2D hit = Physics2D.BoxCast(
                (Vector2)boxCol.bounds.center,
                size,
                0f,
                travelDirection,
                distance,
                hitMask);

            // Illuminate any ILightDetectable objects along the flight path
            if (tracerLight != null && tracerLight.enabled)
            {
                float lightRadius = tracerLight.pointLightOuterRadius > 0f ? tracerLight.pointLightOuterRadius : 2.5f;
                Collider2D[] illuminated = Physics2D.OverlapCircleAll(transform.position, lightRadius);
                for (int i = 0; i < illuminated.Length; i++)
                {
                    Collider2D col = illuminated[i];
                    if (col != null && !IsOwnerCollider(col))
                    {
                        ILightDetectable detectable = col.GetComponent<ILightDetectable>();
                        if (detectable == null && col.attachedRigidbody != null)
                        {
                            detectable = col.attachedRigidbody.GetComponent<ILightDetectable>();
                        }
                        if (detectable != null)
                        {
                            detectable.OnIlluminated(illuminatedIntensity);
                        }
                    }
                }
            }

            if (hit.collider != null && hit.collider.gameObject != gameObject && hit.collider != boxCol)
            {
                HandleHit(hit.collider);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isLaunched || hasHit)
            {
                return;
            }

            if (other == null || other.gameObject == gameObject)
            {
                return;
            }

            if (!IsInHitMask(other.gameObject))
            {
                return;
            }

            HandleHit(other);
        }

        public void Launch(Vector2 direction)
        {
            Launch(direction, null);
        }

        public void Launch(Vector2 direction, GameObject launchOwner)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            travelDirection = direction.normalized;
            hasHit = false;
            isLaunched = true;
            isDespawning = false;

            float angle = Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = travelDirection * speed;
            }

            if (boxCol != null)
            {
                boxCol.isTrigger = true;
            }

            SetOwner(launchOwner);

            StopLifetime();
            lifetimeRoutine = StartCoroutine(LifetimeRoutine());
        }

        public void SetOwner(GameObject launchOwner)
        {
            RestoreOwnerCollisions();
            owner = launchOwner;

            if (owner == null || boxCol == null)
            {
                return;
            }

            Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                Collider2D ownerCollider = ownerColliders[i];
                if (ownerCollider == null || ownerCollider == boxCol)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(boxCol, ownerCollider, true);
                ignoredOwnerColliders.Add(ownerCollider);
            }
        }

        public void Detonate()
        {
            if (!isLaunched || hasHit)
            {
                return;
            }

            hasHit = true;
            SpawnImpactEffect();
            BeginDespawn();
        }

        private void HandleHit(Collider2D other)
        {
            if (hasHit || other == null)
            {
                return;
            }

            if (IsOwnerCollider(other))
            {
                return;
            }

            hasHit = true;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null && other.attachedRigidbody != null)
            {
                damageable = other.attachedRigidbody.GetComponent<IDamageable>();
            }

            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage);
            }

            ILightDetectable detectable = other.GetComponent<ILightDetectable>();
            if (detectable != null)
            {
                detectable.OnIlluminated(illuminatedIntensity);
            }

            SpawnImpactEffect();
            BeginDespawn();
        }

        private void SpawnImpactEffect()
        {
            if (impactEffectPrefab == null)
            {
                return;
            }

            Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
        }

        private bool IsInHitMask(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            int layerBit = 1 << target.layer;
            return (hitMask.value & layerBit) != 0;
        }

        private bool IsOwnerCollider(Collider2D other)
        {
            if (other == null || owner == null)
            {
                return false;
            }

            if (other.gameObject == owner)
            {
                return true;
            }

            return other.transform.IsChildOf(owner.transform);
        }

        private void BeginDespawn()
        {
            if (isDespawning)
            {
                return;
            }

            isDespawning = true;
            StopLifetime();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            StartCoroutine(DespawnRoutine());
        }

        private void StopLifetime()
        {
            if (lifetimeRoutine != null)
            {
                StopCoroutine(lifetimeRoutine);
                lifetimeRoutine = null;
            }
        }

        private void RestoreOwnerCollisions()
        {
            if (boxCol != null)
            {
                for (int i = 0; i < ignoredOwnerColliders.Count; i++)
                {
                    Collider2D ownerCollider = ignoredOwnerColliders[i];
                    if (ownerCollider != null)
                    {
                        Physics2D.IgnoreCollision(boxCol, ownerCollider, false);
                    }
                }
            }

            ignoredOwnerColliders.Clear();
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(lifetime, 0.01f));

            lifetimeRoutine = null;

            if (!hasHit)
            {
                hasHit = true;
                SpawnImpactEffect();
                BeginDespawn();
            }
        }

        private IEnumerator DespawnRoutine()
        {
            if (boxCol != null) boxCol.enabled = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            var sr = GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            float fadeDuration = 0.12f;
            float startIntensity = tracerLight != null ? tracerLight.intensity : 2.5f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (tracerLight != null)
                {
                    tracerLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
                }

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            RestoreOwnerCollisions();

            Destroy(gameObject);
        }
    }
}
