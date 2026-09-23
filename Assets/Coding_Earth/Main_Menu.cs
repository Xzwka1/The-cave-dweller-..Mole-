using UnityEngine;
using UnityEngine.SceneManagement;

public class Main_Menu : MonoBehaviour
{
    public void GoToFirstMap()
    {
        Debug.Log("กำลังไปที่ First_MAP");
        SceneManager.LoadScene("First_MAP");
    }

    // ฟังก์ชันสำหรับปุ่ม Exit
    public void ExitGame()
    {
        Debug.Log("ออกจากเกม");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; 
#else
        Application.Quit(); // ปิดเกมเมื่อ Build จริง
#endif
    }
}
