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

        [Header("Zoom Settings (GDD Spec)")]
        [SerializeField] private float minZoom = 4.0f;
        [SerializeField] private float maxZoom = 9.0f;
        [SerializeField] private float zoomSensitivity = 1.5f;
        [SerializeField] private float zoomSmoothSpeed = 10f;

        private Camera cam;
        private Vector3 currentVelocity;
        private float targetZoom;

        private void Awake()
        {
            cam = GetComponent<Camera>();
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

            Vector3 targetPosition = target.position + offset;
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

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
