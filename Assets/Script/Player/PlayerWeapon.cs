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
        private Camera mainCamera;

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

            mainCamera = Camera.main;
        }

        private void Start()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Camera camObj = FindFirstObjectByType<Camera>();
                if (camObj != null)
                {
                    mainCamera = camObj;
                }
            }
        }

        private void Update()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            HandleAim();
            HandleInput();
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return transform.position + Vector3.right;
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
            float camDist = Mathf.Abs(mainCamera.transform.position.z);
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, camDist));
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
            if (mainCamera != null)
            {
                var camCtrl = mainCamera.GetComponent<CaveDweller.Core.CameraController>();
                if (camCtrl != null)
                {
                    camCtrl.Shake(0.08f, screenShakeMagnitude);
                }
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
            float step = pelletsCount > 1 ? spreadAngle / (pelletsCount - 1) : 0f;

            for (int i = 0; i < pelletsCount; i++)
            {
                // Symmetrical fan distribution with middle pellet aligned 100% on mouse cursor
                float baseOffset = -spreadAngle * 0.5f + i * step;
                // Middle pellet has zero jitter for pinpoint center shot; side pellets have subtle jitter
                bool isCenterPellet = (pelletsCount % 2 == 1 && i == pelletsCount / 2);
                float jitter = isCenterPellet ? 0f : UnityEngine.Random.Range(-step * 0.2f, step * 0.2f);
                float pelletAngle = centerAngle + baseOffset + jitter;
                float rad = pelletAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                float speedVariance = UnityEngine.Random.Range(0.95f, 1.05f);
                float pelletSpeed = projectileSpeed * speedVariance;

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
                        float lifetime = bulletRange / Mathf.Max(pelletSpeed, 1f);
                        proj.Configure(pelletSpeed, damagePerPellet, lifetime, hitMask);
                        proj.Launch(dir, transform.root.gameObject);
                    }
                }
                else
                {
                    RaycastHit2D hit = Physics2D.Raycast(origin, dir, bulletRange, hitMask);
                    Vector2 endPoint;

                    if (hit.collider != null && hit.collider.transform.root != transform.root && !hit.collider.CompareTag("Player"))
                    {
                        endPoint = hit.point;

                        IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                        if (damageable == null)
                        {
                            damageable = hit.collider.GetComponentInParent<IDamageable>();
                        }

                        if (damageable != null)
                        {
                            damageable.TakeDamage(damagePerPellet);
                        }
                    }
                    else
                    {
                        endPoint = origin + dir * bulletRange;
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
