using UnityEngine;

namespace CaveDweller.Core
{
    /// <summary>
    /// Place this component on any GameObject with a 2D Trigger Collider to define a specific
    /// room, cave section, or arena. When the Player enters, it automatically updates the
    /// CameraController to confine within this zone's bounds and optionally applies custom zoom.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CameraZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("The collider defining the room boundary. If left empty, uses this GameObject's Collider2D.")]
        [SerializeField] private Collider2D zoneCollider;

        [Header("Zoom & Viewport Overrides")]
        [Tooltip("If true, changes the camera zoom level when the player is inside this zone.")]
        [SerializeField] private bool overrideZoom = false;
        [SerializeField] private float targetZoom = 7.5f;

        [Header("Priority (Higher priority overrides overlapping zones)")]
        [SerializeField] private int priority = 0;

        [Header("Gizmo Display")]
        [SerializeField] private Color zoneColor = new Color(0.2f, 0.75f, 1f, 0.35f);

        public Collider2D ZoneCollider => zoneCollider != null ? zoneCollider : (zoneCollider = GetComponent<Collider2D>());
        public bool OverrideZoom => overrideZoom;
        public float TargetZoom => targetZoom;
        public int Priority => priority;

        private void Awake()
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider2D>();
            }
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }

            int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreLayer >= 0)
            {
                gameObject.layer = ignoreLayer;
            }
        }

        // Cached once per scene; Unity's == turns a destroyed controller back into null after a reload.
        private static CameraController cachedController;

        private static CameraController Controller
        {
            get
            {
                if (cachedController == null) cachedController = FindAnyObjectByType<CameraController>();
                return cachedController;
            }
        }

        /// <summary>
        /// True for camera-only volumes (CameraZone components or the CameraBounds box).
        /// Hitscan pellets and projectiles use this to pass straight through camera triggers.
        /// </summary>
        public static bool IsCameraVolume(Collider2D col)
        {
            if (col == null) return false;
            if (col.GetComponent<CameraZone>() != null) return true;
            string n = col.name;
            return n.Contains("CameraBound") || n.Contains("CameraZone");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other) && Controller != null) Controller.EnterCameraZone(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other) && Controller != null) Controller.ExitCameraZone(this);
        }

        private bool IsPlayer(Collider2D col)
        {
            return col.CompareTag("Player") || (col.transform.root != null && col.transform.root.CompareTag("Player"));
        }

        private void OnDrawGizmos()
        {
            Collider2D col = zoneCollider != null ? zoneCollider : GetComponent<Collider2D>();
            if (col == null) return;

            Gizmos.color = zoneColor;
            Bounds b = col.bounds;
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.1f));
        }
    }
}
