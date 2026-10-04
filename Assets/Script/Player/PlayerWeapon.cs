using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CaveDweller.Combat;
using CaveDweller.Lighting;

namespace CaveDweller.Player
{
    public class PlayerWeapon : MonoBehaviour
    {
        [Header("Shotgun Settings")]
        [SerializeField] private int maxAmmo = 2;
        [SerializeField] private float reloadTime = 1.2f;
        [SerializeField] private int pelletsCount = 7;
        [SerializeField] private float spreadAngle = 36f;
        [SerializeField] private float bulletRange = 22f;
        [SerializeField] private int damagePerPellet = 18;
        [SerializeField] private float recoilForce = 1.6f;
        [SerializeField] private float screenShakeMagnitude = 0.07f;
        [SerializeField] private LayerMask hitMask = -1;

        [Header("References")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Light2D muzzleFlashLight;
        [SerializeField] private GameObject bulletTracerPrefab;

        [Header("Tracer Settings")]
        [SerializeField] private float tracerLifetime = 0.06f;
        [SerializeField] private float projectileSpeed = 32f;

        private int currentAmmo;
        private bool isReloading;
        private Coroutine reloadCoroutine;
        private Coroutine flashCoroutine;
        private Coroutine shootingStateRoutine;
        private Camera _cachedCamera;
        private Animator playerAnimator;
        private int animShootTriggerHash;
        private int animIsShootingHash;
        private int animIsJumpShootingHash;
        private int animIsDashingShootHash;
        private int animIsDashingBackShootHash;
        private int animIsDashingJumpShootHash;
        private int animIsDashingBackJumpShootHash;

        private Camera MainCamera
        {
            get
            {
                if (_cachedCamera != null) return _cachedCamera;
                _cachedCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                return _cachedCamera;
            }
        }

        public int MaxAmmo => maxAmmo;
        public int CurrentAmmo => currentAmmo;
        public bool IsReloading => isReloading;
        public float ReloadTime => reloadTime;
        public int PelletsCount => pelletsCount;
        public float SpreadAngle => spreadAngle;
        public float BulletRange => bulletRange;
        public int DamagePerPellet => damagePerPellet;
        public float ProjectileSpeed => projectileSpeed;
        public Transform MuzzlePoint => muzzlePoint;
        public Light2D MuzzleFlashLight => muzzleFlashLight;
        public GameObject BulletTracerPrefab => bulletTracerPrefab;

        private void Awake()
        {
            currentAmmo = maxAmmo;

            if (muzzlePoint == null)
            {
                muzzlePoint = transform;
            }

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (hitMask.value == -1 && playerLayer != -1)
            {
                hitMask = ~((1 << playerLayer) | (1 << 2)); // Exclude Player and Ignore Raycast
            }

            playerAnimator = GetComponentInParent<Animator>();
            if (playerAnimator == null)
            {
                playerAnimator = transform.root.GetComponentInChildren<Animator>();
            }
            animShootTriggerHash = Animator.StringToHash("Shoot");
            animIsShootingHash = Animator.StringToHash("IsShooting");
            animIsJumpShootingHash = Animator.StringToHash("IsJumpShooting");
            animIsDashingShootHash = Animator.StringToHash("IsDashingShoot");
            animIsDashingBackShootHash = Animator.StringToHash("IsDashingBackShoot");
            animIsDashingJumpShootHash = Animator.StringToHash("IsDashingJumpShoot");
            animIsDashingBackJumpShootHash = Animator.StringToHash("IsDashingBackJumpShoot");

            _cachedCamera = Camera.main;
        }

        private void Start()
        {
            // Camera may not have spawned yet at Awake; the MainCamera getter re-resolves on demand.
            if (MainCamera == null)
            {
                Debug.LogWarning("[PlayerWeapon] No Camera found. Aim and spread will fall back to world-right until one appears.", this);
            }
        }

        private void Update()
        {
            if (MainCamera == null) return;

            HandleAim();
            HandleInput();
        }

        private Vector3 GetMouseWorldPosition()
        {
            Camera cam = MainCamera;
            if (cam == null)
            {
                return transform.position + Vector3.right;
            }

            Vector2 mouseScreenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                mouseScreenPos = mouse.position.ReadValue();
            }
#else
            mouseScreenPos = Input.mousePosition;
#endif
            float camDist = Mathf.Abs(cam.transform.position.z);
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, camDist));
            worldPos.z = 0f;
            return worldPos;
        }

        private void HandleAim()
        {
            Vector3 mouseWorld = GetMouseWorldPosition();

            // Aim from the gun's pivot (player chest) towards mouse world position
            Vector2 aimDir = (Vector2)mouseWorld - (Vector2)transform.position;
            if (aimDir.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void HandleInput()
        {
            bool firePressed = false;
            bool reloadPressed = false;

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;

            if (mouse != null)
            {
                firePressed = mouse.leftButton.wasPressedThisFrame;
            }

            if (kb != null)
            {
                reloadPressed = kb.rKey.wasPressedThisFrame;
            }
#else
            firePressed = Input.GetMouseButtonDown(0);
            reloadPressed = Input.GetKeyDown(KeyCode.R);
#endif

            if (reloadPressed && !isReloading && currentAmmo < maxAmmo)
            {
                StartReload();
                return;
            }

            if (firePressed)
            {
                TryFire();
            }
        }

        private void TryFire()
        {
            if (isReloading) return;

            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            Fire();
        }

        private void Fire()
        {
            currentAmmo--;

            Vector3 mouseWorld = GetMouseWorldPosition();
            Vector2 origin = muzzlePoint != null ? (Vector2)muzzlePoint.position : (Vector2)transform.position;

            // Direct line from muzzle straight to mouse cursor
            Vector2 aimToMouse = ((Vector2)mouseWorld - origin).normalized;
            if (aimToMouse.sqrMagnitude < 0.0001f)
            {
                aimToMouse = transform.right;
            }
            float centerAngle = Mathf.Atan2(aimToMouse.y, aimToMouse.x) * Mathf.Rad2Deg;

            // Physical recoil on player
            Rigidbody2D playerRb = transform.root.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.AddForce(-aimToMouse * recoilForce, ForceMode2D.Impulse);
            }

            // Screen shake feedback
            Camera camShake = MainCamera;
            if (camShake != null)
            {
                var camCtrl = camShake.GetComponent<CaveDweller.Core.CameraController>();
                if (camCtrl != null)
                {
                    camCtrl.Shake(0.08f, screenShakeMagnitude);
                }
            }

            // Audio feedback — silent when the SoundManager hasn't spawned yet
            if (CaveDweller.Core.SoundManager.TryGetInstance(out var sfxMgr))
            {
                sfxMgr.PlayGunshotSFX();
            }

            // Character shoot recoil animation feedback
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger(animShootTriggerHash);
                if (shootingStateRoutine != null)
                {
                    StopCoroutine(shootingStateRoutine);
                }
                shootingStateRoutine = StartCoroutine(SetShootingStateRoutine());
            }

            if (muzzleFlashLight != null)
            {
                var dynLight = muzzleFlashLight.GetComponent<DynamicMuzzleLight>();
                if (dynLight != null)
                {
                    dynLight.NotifyShot(origin);
                }
                else
                {
                    if (flashCoroutine != null)
                    {
                        StopCoroutine(flashCoroutine);
                        flashCoroutine = null;
                    }
                    flashCoroutine = StartCoroutine(MuzzleFlashCoroutine());
                }
            }

            bool isProjectilePrefab = false;
            if (bulletTracerPrefab != null && bulletTracerPrefab.GetComponent<Projectile>() != null)
            {
                isProjectilePrefab = true;
            }

            List<Collider2D> pelletColliders = new List<Collider2D>();
            float halfSpread = spreadAngle * 0.5f;

            for (int i = 0; i < pelletsCount; i++)
            {
                // Organic randomized shotgun spread:
                // Stratified random sampling across the spread cone with organic scatter flutter
                float sliceStart = -halfSpread + (i * spreadAngle / pelletsCount);
                float sliceEnd = sliceStart + (spreadAngle / pelletsCount);
                float randomAngleOffset = UnityEngine.Random.Range(sliceStart, sliceEnd);
                float scatterFlutter = UnityEngine.Random.Range(-spreadAngle * 0.12f, spreadAngle * 0.12f);
                float pelletAngle = centerAngle + Mathf.Clamp(randomAngleOffset + scatterFlutter, -halfSpread * 1.08f, halfSpread * 1.08f);

                float rad = pelletAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                float speedVariance = UnityEngine.Random.Range(0.88f, 1.14f);
                float pelletSpeed = projectileSpeed * speedVariance;
                float rangeVariance = UnityEngine.Random.Range(0.92f, 1.08f);
                float effectiveRange = bulletRange * rangeVariance;

                if (isProjectilePrefab)
                {
                    GameObject pellet = Instantiate(bulletTracerPrefab, origin, Quaternion.identity);
                    Projectile proj = pellet.GetComponent<Projectile>();
                    Collider2D pCol = pellet.GetComponent<Collider2D>();
                    if (pCol != null)
                    {
                        pelletColliders.Add(pCol);
                    }
                    if (proj != null)
                    {
                        float lifetime = effectiveRange / Mathf.Max(pelletSpeed, 1f);
                        proj.Configure(pelletSpeed, damagePerPellet, lifetime, hitMask);
                        proj.Launch(dir, transform.root.gameObject);
                    }
                }
                else
                {
                    RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, effectiveRange, hitMask);
                    Vector2 endPoint = origin + dir * bulletRange;

                    for (int h = 0; h < hits.Length; h++)
                    {
                        RaycastHit2D hit = hits[h];
                        if (hit.collider == null) continue;
                        if (hit.collider.transform.root == transform.root || hit.collider.CompareTag("Player")) continue;
                        if (CaveDweller.Core.CameraZone.IsCameraVolume(hit.collider)) continue;

                        IDamageable damageable = hit.collider.GetComponent<IDamageable>() ?? hit.collider.GetComponentInParent<IDamageable>();
                        if (damageable == null && hit.collider.attachedRigidbody != null)
                        {
                            damageable = hit.collider.attachedRigidbody.GetComponent<IDamageable>();
                        }

                        // Ignore triggers without IDamageable (camera bounds, checkpoints, etc.)
                        if (hit.collider.isTrigger && damageable == null) continue;

                        endPoint = hit.point;
                        if (damageable != null)
                        {
                            damageable.TakeDamage(damagePerPellet);
                        }
                        break;
                    }

                    if (bulletTracerPrefab != null)
                    {
                        SpawnTracer(origin, endPoint);
                    }
                    else
                    {
                        Debug.DrawLine(origin, endPoint, Color.yellow, 0.05f);
                    }
                }
            }

            // Ensure all pellets spawned in this blast ignore each other in Unity physics
            for (int a = 0; a < pelletColliders.Count; a++)
            {
                for (int b = a + 1; b < pelletColliders.Count; b++)
                {
                    if (pelletColliders[a] != null && pelletColliders[b] != null)
                    {
                        Physics2D.IgnoreCollision(pelletColliders[a], pelletColliders[b], true);
                    }
                }
            }

            if (currentAmmo <= 0 && !isReloading)
            {
                StartReload();
            }
        }

        private void SpawnTracer(Vector2 start, Vector2 end)
        {
            if (bulletTracerPrefab == null) return;

            GameObject tracer = Instantiate(bulletTracerPrefab, start, Quaternion.identity);
            if (tracer == null) return;

            LineRenderer lr = tracer.GetComponent<LineRenderer>();
            if (lr == null)
            {
                lr = tracer.GetComponentInChildren<LineRenderer>();
            }

            if (lr != null)
            {
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(start.x, start.y, 0f));
                lr.SetPosition(1, new Vector3(end.x, end.y, 0f));
            }
            else
            {
                Vector2 dir = end - start;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                tracer.transform.position = new Vector3(start.x, start.y, 0f);
                tracer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            Destroy(tracer, tracerLifetime);
        }

        private void StartReload()
        {
            if (isReloading) return;
            if (currentAmmo >= maxAmmo) return;

            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }
            reloadCoroutine = StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;

            yield return new WaitForSeconds(reloadTime);

            currentAmmo = maxAmmo;
            isReloading = false;
            reloadCoroutine = null;
        }

        private IEnumerator SetShootingStateRoutine()
        {
            if (playerAnimator != null)
            {
                ResetShootingBools();

                var pm = transform.root.GetComponent<PlayerMovement>();
                if (pm != null && !pm.IsGrounded)
                {
                    // IN AIR
                    if (pm.IsDashing)
                    {
                        if (pm.IsDashingBackward)
                            playerAnimator.SetBool(animIsDashingBackJumpShootHash, true);
                        else
                            playerAnimator.SetBool(animIsDashingJumpShootHash, true);
                    }
                    else
                    {
                        playerAnimator.SetBool(animIsJumpShootingHash, true);
                    }
                }
                else if (pm != null && pm.IsDashing)
                {
                    // DASHING ON GROUND
                    if (pm.IsDashingBackward)
                        playerAnimator.SetBool(animIsDashingBackShootHash, true);
                    else
                        playerAnimator.SetBool(animIsDashingShootHash, true);
                }
                else
                {
                    // GROUND STANDING / WALKING
                    playerAnimator.SetBool(animIsShootingHash, true);
                }
            }

            yield return new WaitForSeconds(0.22f);

            ResetShootingBools();
            shootingStateRoutine = null;
        }

        private void ResetShootingBools()
        {
            if (playerAnimator == null) return;
            playerAnimator.SetBool(animIsShootingHash, false);
            playerAnimator.SetBool(animIsJumpShootingHash, false);
            playerAnimator.SetBool(animIsDashingShootHash, false);
            playerAnimator.SetBool(animIsDashingBackShootHash, false);
            playerAnimator.SetBool(animIsDashingJumpShootHash, false);
            playerAnimator.SetBool(animIsDashingBackJumpShootHash, false);
        }

        private IEnumerator MuzzleFlashCoroutine()
        {
            if (muzzleFlashLight == null) yield break;

            muzzleFlashLight.enabled = true;

            float flashDuration = UnityEngine.Random.Range(0.1f, 0.2f);
            yield return new WaitForSeconds(flashDuration);

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
            }

            flashCoroutine = null;
        }

        private void OnDisable()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            if (muzzleFlashLight != null)
            {
                muzzleFlashLight.enabled = false;
            }
        }
    }
}
