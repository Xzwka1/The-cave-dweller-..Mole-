using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CaveDweller.Lighting
{
    [RequireComponent(typeof(Light2D))]
    public class DynamicMuzzleLight : MonoBehaviour
    {
        [Header("Muzzle Flash")]
        [SerializeField] private float flashIntensity = 2f;
        [SerializeField] private float flashRadiusMin = 2f;
        [SerializeField] private float flashRadiusMax = 3.5f;
        [SerializeField] private float fadeDuration = 0.08f;

        [Header("Illumination")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private LayerMask obstructionLayer;
        [SerializeField] private float occlusionBoxSize = 0.1f;

        [Header("Manual Test Input")]
        [SerializeField] private bool enableManualTestInput = false;

        private Light2D muzzleLight;
        private float baseIntensity;
        private float baseOuterRadius;
        private Coroutine fadeRoutine;

        public float FlashIntensity => flashIntensity;
        public float FlashRadiusMin => flashRadiusMin;
        public float FlashRadiusMax => flashRadiusMax;
        public float FadeDuration => fadeDuration;
        public LayerMask EnemyLayer => enemyLayer;
        public LayerMask ObstructionLayer => obstructionLayer;
        public Light2D MuzzleLight => muzzleLight;
        public bool IsFlashing => fadeRoutine != null;

        private void Awake()
        {
            muzzleLight = GetComponent<Light2D>();

            if (muzzleLight != null)
            {
                baseIntensity = muzzleLight.intensity;
                baseOuterRadius = muzzleLight.pointLightOuterRadius;
                muzzleLight.intensity = 0f;
            }
        }

        private void OnValidate()
        {
            flashRadiusMin = Mathf.Clamp(flashRadiusMin, 1f, 5f);
            flashRadiusMax = Mathf.Clamp(flashRadiusMax, 1f, 5f);
            if (flashRadiusMax < flashRadiusMin)
            {
                flashRadiusMax = flashRadiusMin;
            }
            fadeDuration = Mathf.Clamp(fadeDuration, 0.04f, 0.2f);
            flashIntensity = Mathf.Max(0f, flashIntensity);
            occlusionBoxSize = Mathf.Max(0.01f, occlusionBoxSize);
        }

        private void Update()
        {
            if (!enableManualTestInput)
            {
                return;
            }

            ReadTestInput();
        }

        private void ReadTestInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                NotifyShot(transform.position);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                NotifyShot(transform.position);
            }
#else
            if (Input.GetKeyDown(KeyCode.F))
            {
                NotifyShot(transform.position);
            }

            if (Input.GetMouseButtonDown(0))
            {
                NotifyShot(transform.position);
            }
#endif
        }

        public void NotifyShot(Vector2 pos)
        {
            transform.position = pos;
            Flash();
            IlluminateEnemiesInRadius(pos, flashRadiusMax, flashIntensity);
        }

        public void Flash()
        {
            if (muzzleLight == null)
            {
                muzzleLight = GetComponent<Light2D>();
                if (muzzleLight == null)
                {
                    Debug.LogWarning("[DynamicMuzzleLight] Missing Light2D, cannot flash.");
                    return;
                }
                baseIntensity = muzzleLight.intensity;
                baseOuterRadius = muzzleLight.pointLightOuterRadius;
            }

            float radius = Random.Range(flashRadiusMin, flashRadiusMax);
            radius = Mathf.Clamp(radius, 8f, 12f);

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            muzzleLight.pointLightOuterRadius = radius;
            muzzleLight.intensity = flashIntensity;

            fadeRoutine = StartCoroutine(FadeOut());
        }

        public void IlluminateEnemiesInRadius(Vector2 center, float radius, float intensity)
        {
            if (radius <= 0f || intensity <= 0f)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, enemyLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                Vector2 targetPos = hit.bounds.center;

                if (!HasLineOfSight(center, targetPos))
                {
                    continue;
                }

                ILightDetectable detectable = hit.GetComponent<ILightDetectable>();
                if (detectable == null)
                {
                    detectable = hit.GetComponentInParent<ILightDetectable>();
                }

                if (detectable != null)
                {
                    detectable.OnIlluminated(intensity);
                }
            }
        }

        public void IlluminateEnemiesInRadius()
        {
            float radius = muzzleLight != null ? muzzleLight.pointLightOuterRadius : flashRadiusMax;
            IlluminateEnemiesInRadius(transform.position, radius, flashIntensity);
        }

        private bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            direction /= distance;

            Vector2 boxSize = new Vector2(occlusionBoxSize, occlusionBoxSize);

            RaycastHit2D hit = Physics2D.BoxCast(from, boxSize, 0f, direction, distance, obstructionLayer);
            return hit.collider == null;
        }

        private IEnumerator FadeOut()
        {
            if (muzzleLight == null)
            {
                fadeRoutine = null;
                yield break;
            }

            float duration = Mathf.Clamp(fadeDuration, 0.1f, 0.2f);
            float startIntensity = muzzleLight.intensity;
            float startRadius = muzzleLight.pointLightOuterRadius;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (muzzleLight == null)
                {
                    fadeRoutine = null;
                    yield break;
                }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                muzzleLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
                muzzleLight.pointLightOuterRadius = Mathf.Lerp(startRadius, baseOuterRadius, t);
                yield return null;
            }

            if (muzzleLight != null)
            {
                muzzleLight.intensity = 0f;
                muzzleLight.pointLightOuterRadius = baseOuterRadius;
            }
            fadeRoutine = null;
        }
    }
}
