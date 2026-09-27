using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CaveDweller.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 8.5f;
        [SerializeField] private float jumpForce = 15.0f;

        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 22.0f;
        [SerializeField] private float dashDuration = 0.22f;
        [SerializeField] private float dashCooldown = 1.0f;
        [SerializeField] private float postDashGraceTime = 0.1f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("Ground Check Settings")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.15f;

        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        private PlayerHealth playerHealth;

        private float horizontalInput;
        private bool isGrounded;
        private bool isDashing;
        private bool canDash = true;
        private bool isPhasingEnemies;
        private float facingDirection = 1f; // 1 = Right, -1 = Left
        private float originalGravity;

        public bool IsGrounded => isGrounded;
        public bool IsDashing => isDashing;
        public float FacingDirection => facingDirection;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            playerHealth = GetComponent<PlayerHealth>();
            originalGravity = rb.gravityScale;

            if (groundLayer.value == 0)
            {
                groundLayer = 1 << 0;
            }

            if (enemyLayers.value == 0)
            {
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer != -1)
                {
                    enemyLayers = 1 << enemyLayer;
                }
            }

            // Zero friction physics material to prevent sticking/floating against walls & enemies
            if (col != null)
            {
                var zeroFriction = new PhysicsMaterial2D("PlayerFrictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
                col.sharedMaterial = zeroFriction;

                if (col is BoxCollider2D boxCol && boxCol.edgeRadius < 0.02f)
                {
                    boxCol.edgeRadius = 0.02f;
                }
            }

            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        private void Update()
        {
            if (isDashing) return;

            ReadInput();
            CheckGround();

            // Flip sprite according to movement, or mouse aim direction when idle
            if (horizontalInput > 0.05f)
            {
                facingDirection = 1f;
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }
            else if (horizontalInput < -0.05f)
            {
                facingDirection = -1f;
                if (spriteRenderer != null) spriteRenderer.flipX = true;
            }
            else
            {
                // Idle: face towards mouse cursor
                Camera cam = Camera.main;
                if (cam != null && spriteRenderer != null)
                {
                    Vector2 mouseScreen = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                    if (Mouse.current != null) mouseScreen = Mouse.current.position.ReadValue();
#else
                    mouseScreen = Input.mousePosition;
#endif
                    Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(cam.transform.position.z)));
                    if (mouseWorld.x < transform.position.x - 0.2f)
                    {
                        facingDirection = -1f;
                        spriteRenderer.flipX = true;
                    }
                    else if (mouseWorld.x > transform.position.x + 0.2f)
                    {
                        facingDirection = 1f;
                        spriteRenderer.flipX = false;
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (isDashing) return;

            // Move horizontally
            rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
        }

        private void ReadInput()
        {
            horizontalInput = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontalInput -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontalInput += 1f;

                if ((kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) && isGrounded)
                {
                    Jump();
                }

                if (kb.leftShiftKey.wasPressedThisFrame && canDash)
                {
                    StartCoroutine(PerformDash());
                }
            }
#else
            horizontalInput = Input.GetAxisRaw("Horizontal");

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                Jump();
            }

            if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
            {
                StartCoroutine(PerformDash());
            }
#endif
        }

        private void Jump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        private void CheckGround()
        {
            if (col == null)
            {
                isGrounded = false;
                return;
            }

            // Raycast downward from bottom center of collider
            Bounds bounds = col.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y);
            RaycastHit2D hit = Physics2D.BoxCast(origin, new Vector2(bounds.size.x * 0.8f, 0.05f), 0f, Vector2.down, groundCheckDistance, groundLayer);

            isGrounded = hit.collider != null;
        }

        private void OnDisable()
        {
            RestoreEnemyCollisions();
        }

        private IEnumerator PerformDash()
        {
            canDash = false;
            isDashing = true;

            EnableEnemyPhasing();

            rb.gravityScale = 0f;
            float dashDir = horizontalInput != 0f ? Mathf.Sign(horizontalInput) : facingDirection;
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

            yield return new WaitForSeconds(dashDuration);

            rb.gravityScale = originalGravity;
            isDashing = false;

            // Grace period for I-frames and phasing through enemies
            if (postDashGraceTime > 0f)
            {
                yield return new WaitForSeconds(postDashGraceTime);
            }

            // Wait until player is no longer overlapping an enemy collider (max 0.25s safety timeout)
            float timeout = 0.25f;
            while (timeout > 0f && IsOverlappingEnemy())
            {
                yield return null;
                timeout -= Time.deltaTime;
            }

            RestoreEnemyCollisions();

            yield return new WaitForSeconds(dashCooldown);
            canDash = true;
        }

        private void EnableEnemyPhasing()
        {
            isPhasingEnemies = true;
            int playerLayer = gameObject.layer;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
            }

            if (playerHealth != null)
            {
                playerHealth.SetDashInvulnerable(true);
            }
        }

        private void RestoreEnemyCollisions()
        {
            if (!isPhasingEnemies) return;
            isPhasingEnemies = false;

            int playerLayer = gameObject.layer;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
            }

            if (playerHealth != null)
            {
                playerHealth.SetDashInvulnerable(false);
            }
        }

        private bool IsOverlappingEnemy()
        {
            if (col == null || enemyLayers.value == 0) return false;
            Collider2D hit = Physics2D.OverlapBox(col.bounds.center, col.bounds.size * 0.9f, 0f, enemyLayers);
            return hit != null;
        }
    }
}
