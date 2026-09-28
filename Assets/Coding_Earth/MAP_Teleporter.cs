using UnityEngine;
using UnityEngine.SceneManagement;

public class MAP_Teleporter : MonoBehaviour
{
    [Header("Next Scene Settings")]
    public string nextSceneName;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTriggered) return;

        bool isPlayer = collision.CompareTag("Player");
        if (!isPlayer && collision.attachedRigidbody != null)
        {
            isPlayer = collision.attachedRigidbody.CompareTag("Player");
        }
        if (!isPlayer && collision.transform.root != null)
        {
            isPlayer = collision.transform.root.CompareTag("Player");
        }

        if (isPlayer)
        {
            isTriggered = true;
            string targetScene = NormalizeSceneName(nextSceneName);
            Debug.Log($"[MAP_Teleporter] ผู้เล่นเข้าจุดวาร์ป กำลังโหลด: {targetScene}");

            if (!string.IsNullOrEmpty(targetScene))
            {
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                Debug.LogWarning("[MAP_Teleporter] nextSceneName is not configured!");
                isTriggered = false;
            }
        }
    }

    private string NormalizeSceneName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;
        string trimmed = rawName.Trim();

        // Handle common casing mismatches between Scene files
        if (string.Equals(trimmed, "Second_MAP", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Second_Map";
        }
        if (string.Equals(trimmed, "First_Map", System.StringComparison.OrdinalIgnoreCase))
        {
            return "First_MAP";
        }
        if (string.Equals(trimmed, "Third_Map", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Third_MAP";
        }
        if (string.Equals(trimmed, "Winning_MAP", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Winning_Map";
        }

        return trimmed;
    }
}