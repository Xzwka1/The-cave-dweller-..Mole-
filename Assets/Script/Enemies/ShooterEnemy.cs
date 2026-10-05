using System.Collections;
using UnityEngine;
using CaveDweller.Combat;

namespace CaveDweller.Enemies
{
    /// <summary>
    /// Stationary shooter enemy (มอนสเตอร์ยืนนิ่งยิงไกล).
    /// Stands in place, faces player with deadzone filtering, and fires projectiles with animation synchronization.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class ShooterEnemy : BaseEnemy
    {
        [Header("Stationary Shooter Settings")]
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float shootCooldown = 2.2f;
        [SerializeField] private int shootDamage = 15;
        [SerializeField] private float bulletSpeed = 10f;
        [SerializeField] private float bulletLifetime = 3f;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private LayerMask playerLayer;

        [Header("Visual & Facing Settings")]
        [Tooltip("Monster sprite faces left natively in PNG source")]
        [SerializeField] private bool spriteFacesLeftByDefault = true;
        [SerializeField] private float turnDeadzone = 0.4f;

        private Transform playerTransform;
        private IDamageable playerDamageable;
        private float lastShootTime = -999f;
        private float facingDirection = -1f;
        private bool isShooting = false;
        private Coroutine shootRoutine;

        public float DetectionRange => detectionRange;
        public float ShootCooldown => shootCooldown;
        public int ShootDamage => shootDamage;
        public bool PlayerInRange => !IsDead && CheckPlayerVisible();

        protected override void Awake()
        {
            base.Awake();

            if (rb != null)
            {
                rb.freezeRotation = true;
                rb.gravityScale = 1f;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            // Remove/disable any child light if present
            var childLight = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            if (childLight != null)
            {
                childLight.enabled = false;
            }

            // Fallback layer masks
            if (obstacleLayer.value == 0) obstacleLayer = LayerMask.GetMask("Default");
            if (playerLayer.value == 0) playerLayer = LayerMask.GetMask("Player");

            EnsureBulletPrefabLoaded();
            UpdateFacingVisual();
        }

        protected override void Start()
        {
            base.Start();
            ResolvePlayer();
            EnsureBulletPrefabLoaded();
        }

        private void EnsureBulletPrefabLoaded()
        {
            if (bulletPrefab == null)
            {
#if UNITY_EDITOR
                bulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyStoneProjectile.prefab")
                            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BulletTracer.prefab");
#endif
                if (bulletPrefab == null)
                {
                    bulletPrefab = Resources.Load<GameObject>("Prefabs/EnemyStoneProjectile")
                                ?? Resources.Load<GameObject>("Prefabs/BulletTracer");
                }
            }
        }

        protected override void Update()
        {
            if (IsDead) return;

            ResolvePlayer();

            // Enforce idle animation on stationary shooter
            if (cachedAnimator != null)
            {
                cachedAnimator.SetBool(animIsWalkingHash, false);
                cachedAnimator.SetFloat(animSpeedHash, 0f);
            }

            bool playerDead = playerDamageable != null && playerDamageable.IsDead;
            bool canSeePlayer = !playerDead && CheckPlayerVisible();

            if (canSeePlayer && playerTransform != null)
            {
                // Face player cleanly with deadzone to prevent rapid jitter
                float dirX = playerTransform.position.x - transform.position.x;
                if (Mathf.Abs(dirX) > turnDeadzone)
                {
                    SetFacing(Mathf.Sign(dirX));
                }

                // Shoot when ready
                if (!isShooting && Time.time - lastShootTime >= shootCooldown)
                {
                    shootRoutine = StartCoroutine(PerformShoot());
                }
            }
        }

        private void FixedUpdate()
        {
            if (IsDead) return;

            // Strictly stationary: zero horizontal velocity, gravity keeps it planted on the ground
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private bool CheckPlayerVisible()
        {
            if (playerTransform == null) return false;

            Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)col.bounds.center;
            Vector2 toPlayer = (Vector2)playerTransform.position - origin;
            float distance = toPlayer.magnitude;

            if (distance > detectionRange) return false;

            int mask = obstacleLayer.value;
            if (mask == 0) mask = LayerMask.GetMask("Default");

            RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distance, mask);
            return hit.collider == null;
        }

        private IEnumerator PerformShoot()
        {
            isShooting = true;

            // Trigger shoot animation
            if (cachedAnimator != null)
            {
                cachedAnimator.SetTrigger(animShootTriggerHash);
            }

            // Sync with recoil frame in EnemyShoot anim (~0.3s)
            yield return new WaitForSeconds(0.3f);

            if (IsDead || playerTransform == null)
            {
                isShooting = false;
                yield break;
            }

            // Calculate projectile spawn position
            Vector2 spawnPos = firePoint != null
                ? (Vector2)firePoint.position
                : (Vector2)col.bounds.center + new Vector2(facingDirection * 0.65f, 0.1f);

            Vector2 targetPos = playerTransform.position;
            Vector2 shootDir = (targetPos - spawnPos).normalized;

            EnsureBulletPrefabLoaded();

            if (bulletPrefab != null)
            {
                GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                Projectile projectile = bulletObj.GetComponent<Projectile>();
                if (projectile != null)
                {
                    int hitMask = LayerMask.GetMask("Player", "Default");
                    projectile.Configure(bulletSpeed, shootDamage, bulletLifetime, hitMask);
                    projectile.Launch(shootDir, gameObject);
                }
                CaveDweller.Core.SoundManager.Instance?.PlayMonsterRangedSFX();
            }

            // Wait for shoot follow-through (~0.25s)
            yield return new WaitForSeconds(0.25f);

            lastShootTime = Time.time;
            isShooting = false;
            shootRoutine = null;
        }

        private void SetFacing(float direction)
        {
            if (direction > 0.05f)
            {
                facingDirection = 1f;
            }
            else if (direction < -0.05f)
            {
                facingDirection = -1f;
            }
            UpdateFacingVisual();
        }

        private void UpdateFacingVisual()
        {
            if (spriteRenderer != null)
            {
                // Native sprite faces LEFT.
                // When facing right (+1), flipX = true (flips left sprite to face right).
                // When facing left (-1), flipX = false (keeps native left-facing sprite).
                spriteRenderer.flipX = spriteFacesLeftByDefault ? (facingDirection > 0f) : (facingDirection < 0f);
            }
        }

        public override void DealDamageToPlayer(IDamageable target, int amount)
        {
            if (target == null || target.IsDead) return;
            // Stationary shooter does not play melee bite animation
            target.TakeDamage(amount);
        }

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead || collision == null || collision.gameObject == null) return;
            if (!collision.gameObject.CompareTag("Player")) return;

            // Direct contact damage without triggering melee bite animation
            IDamageable damageable = ResolveDamageable(collision.gameObject);
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(ContactDamage);
            }
        }

        private void ResolvePlayer()
        {
            if (playerTransform == null)
            {
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                {
                    playerTransform = playerGo.transform;
                    playerDamageable = playerGo.GetComponent<IDamageable>();
                }
            }
        }

        public override void Die()
        {
            if (shootRoutine != null)
            {
                StopCoroutine(shootRoutine);
                shootRoutine = null;
            }
            isShooting = false;
            base.Die();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
#endif
    }

    /// <summary>
    /// Legacy alias for ShooterEnemy to avoid breaking serialized references.
    /// </summary>
    [System.Obsolete("Use ShooterEnemy instead")]
    public class AmbushEnemyDarkness : ShooterEnemy { }
}
