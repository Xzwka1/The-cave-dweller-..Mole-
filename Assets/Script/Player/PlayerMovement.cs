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
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpForce = 12f;

        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 16f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 1.0f;

        [Header("Ground Check Settings")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.15f;

        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;

        private float horizontalInput;
        private bool isGrounded;
        private bool isDashing;
        private bool canDash = true;
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
            originalGravity = rb.gravityScale;
            if (groundLayer.value == 0)
            {
                groundLayer = 1 << 0;
            }
        }

        private void Update()
        {
            if (isDashing) return;

            ReadInput();
            CheckGround();

            // Flip sprite according to movement
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

        private IEnumerator PerformDash()
        {
            canDash = false;
            isDashing = true;

            rb.gravityScale = 0f;
            float dashDir = horizontalInput != 0f ? Mathf.Sign(horizontalInput) : facingDirection;
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

            yield return new WaitForSeconds(dashDuration);

            rb.gravityScale = originalGravity;
            isDashing = false;

            yield return new WaitForSeconds(dashCooldown);
            canDash = true;
        }
    }
}
