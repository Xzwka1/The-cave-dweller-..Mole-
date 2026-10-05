using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if TMPro
using TMPro;
#endif

namespace CaveDweller.Player
{
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Health Slide Bar")]
        [Tooltip("Direct Unity Slider component for health slide bar.")]
        [SerializeField] private Slider healthSlider;
        [Tooltip("Filled Image component (Horizontal) for continuous slide bar.")]
        [SerializeField] private Image healthBarFill;
        [Tooltip("Background Image component for empty bar (HP_0).")]
        [SerializeField] private Image healthBarBackground;
        [SerializeField] private bool smoothDrain = true;
        [SerializeField] private float drainSpeed = 5f;

        [Header("Sprite Fill Mapping (For composite bar sprites with portraits)")]
        [Tooltip("Minimum fillAmount where the colored health bar starts (0.286 for HP_100 sprite).")]
        [SerializeField] private float minFill = 0.286f;
        [Tooltip("Maximum fillAmount where the colored health bar ends (0.957 for HP_100 sprite).")]
        [SerializeField] private float maxFill = 0.957f;

        [Header("Heart / Slot Display (Fallback / Optional)")]
        [Tooltip("List or array of heart/health icons in order from left to right.")]
        [SerializeField] private Image[] healthIcons;
        [SerializeField] private Sprite fullHealthSprite;
        [SerializeField] private Sprite emptyHealthSprite;

        [Header("Animation / Feedback")]
        [SerializeField] private bool animateOnDamage = true;
        [SerializeField] private float punchScale = 1.15f;
        [SerializeField] private float punchDuration = 0.12f;

        private Coroutine punchCoroutine;
        private Vector3 originalScale = Vector3.one;
        private float targetFill = 1f;
        private float currentFill = 1f;

        private void Awake()
        {
            originalScale = transform.localScale;
            if (healthBarFill == null && healthSlider == null)
            {
                // Auto-detect fill image if present in children
                var fills = GetComponentsInChildren<Image>(true);
                foreach (var img in fills)
                {
                    if (img.type == Image.Type.Filled)
                    {
                        healthBarFill = img;
                        break;
                    }
                }
            }
            if (healthSlider == null)
            {
                healthSlider = GetComponent<Slider>();
                if (healthSlider == null) healthSlider = GetComponentInChildren<Slider>();
            }
        }

        private void Update()
        {
            if (smoothDrain && Mathf.Abs(currentFill - targetFill) > 0.001f)
            {
                currentFill = Mathf.MoveTowards(currentFill, targetFill, Time.deltaTime * drainSpeed);
                ApplyFill(currentFill);
            }
        }

        private void ApplyFill(float fill)
        {
            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = fill;
            }
            if (healthSlider != null)
            {
                healthSlider.value = fill;
            }
        }

        private void Start()
        {
            ResolvePlayerHealth();
            if (playerHealth != null)
            {
                UpdateUI(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void ResolvePlayerHealth()
        {
            if (playerHealth != null) return;

            playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    playerHealth = playerObj.GetComponent<PlayerHealth>();
                    if (playerHealth == null)
                    {
                        playerHealth = playerObj.GetComponentInChildren<PlayerHealth>();
                    }
                }
            }

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
                playerHealth.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(int currentHp, int maxHp)
        {
            UpdateUI(currentHp, maxHp);

            if (animateOnDamage && gameObject.activeInHierarchy)
            {
                if (punchCoroutine != null)
                {
                    StopCoroutine(punchCoroutine);
                }
                punchCoroutine = StartCoroutine(PunchScaleRoutine());
            }
        }

        public void UpdateUI(int currentHp, int maxHp)
        {
            if (maxHp <= 0) maxHp = 100;
            float normalized = Mathf.Clamp01((float)currentHp / maxHp);

            // If player is dead or HP <= 0, fill completely drains to 0
            if (currentHp <= 0)
            {
                targetFill = 0f;
            }
            else
            {
                // Map the health percentage accurately across the colored bar region so it doesn't look empty when player still has HP
                targetFill = Mathf.Lerp(minFill, maxFill, normalized);
            }

            if (!smoothDrain || currentHp <= 0)
            {
                currentFill = targetFill;
                ApplyFill(currentFill);
            }

            // 2. Update heart slots if present
            if (healthIcons != null && healthIcons.Length > 0)
            {
                int count = healthIcons.Length;
                float hpPerSlot = (float)maxHp / count;

                for (int i = 0; i < count; i++)
                {
                    if (healthIcons[i] == null) continue;

                    // A slot is full if player has at least hpPerSlot * (i + 1) or partially thresholded
                    float slotThreshold = (i + 1) * hpPerSlot - (hpPerSlot * 0.5f);
                    bool isSlotActive = currentHp >= slotThreshold;

                    if (isSlotActive)
                    {
                        if (fullHealthSprite != null)
                        {
                            healthIcons[i].sprite = fullHealthSprite;
                        }
                        healthIcons[i].color = Color.white;
                    }
                    else
                    {
                        if (emptyHealthSprite != null)
                        {
                            healthIcons[i].sprite = emptyHealthSprite;
                        }
                        else
                        {
                            // Dim the icon if no empty sprite is provided
                            healthIcons[i].color = new Color(1f, 1f, 1f, 0.25f);
                        }
                    }
                }
            }
        }

        private IEnumerator PunchScaleRoutine()
        {
            transform.localScale = originalScale * punchScale;
            float elapsed = 0f;

            while (elapsed < punchDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / punchDuration;
                transform.localScale = Vector3.Lerp(originalScale * punchScale, originalScale, t);
                yield return null;
            }

            transform.localScale = originalScale;
            punchCoroutine = null;
        }
    }
}
