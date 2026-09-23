using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CaveDweller.Combat;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CaveDweller.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        public enum GameState
        {
            Playing,
            Won,
            Lost
        }

        [Header("Singleton Settings")]
        [SerializeField] private bool persistAcrossScenes = false;

        [Header("Player Reference")]
        [SerializeField] private GameObject playerObject;
        [SerializeField] private string playerTag = "Player";

        [Header("UI References")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject hudRoot;

        [Header("Time Control")]
        [SerializeField] private float loseTimeScale = 0f;
        [SerializeField] private float winTimeScale = 0f;
        [SerializeField][Range(0f, 1f)] private float loseSlowMotionScale = 0.2f;
        [SerializeField][Min(0f)] private float slowMotionDuration = 0.6f;

        [Header("Exit Detection")]
        [SerializeField] private string exitDoorTag = "ExitDoor";

        [Header("Restart Input")]
        [SerializeField] private bool allowRestartInput = true;

        private GameState currentState = GameState.Playing;
        private float originalTimeScale = 1f;
        private Coroutine flowRoutine;

        public static GameFlowManager Instance { get; private set; }

        public GameState CurrentState => currentState;
        public bool IsGameOver => currentState != GameState.Playing;
        public bool HasWon => currentState == GameState.Won;
        public bool HasLost => currentState == GameState.Lost;
        public GameObject PlayerObject => playerObject;
        public GameObject GameOverPanel => gameOverPanel;
        public GameObject WinPanel => winPanel;
        public GameObject HudRoot => hudRoot;
        public string ExitDoorTag => exitDoorTag;
        public string PlayerTag => playerTag;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            originalTimeScale = Time.timeScale;

            if (TryGetComponent<BoxCollider2D>(out var triggerCollider))
            {
                triggerCollider.isTrigger = true;
            }

            if (TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.useFullKinematicContacts = true;
            }

            SetPanelActive(gameOverPanel, false);
            SetPanelActive(winPanel, false);
        }

        private void Start()
        {
            if (playerObject == null && !string.IsNullOrEmpty(playerTag))
            {
                try
                {
                    var found = GameObject.FindGameObjectWithTag(playerTag);
                    if (found != null)
                    {
                        playerObject = found;
                    }
                }
                catch (UnityException)
                {
                    Debug.LogWarning("[GameFlowManager] Player tag is not defined, assign the player manually.", this);
                }
            }

            if (playerObject != null)
            {
                var damageable = playerObject.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    Debug.LogWarning("[GameFlowManager] Player object has no IDamageable component, death must be reported via OnPlayerDied().", this);
                }
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] No player assigned or found, continuing without player reference.", this);
            }
        }

        private void OnValidate()
        {
            loseSlowMotionScale = Mathf.Clamp01(loseSlowMotionScale);
            if (slowMotionDuration < 0f)
            {
                slowMotionDuration = 0f;
            }
        }

        private void Update()
        {
            ReadRestartInput();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;
            if (currentState != GameState.Playing) return;

            if (!string.IsNullOrEmpty(exitDoorTag) && other.CompareTag(exitDoorTag))
            {
                OnPlayerReachedExit();
                return;
            }

            if (!string.IsNullOrEmpty(playerTag)
                && !string.IsNullOrEmpty(exitDoorTag)
                && other.CompareTag(playerTag)
                && CompareTag(exitDoorTag))
            {
                OnPlayerReachedExit();
            }
        }

        public void OnPlayerDied()
        {
            if (currentState != GameState.Playing) return;

            currentState = GameState.Lost;
            Debug.Log("[GameFlowManager] Player died. Game over.", this);

            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }

            flowRoutine = StartCoroutine(CoPlayLoseSequence());
        }

        public void OnPlayerReachedExit()
        {
            if (currentState != GameState.Playing) return;

            currentState = GameState.Won;
            Debug.Log("[GameFlowManager] Player reached exit. You win.", this);

            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }

            flowRoutine = StartCoroutine(CoPlayWinSequence());
        }

        public void ReloadScene()
        {
            Time.timeScale = 1f;

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogWarning("[GameFlowManager] Active scene is invalid, cannot reload.", this);
                return;
            }

            SceneManager.LoadScene(activeScene.buildIndex);
        }

        public void RestartGame()
        {
            ReloadScene();
        }

        private IEnumerator CoPlayLoseSequence()
        {
            Time.timeScale = Mathf.Clamp01(loseSlowMotionScale);

            if (slowMotionDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(slowMotionDuration);
            }

            Time.timeScale = loseTimeScale;

            SetPanelActive(hudRoot, false);
            SetPanelActive(winPanel, false);
            SetPanelActive(gameOverPanel, true);

            flowRoutine = null;
        }

        private IEnumerator CoPlayWinSequence()
        {
            Time.timeScale = winTimeScale;

            SetPanelActive(hudRoot, false);
            SetPanelActive(gameOverPanel, false);
            SetPanelActive(winPanel, true);

            yield return null;

            flowRoutine = null;
        }

        private void ReadRestartInput()
        {
            if (!allowRestartInput) return;
            if (currentState == GameState.Playing) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                ReloadScene();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                ReloadScene();
            }
#else
            if (Input.GetKeyDown(KeyCode.R))
            {
                ReloadScene();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                ReloadScene();
            }
#endif
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel == null) return;
            panel.SetActive(active);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = originalTimeScale;
            }
        }
    }
}
