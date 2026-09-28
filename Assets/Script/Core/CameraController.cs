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

        private Camera cam;
        private Vector3 currentVelocity;
        private float targetZoom;
        private float shakeTimer = 0f;
        private float shakeMagnitude = 0f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam.orthographicSize > maxZoom || cam.orthographicSize < minZoom)
            {
                cam.orthographicSize = 7.5f;
            }
            targetZoom = cam.orthographicSize;
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

        public void Shake(float duration = 0.08f, float magnitude = 0.06f)
        {
            shakeTimer = duration;
            shakeMagnitude = magnitude;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
