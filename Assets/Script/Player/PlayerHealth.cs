using System.Collections;
using UnityEngine;
using CaveDweller.Combat;
using CaveDweller.Core;

namespace CaveDweller.Player
{
    [RequireComponent(typeof(Collider2D))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        private const int EnemyHitDamage = 20;

        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float invulnerabilityDuration = 0.5f;

        [Header("Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.4f, 0.4f, 0.7f);
        [SerializeField] private float flashInterval = 0.08f;

        private int currentHealth;
        private bool isInvulnerable;
        private bool isDead;
        private Coroutine invulnerabilityCoroutine;
        private Color originalColor;
        private bool hasOriginalColor;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public bool IsInvulnerable => isInvulnerable;
        public float InvulnerabilityDuration => invulnerabilityDuration;

        private void Awake()
        {
            currentHealth = Mathf.Max(1, maxHealth);

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                }
            }

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                hasOriginalColor = true;
            }
        }

        private void OnValidate()
        {
            if (maxHealth < 1) maxHealth = 1;
            if (invulnerabilityDuration < 0f) invulnerabilityDuration = 0f;
            if (flashInterval < 0.02f) flashInterval = 0.02f;
        }

        public void TakeDamage(int amount)
        {
            if (isDead) return;
            if (isInvulnerable) return;
            if (amount <= 0) return;

            currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
                return;
            }

            if (invulnerabilityDuration > 0f)
            {
                if (invulnerabilityCoroutine != null)
                {
                    StopCoroutine(invulnerabilityCoroutine);
                    RestoreVisualState();
                }
                invulnerabilityCoroutine = StartCoroutine(InvulnerabilityRoutine());
            }
        }

        private IEnumerator InvulnerabilityRoutine()
        {
            isInvulnerable = true;
            float elapsed = 0f;
            bool visible = false;

            while (elapsed < invulnerabilityDuration)
            {
                if (spriteRenderer != null && hasOriginalColor)
                {
                    spriteRenderer.color = visible ? originalColor : hitFlashColor;
                    visible = !visible;
                }

                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            RestoreVisualState();
            isInvulnerable = false;
            invulnerabilityCoroutine = null;
        }

        private void RestoreVisualState()
        {
            if (spriteRenderer != null && hasOriginalColor)
            {
                spriteRenderer.color = originalColor;
            }
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;
            isInvulnerable = false;

            if (invulnerabilityCoroutine != null)
            {
                StopCoroutine(invulnerabilityCoroutine);
                invulnerabilityCoroutine = null;
            }

            RestoreVisualState();

            var manager = GameFlowManager.Instance;
            if (manager != null)
            {
                manager.OnPlayerDied();
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] GameFlowManager.Instance is null — cannot report player death. Ensure GameFlowManager exists in scene.", this);
            }
        }

        public void TakeEnemyHit()
        {
            TakeDamage(EnemyHitDamage);
        }

        public bool CanHeal => false;
    }
}
