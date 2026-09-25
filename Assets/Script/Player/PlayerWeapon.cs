using System.Collections;
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
        [SerializeField] private int pelletsCount = 6;
        [SerializeField] private float spreadAngle = 18f;
        [SerializeField] private float bulletRange = 12f;
        [SerializeField] private int damagePerPellet = 20;
        [SerializeField] private LayerMask hitMask = -1;

        [Header("References")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Light2D muzzleFlashLight;
        [SerializeField] private GameObject bulletTracerPrefab;

        [Header("Tracer Settings")]
        [SerializeField] private float tracerLifetime = 0.06f;
        [SerializeField] private float projectileSpeed = 22f;

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

        private void HandleAim()
        {
            if (mainCamera == null) return;
            if (muzzlePoint == null) return;

            Vector2 mouseScreenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                mouseScreenPos = mouse.position.ReadValue();
            }
            else
            {
                return;
            }
#else
            mouseScreenPos = Input.mousePosition;
#endif

            float camToMuzzleDist = Mathf.Abs(mainCamera.transform.position.z - muzzlePoint.position.z);
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, camToMuzzleDist));
            Vector2 direction = (Vector2)mouseWorld - (Vector2)muzzlePoint.position;

            if (direction.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
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
            if (muzzlePoint == null) return;

            currentAmmo--;

            Vector2 origin = muzzlePoint != null ? (Vector2)muzzlePoint.position : (Vector2)transform.position;

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

            float baseAngle = transform.eulerAngles.z;

            bool isProjectilePrefab = false;
            if (bulletTracerPrefab != null && bulletTracerPrefab.GetComponent<Projectile>() != null)
            {
                isProjectilePrefab = true;
            }

            for (int i = 0; i < pelletsCount; i++)
            {
                float spreadOffset = UnityEngine.Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
                float pelletAngle = baseAngle + spreadOffset;
                float rad = pelletAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector2.right;
                }

                if (isProjectilePrefab)
                {
                    GameObject pellet = Instantiate(bulletTracerPrefab, origin, Quaternion.identity);
                    Projectile proj = pellet.GetComponent<Projectile>();
                    if (proj != null)
                    {
                        float lifetime = bulletRange / Mathf.Max(projectileSpeed, 1f);
                        proj.Configure(projectileSpeed, damagePerPellet, lifetime, hitMask);
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
