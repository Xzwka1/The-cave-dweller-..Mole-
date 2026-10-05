using System.Collections;
using UnityEngine;
using CaveDweller.Combat;

namespace CaveDweller.Enemies
{
    /// <summary>
    /// Walking melee enemy (มอนสเตอร์เดินลาดตระเวนตีใกล้).
    /// Features:
    /// - Autonomous patrol when no patrol points are assigned (wall & ledge detection).
    /// - Smooth turnaround with idle pause (no robotic instant snap).
    /// - Deadzone facing to eliminate rapid left/right twitching.
    /// - Sprite orientation fix (source PNGs face left natively).
    /// - Timed attack sequence synced with attack animation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class MeleeEnemy : BaseEnemy
    {
        public enum EnemyState
        {
            Patrol,
            PatrolIdle,
            Chase,
            Attack
        }

        [Header("Patrol Settings")]
        [Tooltip("Optional patrol bounds. If left empty, the enemy patrols autonomously.")]
        [SerializeField] private Transform patrolPointA;
        [SerializeField] private Transform patrolPointB;
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float patrolIdleMin = 1.0f;
        [SerializeField] private float patrolIdleMax = 2.0f;

        [Header("Vision Settings")]
        [SerializeField] private float viewDistance = 7.5f;
        [SerializeField] private float viewAngle = 90f;
        [SerializeField] private float proximityDetectDistance = 2.0f;
        [SerializeField] private float losePlayerGraceTime = 1.5f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Chase / Attack Settings")]
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float attackCooldown = 1.0f;
        [SerializeField] private bool stopAtLedgeDuringChase = true;

        [Header("Physics & Ledge Check Settings")]
        [SerializeField] private float wallCheckDistance = 0.4f;
        [SerializeField] private float ledgeCheckForwardOffset = 0.45f;
        [SerializeField] private float ledgeCheckDownDistance = 0.7f;
        [SerializeField] private float turnDeadzone = 0.4f;

        [Header("Visual Settings")]
        [Tooltip("Monster sprite faces left natively in PNG source")]
        [SerializeField] private bool spriteFacesLeftByDefault = true;

        [Header("Debug State")]
        [SerializeField] private EnemyState currentState = EnemyState.Patrol;

        private Transform playerTransform;
        private Collider2D playerCollider;
        private IDamageable playerDamageable;
        private Vector3 patrolTarget;
        private float facingDirection = 1f;
        private float lastAttackTime = -999f;
        private float idleTimer = 0f;
        private float lostPlayerTimer = 0f;
        private bool isAttacking = false;
        private Coroutine attackRoutine;

        public Transform PatrolPointA => patrolPointA;
        public Transform PatrolPointB => patrolPointB;
        public float PatrolSpeed => patrolSpeed;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public LayerMask PlayerLayer => playerLayer;
        public LayerMask ObstacleLayer => obstacleLayer;
        public float ChaseSpeed => chaseSpeed;
        public float AttackRange => attackRange;
        public int AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public EnemyState CurrentState => currentState;
        public float FacingDirection => facingDirection;

        protected override void Awake()
        {
            base.Awake();

            // Disable any legacy light if present
            var childLight = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            if (childLight != null)
            {
                childLight.enabled = false;
            }

            // Fallback layer masks if left unassigned
            if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Default");
            if (obstacleLayer.value == 0) obstacleLayer = LayerMask.GetMask("Default");
            if (playerLayer.value == 0) playerLayer = LayerMask.GetMask("Player");

            // Initial patrol target setup
            if (patrolPointA != null)
            {
                patrolTarget = patrolPointA.position;
                facingDirection = Mathf.Sign(patrolTarget.x - transform.position.x);
                if (facingDirection == 0f) facingDirection = 1f;
            }
            else if (patrolPointB != null)
            {
                patrolTarget = patrolPointB.position;
                facingDirection = Mathf.Sign(patrolTarget.x - transform.position.x);
                if (facingDirection == 0f) facingDirection = 1f;
            }
            else
            {
                // Autonomous mode: start walking right
                facingDirection = 1f;
            }

            UpdateFacingVisual();
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

            bool playerDead = playerDamageable != null && playerDamageable.IsDead;
            bool canSeePlayer = !playerDead && CheckPlayerVisible();

            // State management
            if (isAttacking)
            {
                // Attacking coroutine controls movement and facing
                return;
            }

            switch (currentState)
            {
                case EnemyState.Patrol:
                    if (canSeePlayer)
                    {
                        currentState = EnemyState.Chase;
                        lostPlayerTimer = 0f;
                        break;
                    }
                    PatrolUpdate();
                    break;

                case EnemyState.PatrolIdle:
                    if (canSeePlayer)
                    {
                        currentState = EnemyState.Chase;
                        lostPlayerTimer = 0f;
                        break;
                    }
                    idleTimer -= Time.deltaTime;
                    if (idleTimer <= 0f)
                    {
                        // Turn around and resume patrol
                        SetFacing(-facingDirection);
                        currentState = EnemyState.Patrol;
                    }
                    break;

                case EnemyState.Chase:
                    if (canSeePlayer)
                    {
                        lostPlayerTimer = 0f;
                    }
                    else
                    {
                        lostPlayerTimer += Time.deltaTime;
                        if (lostPlayerTimer >= losePlayerGraceTime)
                        {
                            EnterPatrolIdle();
                            break;
                        }
                    }

                    if (IsPlayerInAttackRange())
                    {
                        if (Time.time - lastAttackTime >= attackCooldown)
                        {
                            StartAttack();
                        }
                        else
                        {
                            currentState = EnemyState.Attack;
                        }
                    }
                    else
                    {
                        ChaseUpdate();
                    }
                    break;

                case EnemyState.Attack:
                    if (playerTransform == null || playerDead)
                    {
                        EnterPatrolIdle();
                        break;
                    }

                    if (!IsPlayerInAttackRange())
                    {
                        currentState = EnemyState.Chase;
                        break;
                    }

                    // Face player while in attack range
                    float dirToP = playerTransform.position.x - transform.position.x;
                    if (Mathf.Abs(dirToP) > turnDeadzone)
                    {
                        SetFacing(Mathf.Sign(dirToP));
                    }

                    // Continuous attack loop: when cooldown passes, bite again
                    if (!isAttacking && Time.time - lastAttackTime >= attackCooldown)
                    {
                        StartAttack();
                    }
                    break;
            }

            UpdateFacingVisual();
        }

        private void FixedUpdate()
        {
            if (IsDead || rb == null) return;

            if (isAttacking || currentState == EnemyState.PatrolIdle || currentState == EnemyState.Attack)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                UpdateAnimatorMovement(0f);
                return;
            }

            float targetSpeed = 0f;

            if (currentState == EnemyState.Patrol)
            {
                // In waypoint patrol mode
                if (HasPatrolPoints())
                {
                    float distToTarget = patrolTarget.x - transform.position.x;
                    if (Mathf.Abs(distToTarget) < 0.25f)
                    {
                        EnterPatrolIdle();
                        return;
                    }
                    SetFacing(Mathf.Sign(distToTarget));
                }

                // Check wall or ledge ahead
                if (CheckWallAhead() || CheckLedgeAhead())
                {
                    EnterPatrolIdle();
                    return;
                }

                targetSpeed = facingDirection * patrolSpeed;
            }
            else if (currentState == EnemyState.Chase)
            {
                if (playerTransform != null)
                {
                    float dirToPlayer = playerTransform.position.x - transform.position.x;
                    if (Mathf.Abs(dirToPlayer) > turnDeadzone)
                    {
                        SetFacing(Mathf.Sign(dirToPlayer));
                    }

                    // Check obstacle or ledge during chase
                    if (CheckWallAhead() || (stopAtLedgeDuringChase && CheckLedgeAhead()))
                    {
                        targetSpeed = 0f;
                    }
                    else
                    {
                        targetSpeed = facingDirection * chaseSpeed;
                    }
                }
            }

            rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
            UpdateAnimatorMovement(Mathf.Abs(targetSpeed));
        }

        private void UpdateAnimatorMovement(float speed)
        {
            if (cachedAnimator == null) return;
            cachedAnimator.SetFloat(animSpeedHash, speed);
            cachedAnimator.SetBool(animIsWalkingHash, speed > 0.05f);
        }

        private void PatrolUpdate()
        {
            // Waypoint check
            if (HasPatrolPoints())
            {
                if (Mathf.Abs(transform.position.x - patrolTarget.x) < 0.25f)
                {
                    EnterPatrolIdle();
                }
            }
        }

        private void ChaseUpdate()
        {
            if (playerTransform == null) return;

            float dirX = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(dirX) > turnDeadzone)
            {
                SetFacing(Mathf.Sign(dirX));
            }
        }

        private void EnterPatrolIdle()
        {
            currentState = EnemyState.PatrolIdle;
            idleTimer = Random.Range(patrolIdleMin, patrolIdleMax);

            if (HasPatrolPoints())
            {
                // Switch to next waypoint target
                SwapPatrolTarget();
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            UpdateAnimatorMovement(0f);
        }

        private void StartAttack()
        {
            if (isAttacking) return;
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(AttackSequence());
        }

        private IEnumerator AttackSequence()
        {
            isAttacking = true;
            currentState = EnemyState.Attack;

            // Stop moving
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            UpdateAnimatorMovement(0f);

            // Face player before starting swing
            if (playerTransform != null)
            {
                float dirX = playerTransform.position.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.1f)
                {
                    SetFacing(Mathf.Sign(dirX));
                }
            }

            // Trigger attack in animator
            if (cachedAnimator != null)
            {
                cachedAnimator.SetBool(animIsAttackingHash, true);
                cachedAnimator.SetTrigger(animAttackTriggerHash);
            }

            // Wait for swing impact frame (~0.28s in EnemyAttack anim)
            yield return new WaitForSeconds(0.28f);

            if (IsDead)
            {
                isAttacking = false;
                yield break;
            }

            // Deal damage if player is still in range (with a generous hit margin)
            if (playerTransform != null && IsPlayerInDamageRange())
            {
                IDamageable target = playerDamageable ?? ResolveDamageable(playerTransform.gameObject);
                if (target != null && !target.IsDead)
                {
                    DealDamageToPlayer(target, attackDamage);
                }
            }

            // Wait for attack animation follow-through (~0.22s to complete the 0.5s clip)
            yield return new WaitForSeconds(0.22f);

            if (cachedAnimator != null)
            {
                cachedAnimator.SetBool(animIsAttackingHash, false);
            }

            lastAttackTime = Time.time;
            isAttacking = false;
            attackRoutine = null;

            // After attack, transition according to current situation
            if (playerTransform != null && (playerDamageable == null || !playerDamageable.IsDead))
            {
                currentState = IsPlayerInAttackRange() ? EnemyState.Attack : EnemyState.Chase;
            }
            else
            {
                EnterPatrolIdle();
            }
        }

        public override void DealDamageToPlayer(IDamageable target, int amount)
        {
            if (target == null || target.IsDead) return;
            CaveDweller.Core.SoundManager.Instance?.PlayMonsterMeleeSFX();
            target.TakeDamage(amount);
        }

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead || collision == null || collision.gameObject == null) return;
            if (!collision.gameObject.CompareTag("Player")) return;

            // Direct collision with player: immediately start bite attack if ready
            if (!isAttacking && Time.time - lastAttackTime >= attackCooldown)
            {
                StartAttack();
            }
        }

        private bool HasPatrolPoints()
        {
            return patrolPointA != null && patrolPointB != null;
        }

        private void SwapPatrolTarget()
        {
            if (patrolPointA == null || patrolPointB == null) return;
            patrolTarget = (patrolTarget == patrolPointA.position) ? patrolPointB.position : patrolPointA.position;
        }

        private bool IsPlayerInAttackRange()
        {
            if (playerTransform == null) return false;

            // Check distance between colliders if available
            if (col != null && playerCollider != null)
            {
                var dist = col.Distance(playerCollider);
                if (dist.isOverlapped || dist.distance <= 0.8f)
                {
                    return true;
                }
            }

            // Fallback or center-to-center distance check
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            return distance <= attackRange;
        }

        private bool IsPlayerInDamageRange()
        {
            if (playerTransform == null) return false;

            if (col != null && playerCollider != null)
            {
                var dist = col.Distance(playerCollider);
                if (dist.isOverlapped || dist.distance <= 1.2f)
                {
                    return true;
                }
            }

            float distance = Vector2.Distance(transform.position, playerTransform.position);
            return distance <= attackRange + 0.6f;
        }

        private bool CheckPlayerVisible()
        {
            if (playerTransform == null) return false;

            Vector2 origin = GetEyePosition();
            Vector2 toPlayer = (Vector2)playerTransform.position - origin;
            float distance = toPlayer.magnitude;
            if (distance > viewDistance) return false;

            // Close proximity awareness: senses player within proximity distance regardless of angle
            if (distance <= proximityDetectDistance)
            {
                return HasLineOfSight(origin, toPlayer, distance);
            }

            // Vision cone angle check
            Vector2 facing = GetFacingVector();
            float angleToPlayer = Vector2.Angle(facing, toPlayer.normalized);
            if (angleToPlayer > viewAngle * 0.5f) return false;

            return HasLineOfSight(origin, toPlayer, distance);
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 toPlayer, float distance)
        {
            int mask = obstacleLayer.value;
            if (mask == 0) mask = LayerMask.GetMask("Default");

            RaycastHit2D hit = Physics2D.Raycast(origin, toPlayer.normalized, distance, mask);
            return hit.collider == null;
        }

        private bool CheckWallAhead()
        {
            if (col == null) return false;

            Bounds bounds = col.bounds;
            Vector2 origin = bounds.center;
            Vector2 size = new Vector2(0.05f, bounds.size.y * 0.7f);
            Vector2 dir = GetFacingVector();

            int mask = obstacleLayer.value;
            if (mask == 0) mask = LayerMask.GetMask("Default");

            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, dir, wallCheckDistance, mask);
            return hit.collider != null;
        }

        private bool CheckLedgeAhead()
        {
            if (col == null) return false;

            Bounds bounds = col.bounds;
            // Project check point forward ahead of front foot
            float checkX = bounds.center.x + (facingDirection * (bounds.extents.x + ledgeCheckForwardOffset));
            float checkY = bounds.min.y + 0.1f;
            Vector2 origin = new Vector2(checkX, checkY);

            int mask = groundLayer.value;
            if (mask == 0) mask = LayerMask.GetMask("Default");

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, ledgeCheckDownDistance, mask);
            // If raycast hits nothing, there is no ground ahead -> ledge/cliff detected!
            return hit.collider == null;
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
                // When moving right (+1), flipX = true (flips left sprite to face right).
                // When moving left (-1), flipX = false (keeps native left-facing sprite).
                spriteRenderer.flipX = spriteFacesLeftByDefault ? (facingDirection > 0f) : (facingDirection < 0f);
            }
        }

        private void ResolvePlayer()
        {
            if (playerTransform == null || playerCollider == null || playerDamageable == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null)
                {
                    playerTransform = go.transform;
                    playerCollider = go.GetComponent<Collider2D>();
                    playerDamageable = ResolveDamageable(go);
                }
            }
        }

        public override void Die()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            isAttacking = false;
            base.Die();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c == null) return;

            Bounds bounds = c.bounds;

            // Draw wall check
            Gizmos.color = Color.red;
            Vector2 wallCheckDir = new Vector2(facingDirection, 0f);
            Gizmos.DrawRay(bounds.center, wallCheckDir * wallCheckDistance);

            // Draw ledge check
            Gizmos.color = Color.yellow;
            float checkX = bounds.center.x + (facingDirection * (bounds.extents.x + ledgeCheckForwardOffset));
            Vector2 ledgeOrigin = new Vector2(checkX, bounds.min.y + 0.1f);
            Gizmos.DrawRay(ledgeOrigin, Vector2.down * ledgeCheckDownDistance);

            // Draw attack range
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Draw vision distance
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, viewDistance);
        }
#endif
    }

    /// <summary>
    /// Legacy alias for MeleeEnemy to avoid breaking serialized references.
    /// </summary>
    [System.Obsolete("Use MeleeEnemy instead")]
    public class PatrolEnemyWithLight : MeleeEnemy { }
}
