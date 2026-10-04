using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CaveDweller.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, -10f);
        [SerializeField] private float smoothSpeed = 0.15f;

        [Header("Mouse Aim Look-Ahead")]
        [SerializeField] private bool enableMouseLookAhead = true;
        [SerializeField] private float maxLookAheadDistance = 3.5f;
        [SerializeField] private float mouseInfluence = 0.32f;

        [Header("Zoom Settings (GDD Spec)")]
        [SerializeField] private float minZoom = 5.0f;
        [SerializeField] private float maxZoom = 11.5f;
        [SerializeField] private float zoomSensitivity = 1.5f;
        [SerializeField] private float zoomSmoothSpeed = 10f;

        [Header("Confine Bounds")]
        [Tooltip("Enable or disable camera viewport confinement to a boundary.")]
        [SerializeField] private bool enableConfine = true;
        [Tooltip("Assign a Collider2D representing the camera boundary box (e.g. BoxCollider2D, PolygonCollider2D, CompositeCollider2D).")]
        [SerializeField] private Collider2D boundsCollider;
        [Tooltip("If true, uses minBounds and maxBounds coordinates instead of a Collider2D.")]
        [SerializeField] private bool useManualBounds = false;
        [SerializeField] private Vector2 minBounds = new Vector2(-50f, -25f);
        [SerializeField] private Vector2 maxBounds = new Vector2(50f, 25f);
        [Tooltip("Automatically searches for a GameObject tagged 'CameraBounds' or named 'CameraBounds' if none is assigned.")]
        [SerializeField] private bool autoFindBounds = true;

        [Header("Gizmos")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color boundsGizmoColor = new Color(0f, 1f, 0.35f, 0.6f);
        [SerializeField] private Color camViewGizmoColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] private Color clampedCenterGizmoColor = new Color(1f, 0.85f, 0.1f, 0.5f);

        private Camera cam;
        private Vector3 currentVelocity;
        private float targetZoom;
        private float shakeTimer = 0f;
        private float shakeMagnitude = 0f;

        private readonly System.Collections.Generic.List<CameraZone> activeZones = new System.Collections.Generic.List<CameraZone>();
        private Collider2D defaultBoundsCollider;
        private bool defaultUseManualBounds;
        private Vector2 defaultMinBounds;
        private Vector2 defaultMaxBounds;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam.orthographicSize > maxZoom || cam.orthographicSize < minZoom)
            {
                cam.orthographicSize = 7.5f;
            }
            targetZoom = cam.orthographicSize;

            defaultBoundsCollider = boundsCollider;
            defaultUseManualBounds = useManualBounds;
            defaultMinBounds = minBounds;
            defaultMaxBounds = maxBounds;
        }

        private void Start()
        {
            if (target == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }

            if (enableConfine && boundsCollider == null && !useManualBounds && autoFindBounds)
            {
                var boundsObj = GameObject.FindWithTag("CameraBounds") ?? GameObject.Find("CameraBounds");
                if (boundsObj != null)
                {
                    boundsCollider = boundsObj.GetComponent<Collider2D>();
                }
            }

            if (boundsCollider != null)
            {
                int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreLayer >= 0)
                {
                    boundsCollider.gameObject.layer = ignoreLayer;
                }
            }
        }

        private void LateUpdate()
        {
            HandleFollow();
            HandleZoom();
        }

        private void HandleFollow()
        {
            if (target == null) return;
            if (cam == null) cam = GetComponent<Camera>();

            Vector3 mouseOffset = Vector3.zero;

            if (enableMouseLookAhead && cam != null)
            {
                Vector2 mouseScreenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    mouseScreenPos = mouse.position.ReadValue();
                }
#else
                mouseScreenPos = Input.mousePosition;
#endif
                if (mouseScreenPos.sqrMagnitude > 0.001f)
                {
                    float camDist = Mathf.Abs(transform.position.z - target.position.z);
                    Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, camDist));
                    Vector3 diff = mouseWorld - (target.position + offset);
                    diff.z = 0f;

                    // Clamped offset towards mouse direction so camera never leaves player
                    mouseOffset = Vector3.ClampMagnitude(diff * mouseInfluence, maxLookAheadDistance);
                }
            }

            Vector3 shakeOffset = Vector3.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                shakeOffset = (Vector3)(Random.insideUnitCircle * shakeMagnitude);
            }

            Vector3 targetPosition = target.position + offset + mouseOffset + shakeOffset;

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothSpeed);

            // Single post-clamp guarantees the viewport never leaks outside bounds
            // (smooth damping would otherwise ease through the edge, and screen shake
            // can push past it by up to shakeMagnitude).
            if (enableConfine)
            {
                transform.position = ClampPositionToBounds(transform.position, cam.orthographicSize);
            }
        }

        private void HandleZoom()
        {
            float scrollDelta = 0f;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                scrollDelta = mouse.scroll.ReadValue().y;
            }
#else
            scrollDelta = Input.GetAxis("Mouse ScrollWheel") * 100f;
#endif

            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // Invert or scale scroll: scrolling down increases orthographic size (zooms out)
                targetZoom -= Mathf.Sign(scrollDelta) * zoomSensitivity;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomSmoothSpeed);
        }

        /// <summary>
        /// Clamps a world position so that the camera's rectangular viewport (at the given orthographic size)
        /// remains strictly inside the active boundary box. If the room is narrower/shorter than the viewport,
        /// it cleanly centers the camera along that axis.
        /// </summary>
        public Vector3 ClampPositionToBounds(Vector3 targetPos, float orthoSize)
        {
            if (!enableConfine) return targetPos;

            Bounds b;
            if (!TryGetActiveBounds(out b)) return targetPos;

            float vertExtent = orthoSize;
            float horzExtent = vertExtent * (cam != null ? cam.aspect : 16f / 9f);

            float minX = b.min.x + horzExtent;
            float maxX = b.max.x - horzExtent;
            float minY = b.min.y + vertExtent;
            float maxY = b.max.y - vertExtent;

            float clampedX;
            if (minX > maxX)
            {
                // Bounding box is narrower than camera view -> center camera horizontally
                clampedX = b.center.x;
            }
            else
            {
                clampedX = Mathf.Clamp(targetPos.x, minX, maxX);
            }

            float clampedY;
            if (minY > maxY)
            {
                // Bounding box is shorter than camera view -> center camera vertically
                clampedY = b.center.y;
            }
            else
            {
                clampedY = Mathf.Clamp(targetPos.y, minY, maxY);
            }

            return new Vector3(clampedX, clampedY, targetPos.z);
        }

        /// <summary>
        /// Retrieves the current active world bounding box.
        /// </summary>
        public bool TryGetActiveBounds(out Bounds bounds)
        {
            bounds = default;
            if (!enableConfine) return false;

            if (useManualBounds)
            {
                Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
                Vector3 size = new Vector3(Mathf.Abs(maxBounds.x - minBounds.x), Mathf.Abs(maxBounds.y - minBounds.y), 100f);
                bounds = new Bounds(center, size);
                return true;
            }

            if (boundsCollider != null)
            {
                bounds = boundsCollider.bounds;
                return true;
            }

            return false;
        }

        public void EnterCameraZone(CameraZone zone)
        {
            if (!activeZones.Contains(zone))
            {
                activeZones.Add(zone);
                activeZones.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            UpdateCurrentZone();
        }

        public void ExitCameraZone(CameraZone zone)
        {
            activeZones.Remove(zone);
            UpdateCurrentZone();
        }

        private void UpdateCurrentZone()
        {
            if (activeZones.Count > 0)
            {
                var topZone = activeZones[0];
                boundsCollider = topZone.ZoneCollider;
                useManualBounds = false;
                if (topZone.OverrideZoom)
                {
                    targetZoom = Mathf.Clamp(topZone.TargetZoom, minZoom, maxZoom);
                }
            }
            else
            {
                boundsCollider = defaultBoundsCollider;
                useManualBounds = defaultUseManualBounds;
                minBounds = defaultMinBounds;
                maxBounds = defaultMaxBounds;
            }
        }

        public void SetBounds(Collider2D newBounds)
        {
            boundsCollider = newBounds;
            if (boundsCollider != null)
            {
                int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreLayer >= 0)
                {
                    boundsCollider.gameObject.layer = ignoreLayer;
                }
            }
            useManualBounds = false;
            enableConfine = true;
        }

        public void SetManualBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
            useManualBounds = true;
            enableConfine = true;
        }

        public void SetEnableConfine(bool enable)
        {
            enableConfine = enable;
        }

        public void Shake(float duration = 0.08f, float magnitude = 0.06f)
        {
            shakeTimer = duration;
            shakeMagnitude = magnitude;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Bounds b;
            if (!TryGetActiveBounds(out b)) return;

            // 1. Outer world boundary box (Green)
            Gizmos.color = boundsGizmoColor;
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.1f));

            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;

            float vertExtent = cam.orthographicSize;
            float horzExtent = vertExtent * cam.aspect;

            float minX = b.min.x + horzExtent;
            float maxX = b.max.x - horzExtent;
            float minY = b.min.y + vertExtent;
            float maxY = b.max.y - vertExtent;

            // 2. Allowed camera center boundary (Yellow)
            if (minX <= maxX && minY <= maxY)
            {
                Vector3 centerBoxPos = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
                Vector3 centerBoxSize = new Vector3(maxX - minX, maxY - minY, 0.1f);
                Gizmos.color = clampedCenterGizmoColor;
                Gizmos.DrawWireCube(centerBoxPos, centerBoxSize);
            }

            // 3. Current camera viewport (Cyan)
            Gizmos.color = camViewGizmoColor;
            Gizmos.DrawWireCube(transform.position + Vector3.forward * 10f, new Vector3(horzExtent * 2f, vertExtent * 2f, 0.1f));
        }
    }
}
