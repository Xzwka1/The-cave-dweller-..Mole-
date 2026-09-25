using UnityEngine;
using UnityEngine.SceneManagement;

public class MAP_Teleporter : MonoBehaviour
{
    [Header("")]
    public string nextSceneName;

   
    private void OnTriggerEnter2D(Collider2D collision)
    {
        
        if (collision.CompareTag("Player"))
        {
            Debug.Log("ผู้เล่นเข้าจุดวาร์ป กำลังโหลด: " + nextSceneName);
            SceneManager.LoadScene(nextSceneName);
        }
    }
}