using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaveDweller.Core
{
    /// <summary>Main menu button handlers (wired to UI Button OnClick in Main_Menu.unity).</summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string firstMapSceneName = "First_MAP";

        public void GoToFirstMap()
        {
            SoundManager.Instance?.PlayButtonClickSFX();
            SceneManager.LoadScene(firstMapSceneName);
        }

        public void ExitGame()
        {
            SoundManager.Instance?.PlayButtonClickSFX();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
