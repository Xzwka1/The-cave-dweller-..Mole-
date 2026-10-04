using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaveDweller.Level
{
    /// <summary>Loads <see cref="nextSceneName"/> when the Player enters this trigger.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class MapTeleporter : MonoBehaviour
    {
        [Header("Next Scene Settings")]
        [Tooltip("Exact scene name from Build Settings (case-sensitive), e.g. Winning_Map.")]
        [SerializeField] private string nextSceneName = "Winning_Map";

        private bool isTriggered;

        public string NextSceneName => nextSceneName;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isTriggered || !IsPlayer(other)) return;

            string target = string.IsNullOrWhiteSpace(nextSceneName) ? string.Empty : nextSceneName.Trim();
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogError($"[MapTeleporter] Scene '{target}' is not in Build Settings (names are case-sensitive).", this);
                return;
            }

            isTriggered = true;
            SceneManager.LoadScene(target);
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other.CompareTag("Player")) return true;
            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")) return true;
            return other.transform.root.CompareTag("Player");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(nextSceneName)) return;
            string wanted = nextSceneName.Trim();
            foreach (var s in UnityEditor.EditorBuildSettings.scenes)
            {
                if (s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == wanted) return;
            }
            Debug.LogWarning($"[MapTeleporter] '{wanted}' is not an enabled scene in Build Settings.", this);
        }
#endif
    }
}
