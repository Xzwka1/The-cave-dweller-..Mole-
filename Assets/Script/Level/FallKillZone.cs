using UnityEngine;
using CaveDweller.Combat;
using CaveDweller.Player;

namespace CaveDweller.Level
{
    /// <summary>
    /// Instantly kills the player (or damageable entities) when falling into this volume.
    /// Attached to FallCollider or bottom-of-map kill triggers.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FallKillZone : MonoBehaviour
    {
        [Tooltip("If true, automatically sets the attached Collider2D to a trigger on Awake.")]
        [SerializeField] private bool enforceTrigger = true;

        private void Awake()
        {
            if (enforceTrigger && TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleKill(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleKill(collision.collider);
        }

        private void HandleKill(Collider2D other)
        {
            if (other == null) return;

            // 1. Direct player health check
            var playerHealth = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                if (!playerHealth.IsDead)
                {
                    playerHealth.InstantKill();
                }
                return;
            }

            // 2. Generic damageable check
            var damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(999999);
            }
        }
    }
}
