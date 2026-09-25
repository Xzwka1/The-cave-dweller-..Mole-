using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CaveDweller.Combat;

namespace CaveDweller.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PatrolEnemyWithLight : BaseEnemy
    {
        public enum EnemyState
        {
            Patrol,
            Chase,
            Attack
        }

        [Header("Patrol Settings")]
        [SerializeField] private Transform patrolPointA;
        [SerializeField] private Transform patrolPointB;
        [SerializeField] private float patrolSpeed = 2f;

        [Header("Vision / Flashlight Settings")]
        [SerializeField] private Light2D spotLight;
        [SerializeField] private float viewDistance = 8f;
        [SerializeField] private float viewAngle = 60f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Chase / Attack Settings")]
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackCooldown = 1f;

        [Header("Physics Check Settings")]
        [SerializeField] private float wallCheckDistance = 0.3f;

        [Header("Debug State")]
        [SerializeField] private EnemyState currentState = EnemyState.Patrol;

        private Transform playerTransform;
        private IDamageable playerDamageable;
        private Vector3 patrolTarget;
        private float facingDirection = 1f;
        private float lastAttackTime = -999f;
        private bool playerVisible;
        private Color originalSpriteColor = Color.white;
        private bool hasOriginalColor;
        private Coroutine hitFlashRoutine;
        private bool debugLightForcedOff;

        public Transform PatrolPointA => patrolPointA;
        public Transform PatrolPointB => patrolPointB;
        public float PatrolSpeed => patrolSpeed;
        public Light2D SpotLight => spotLight;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public LayerMask PlayerLayer => playerLayer;
        public LayerMask ObstacleLayer => obstacleLayer;
        public float ChaseSpeed => chaseSpeed;
        public float AttackRange => attackRange;
        public int AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public EnemyState CurrentState => currentState;
        public bool PlayerVisible => playerVisible;
        public float FacingDirection => facingDirection;

        protected override void Awake()
        {
            maxHealth = 140;
            base.Awake();

            if (spriteRenderer != null)
            {
                originalSpriteColor = spriteRenderer.color;
                hasOriginalColor = true;
            }

            if (spotLight == null)
            {
                spotLight = GetComponent<Light2D>();
            }
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light2D>();
            }

            if (patrolPointA != null)
            {
                patrolTarget = patrolPointA.position;
            }
            else if (patrolPointB != null)
            {
                patrolTarget = patrolPointB.position;
            }
            else
            {
                patrolTarget = transform.position;
            }
        }

        protected override void Start()
        {
            base.Start();
            ResolvePlayer();
        }

        protected override void Update()
        {
            if (IsDead) return;

            ResolvePlayer();
            HandleDebugInput();

            bool playerDead = playerDamageable != null && playerDamageable.IsDead;
            playerVisible = !playerDead && CheckPlayerVisible();

            if (playerDead && currentState != EnemyState.Patrol)
            {
                currentState = EnemyState.Patrol;
            }

            switch (currentState)
            {
                case EnemyState.Patrol:
                    PatrolTick();
                    if (playerVisible)
                    {
                        currentState = EnemyState.Chase;
                    }
                    break;
                case EnemyState.Chase:
                    ChaseTick();
                    if (!playerVisible)
                    {
                        currentState = EnemyState.Patrol;
                    }
                    else if (IsPlayerInAttackRange())
                    {
                        currentState = EnemyState.Attack;
                    }
                    break;
                case EnemyState.Attack:
                    AttackTick();
                    if (!IsPlayerInAttackRange())
                    {
                        currentState = playerVisible ? EnemyState.Chase : EnemyState.Patrol;
                    }
                    break;
            }

            UpdateFlashlightAim();
        }

        private void FixedUpdate()
        {
            if (IsDead) return;
            if (rb == null) return;

            float horizontalSpeed = 0f;
            switch (currentState)
            {
                case EnemyState.Patrol:
                    horizontalSpeed = Mathf.Sign(patrolTarget.x - transform.position.x) * patrolSpeed;
                    if (Mathf.Abs(patrolTarget.x - transform.position.x) < 0.15f)
                    {
                        horizontalSpeed = 0f;
                    }
                    break;
                case EnemyState.Chase:
                    if (playerTransform != null)
                    {
                        horizontalSpeed = Mathf.Sign(playerTransform.position.x - transform.position.x) * chaseSpeed;
                        if (Mathf.Abs(playerTransform.position.x - transform.position.x) < 0.05f)
                        {
                            horizontalSpeed = 0f;
                        }
                    }
                    break;
                case EnemyState.Attack:
                    horizontalSpeed = 0f;
                    break;
            }

            if (Mathf.Abs(horizontalSpeed) > 0.01f && CheckWallAhead(Mathf.Sign(horizontalSpeed), wallCheckDistance))
            {
                horizontalSpeed = 0f;
                if (currentState == EnemyState.Patrol)
                {
                    SwapPatrolTarget();
                }
            }

            rb.linearVelocity = new Vector2(horizontalSpeed, rb.linearVelocity.y);

            if (horizontalSpeed > 0.05f)
            {
                SetFacing(1f);
            }
            else if (horizontalSpeed < -0.05f)
            {
                SetFacing(-1f);
            }
        }

        private void PatrolTick()
        {
            if (patrolPointA == null && patrolPointB == null) return;
            if (patrolPointA == null || patrolPointB == null)
            {
                Transform single = patrolPointA != null ? patrolPointA : patrolPointB;
                if (single != null)
                {
                    patrolTarget = single.position;
                }
                return;
            }

            if (Mathf.Abs(transform.position.x - patrolTarget.x) < 0.2f)
            {
                SwapPatrolTarget();
            }
        }

        private void ChaseTick()
        {
            if (playerTransform == null) return;
            float dirToPlayer = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(dirToPlayer) > 0.05f)
            {
                SetFacing(Mathf.Sign(dirToPlayer));
            }
        }

        private void AttackTick()
        {
            if (playerTransform != null)
            {
                float dirToPlayer = playerTransform.position.x - transform.position.x;
                if (Mathf.Abs(dirToPlayer) > 0.05f)
                {
                    SetFacing(Mathf.Sign(dirToPlayer));
                }
            }

            if (Time.time - lastAttackTime < attackCooldown) return;
            if (!IsPlayerInAttackRange()) return;

            lastAttackTime = Time.time;
            if (playerDamageable != null)
            {
                DealDamageToPlayer(playerDamageable, attackDamage);
            }
            else if (playerTransform != null)
            {
                DealDamageToPlayer(playerTransform.gameObject, attackDamage);
            }
        }

        private bool IsPlayerInAttackRange()
        {
            if (playerTransform == null) return false;
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            return distance <= attackRange;
        }

        private bool CheckPlayerVisible()
        {
            if (playerTransform == null) return false;

            Vector2 origin = GetEyePosition();
            Vector2 toPlayer = (Vector2)playerTransform.position - origin;
            float distance = toPlayer.magnitude;
            if (distance > viewDistance) return false;
            if (distance < 0.001f) return true;

            Vector2 facing = GetFacingVector();
            float angleToPlayer = Vector2.Angle(facing, toPlayer.normalized);
            if (angleToPlayer > viewAngle * 0.5f) return false;

            int combinedMask = playerLayer.value | obstacleLayer.value;
            if (combinedMask == 0)
            {
                return true;
            }

            RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distance + 0.1f, combinedMask);
            if (hit.collider == null) return false;

            if (((1 << hit.collider.gameObject.layer) & playerLayer.value) != 0)
            {
                return true;
            }

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable == playerDamageable)
            {
                return true;
            }

            return false;
        }

        private Vector2 GetEyePosition()
        {
            if (col != null)
            {
                return (Vector2)col.bounds.center;
            }
            return (Vector2)transform.position;
        }

        private Vector2 GetFacingVector()
        {
            return new Vector2(facingDirection, 0f);
        }

        private void UpdateFlashlightAim()
        {
            if (spotLight == null) return;
            if (debugLightForcedOff)
            {
                if (spotLight.enabled)
                {
                    spotLight.enabled = false;
                }
                return;
            }
            if (!spotLight.enabled)
            {
                spotLight.enabled = true;
            }

            Vector2 aimDir = GetFacingVector();
            if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
            {
                if (playerTransform != null)
                {
                    Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)spotLight.transform.position;
                    if (toPlayer.sqrMagnitude > 0.0001f)
                    {
                        aimDir = toPlayer.normalized;
                    }
                }
            }

            if (spotLight.transform != transform)
            {
                float aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                spotLight.transform.rotation = Quaternion.Euler(0f, 0f, aimAngle - 90f);
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingDirection < 0f;
            }
        }

        private void SetFacing(float direction)
        {
            if (direction > 0f)
            {
                facingDirection = 1f;
            }
            else if (direction < 0f)
            {
                facingDirection = -1f;
            }
        }

        private void SwapPatrolTarget()
        {
            if (patrolPointA == null || patrolPointB == null) return;
            float distToA = Mathf.Abs(transform.position.x - patrolPointA.position.x);
            float distToB = Mathf.Abs(transform.position.x - patrolPointB.position.x);
            patrolTarget = distToA < distToB ? patrolPointB.position : patrolPointA.position;
        }

        private bool CheckWallAhead(float direction)
        {
            if (col == null) return false;
            Bounds bounds = col.bounds;
            Vector2 origin = new Vector2(
                bounds.center.x + direction * (bounds.extents.x + 0.02f),
                bounds.center.y);
            Vector2 size = new Vector2(0.05f, bounds.size.y * 0.8f);
            LayerMask mask = groundLayer.value != 0 ? groundLayer : obstacleLayer;
            if (mask.value == 0) return false;
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, new Vector2(direction, 0f), wallCheckDistance, mask);
            return hit.collider != null;
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null && playerDamageable != null && !playerDamageable.IsDead)
            {
                return;
            }

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;

            playerTransform = playerObj.transform;
            playerDamageable = playerObj.GetComponent<IDamageable>();
            if (playerDamageable == null)
            {
                playerDamageable = playerObj.GetComponentInParent<IDamageable>();
            }
            if (playerDamageable == null)
            {
                playerDamageable = playerObj.GetComponentInChildren<IDamageable>();
            }
        }

        public override void TakeDamage(int amount)
        {
            if (IsDead) return;
            base.TakeDamage(amount);
            if (IsDead) return;

            ResolvePlayer();
            if (playerTransform != null)
            {
                currentState = EnemyState.Chase;
            }

            if (spriteRenderer != null)
            {
                if (hitFlashRoutine != null)
                {
                    StopCoroutine(hitFlashRoutine);
                }
                hitFlashRoutine = StartCoroutine(HitFlashRoutine());
            }
        }

        private IEnumerator HitFlashRoutine()
        {
            Color restoreColor = hasOriginalColor ? originalSpriteColor : Color.white;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = restoreColor;
            }
            hitFlashRoutine = null;
        }

        private void HandleDebugInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.lKey.wasPressedThisFrame)
            {
                debugLightForcedOff = !debugLightForcedOff;
            }
#else
            if (Input.GetKeyDown(KeyCode.L))
            {
                debugLightForcedOff = !debugLightForcedOff;
            }
#endif
        }

        public void SetPatrolPoints(Transform pointA, Transform pointB)
        {
            patrolPointA = pointA;
            patrolPointB = pointB;
            if (pointA != null)
            {
                patrolTarget = pointA.position;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
