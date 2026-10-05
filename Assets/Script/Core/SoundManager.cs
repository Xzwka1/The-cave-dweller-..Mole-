using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaveDweller.Core
{
    /// <summary>
    /// SoundManager Singleton — must exist in Main_Menu and be DontDestroyOnLoad.
    /// IMPORTANT: Do NOT call Instance before every Awakened scene has had the
    /// chance to place its SoundManager prefab; otherwise audio silently drops.
    /// No lazy GetComponent-ghost: if there is no backing AudioSource/Clip the
    /// call must LogWarning (not hide) so that broken content never stays muted.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager instance;
        private static bool noInstanceWarnedOnce;

        public static SoundManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SoundManager>();
                    if (instance == null && !noInstanceWarnedOnce)
                    {
                        noInstanceWarnedOnce = true;
                        Debug.LogWarning("[SoundManager] No SoundManager in active scene. "
                            + "SFX calls will be silenced. Place a SoundManager prefab in Main_Menu "
                            + "(DontDestroyOnLoad) so that it reaches First_MAP.");
                    }
                }
                return instance;
            }
        }

        public static bool TryGetInstance(out SoundManager result)
        {
            result = instance != null ? instance : FindAnyObjectByType<SoundManager>();
            return result != null;
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music Tracks (BGM)")]
        [SerializeField] private AudioClip victoryMusic;
        [SerializeField] private AudioClip mainMenuMusic;
        [SerializeField] private AudioClip inGameMusic;

        [Header("Ambience Tracks")]
        [SerializeField] private AudioClip[] caveAmbientClips;

        [Header("Volume Settings")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;

        [Header("Sound Effects (SFX)")]
        [SerializeField] private AudioClip gunshotSFX;
        [SerializeField] private AudioClip gunReloadSFX;
        [Tooltip("Sequential reload sounds played one after the other to extend reload duration.")]
        [SerializeField] private AudioClip[] gunReloadSequenceClips;
        [SerializeField] private float reloadSequenceDelay = 0f;
        [SerializeField] private AudioClip playerJumpSFX;
        [SerializeField] private AudioClip playerDashSFX;
        [SerializeField] private AudioClip playerFootstepSFX;
        [SerializeField] private AudioClip playerHurtSFX;
        [SerializeField] private AudioClip playerDeathSFX;
        [SerializeField] private AudioClip monsterMeleeSFX;
        [SerializeField] private AudioClip monsterRangedSFX;
        [SerializeField] private AudioClip monsterDeathSFX;
        [SerializeField] private AudioClip buttonClickSFX;

        [Header("Sound Effects Variations (Randomized playback)")]
        [SerializeField] private AudioClip[] gunshotSFXClips;
        [SerializeField] private AudioClip[] jumpSFXClips;
        [SerializeField] private AudioClip[] dashSFXClips;
        [SerializeField] private AudioClip[] footstepSFXClips;
        [SerializeField] private AudioClip[] monsterMeleeSFXClips;
        [SerializeField] private AudioClip[] monsterRangedSFXClips;
        [SerializeField] private AudioClip[] monsterDeathSFXClips;
        [SerializeField] private AudioClip[] buttonClickSFXClips;

        // Procedural fallbacks
        private AudioClip procGunshot;
        private AudioClip procGunReload;
        private AudioClip procJump;
        private AudioClip procDash;
        private AudioClip procFootstep;
        private AudioClip procHurt;
        private AudioClip procDeath;
        private AudioClip procMonsterMelee;
        private AudioClip procMonsterRanged;
        private AudioClip procClick;
        private AudioClip procCaveAmbient;

        private Coroutine fadeCoroutine;
        private Coroutine ambientFadeCoroutine;
        private Coroutine ambientPlaylistRoutine;
        private Coroutine reloadSequenceCoroutine;

        public float BGMVolume => bgmVolume;
        public float AmbientVolume => ambientVolume;
        public float SFXVolume => sfxVolume;
        public AudioClip VictoryMusic => victoryMusic;
        public AudioClip InGameMusic => inGameMusic;
        public AudioClip[] CaveAmbientClips => caveAmbientClips;
        public AudioClip CaveAmbientMusic => (caveAmbientClips != null && caveAmbientClips.Length > 0) ? caveAmbientClips[0] : null;
        public AudioClip[] GunReloadSequenceClips => gunReloadSequenceClips;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            EnsureClipsLoaded();
        }

        private void Start()
        {
            // When entering play mode directly in the editor, ensure scene audio kicks in
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void InitializeAudioSources()
        {
            if (bgmSource == null)
            {
                var bgmObj = new GameObject("BGMSource");
                bgmObj.transform.SetParent(transform);
                bgmSource = bgmObj.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.volume = bgmVolume;
                bgmSource.spatialBlend = 0f; // 2D Stereo
            }

            if (ambientSource == null)
            {
                var ambObj = new GameObject("AmbientSource");
                ambObj.transform.SetParent(transform);
                ambientSource = ambObj.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
                ambientSource.volume = ambientVolume;
                ambientSource.spatialBlend = 0f; // 2D Stereo
            }

            if (sfxSource == null)
            {
                var sfxObj = new GameObject("SFXSource");
                sfxObj.transform.SetParent(transform);
                sfxSource = sfxObj.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.volume = sfxVolume;
                sfxSource.spatialBlend = 0f; // 2D Stereo
            }
        }

        private void EnsureClipsLoaded()
        {
#if UNITY_EDITOR
            if (victoryMusic == null)
                victoryMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music_Game/Win_End_Game.mp3");
            if (mainMenuMusic == null)
                mainMenuMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Main Manu.mp3");
            if (inGameMusic == null)
                inGameMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/BG game.mp3");
            if (caveAmbientClips == null || caveAmbientClips.Length == 0)
            {
                var a1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Ambient/mavopix-deep-cave-159876.mp3");
                var a2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Ambient/mavopix-forgotten-cave-159880.mp3");
                var a3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Ambient/mavopix-monsters-cave-159887.mp3");
                var a4 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Ambient/soundreality-scary-cave-392041.mp3");
                var ambList = new System.Collections.Generic.List<AudioClip>();
                if (a1 != null) ambList.Add(a1);
                if (a2 != null) ambList.Add(a2);
                if (a3 != null) ambList.Add(a3);
                if (a4 != null) ambList.Add(a4);
                if (ambList.Count > 0) caveAmbientClips = ambList.ToArray();
            }

            // SFX - Gunshot
            if (gunshotSFX == null)
                gunshotSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/เสียงปืน 1.mp3")
                          ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/เสียงปืน.mp3");

            if (gunshotSFXClips == null || gunshotSFXClips.Length == 0)
            {
                var g1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/เสียงปืน 1.mp3");
                var g2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/เสียงปืน.mp3");
                if (g1 != null && g2 != null) gunshotSFXClips = new[] { g1, g2 };
                else if (g1 != null) gunshotSFXClips = new[] { g1 };
            }

            // SFX - Gun Reload
            if (gunReloadSFX == null)
                gunReloadSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/ump45_reload_tactical.ogg");

            if (gunReloadSequenceClips == null || gunReloadSequenceClips.Length == 0)
            {
                var r1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/ump45_reload_tactical.ogg");
                var r2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/ump45_reload_empty.ogg");
                var list = new System.Collections.Generic.List<AudioClip>();
                if (r1 != null) list.Add(r1);
                if (r2 != null) list.Add(r2);
                if (list.Count > 0) gunReloadSequenceClips = list.ToArray();
            }

            // SFX - Jump
            if (playerJumpSFX == null)
                playerJumpSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Jump/กระโดด 2.mp3")
                             ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Jump/กระโดด.mp3")
                             ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/กระโดด.mp3");

            if (jumpSFXClips == null || jumpSFXClips.Length == 0)
            {
                var j1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Jump/กระโดด.mp3");
                var j2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Jump/กระโดด 2.mp3");
                var j3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Jump/กระโดด 3.mp3");
                var j4 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Jump/กระโดด 4.mp3");
                var list = new System.Collections.Generic.List<AudioClip>();
                if (j1 != null) list.Add(j1);
                if (j2 != null) list.Add(j2);
                if (j3 != null) list.Add(j3);
                if (j4 != null) list.Add(j4);
                if (list.Count > 0) jumpSFXClips = list.ToArray();
            }

            // SFX - Dash
            if (playerDashSFX == null)
                playerDashSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/dash 1.mp3")
                             ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/dash.mp3");

            if (dashSFXClips == null || dashSFXClips.Length == 0)
            {
                var d1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/dash 1.mp3");
                var d2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/dash.mp3");
                if (d1 != null && d2 != null) dashSFXClips = new[] { d1, d2 };
                else if (d1 != null) dashSFXClips = new[] { d1 };
            }

            // SFX - Footstep
            if (playerFootstepSFX == null)
                playerFootstepSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Walk/Walk1.wav")
                                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/เดิน.mp3");

            if (footstepSFXClips == null || footstepSFXClips.Length == 0)
            {
                var w1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Walk/Walk1.wav");
                var w2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Walk/Walk2.wav");
                var w3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Walk/Walk3.wav");
                var list = new System.Collections.Generic.List<AudioClip>();
                if (w1 != null) list.Add(w1);
                if (w2 != null) list.Add(w2);
                if (w3 != null) list.Add(w3);
                if (list.Count > 0) footstepSFXClips = list.ToArray();
            }

            // SFX - Button Click
            if (buttonClickSFX == null)
                buttonClickSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/click sound 1.mp3")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/click sound.mp3");

            if (buttonClickSFXClips == null || buttonClickSFXClips.Length == 0)
            {
                var c1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/click sound 1.mp3");
                var c2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/click sound.mp3");
                if (c1 != null && c2 != null) buttonClickSFXClips = new[] { c1, c2 };
                else if (c1 != null) buttonClickSFXClips = new[] { c1 };
            }

            // SFX - Monster
            if (monsterMeleeSFX == null)
                monsterMeleeSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_1.mp3")
                               ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/sound monster.mp3");
            if (monsterRangedSFX == null)
                monsterRangedSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_2.mp3")
                                ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_3.mp3")
                                ?? monsterMeleeSFX;

            if (monsterMeleeSFXClips == null || monsterMeleeSFXClips.Length == 0)
            {
                var m1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_1.mp3");
                var m2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_2.mp3");
                var m3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_3.mp3");
                var list = new System.Collections.Generic.List<AudioClip>();
                if (m1 != null) list.Add(m1);
                if (m2 != null) list.Add(m2);
                if (m3 != null) list.Add(m3);
                if (list.Count > 0) monsterMeleeSFXClips = list.ToArray();
            }

            if (monsterRangedSFXClips == null || monsterRangedSFXClips.Length == 0)
            {
                var m2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_2.mp3");
                var m3 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/Monster/sound-monster_3.mp3");
                var list = new System.Collections.Generic.List<AudioClip>();
                if (m2 != null) list.Add(m2);
                if (m3 != null) list.Add(m3);
                if (list.Count > 0) monsterRangedSFXClips = list.ToArray();
            }

            // SFX - Monster Death
            if (monsterDeathSFX == null)
                monsterDeathSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/enemydie1.mp3");

            if (monsterDeathSFXClips == null || monsterDeathSFXClips.Length == 0)
            {
                var ed1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/enemydie1.mp3");
                var ed2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound_Effect/enemydie2.mp3");
                var list = new System.Collections.Generic.List<AudioClip>();
                if (ed1 != null) list.Add(ed1);
                if (ed2 != null) list.Add(ed2);
                if (list.Count > 0) monsterDeathSFXClips = list.ToArray();
            }
#endif
            if (victoryMusic == null)
            {
                victoryMusic = Resources.Load<AudioClip>("Music_Game/Win_End_Game");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Assign Audio Clips")]
        public void AutoAssignClips()
        {
            EnsureClipsLoaded();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string sceneName = scene.name;

            if (sceneName.Equals("Winning_Map", System.StringComparison.OrdinalIgnoreCase))
            {
                StopAmbience(0.5f);
                PlayVictoryMusic();
            }
            else if (sceneName.Equals("Main_Menu", System.StringComparison.OrdinalIgnoreCase))
            {
                StopAmbience(0.5f);
                if (mainMenuMusic != null)
                {
                    PlayBGM(mainMenuMusic, true, 0.8f);
                }
            }
            else if (sceneName.StartsWith("First", System.StringComparison.OrdinalIgnoreCase) ||
                     sceneName.StartsWith("Second", System.StringComparison.OrdinalIgnoreCase) ||
                     sceneName.StartsWith("Third", System.StringComparison.OrdinalIgnoreCase) ||
                     sceneName.Equals("code", System.StringComparison.OrdinalIgnoreCase))
            {
                // Play In-Game Music
                if (inGameMusic != null)
                {
                    PlayBGM(inGameMusic, true, 0.8f);
                }

                // Play Cave Ambience (randomly from list)
                PlayCaveAmbience(1.0f);
            }
        }

        public void PlayBGM(AudioClip clip, bool loop = true, float fadeDuration = 0.5f)
        {
            if (clip == null) return;
            if (bgmSource == null) InitializeAudioSources();

            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(CoCrossFadeBGM(clip, loop, fadeDuration));
        }

        public void StopBGM(float fadeDuration = 0.5f)
        {
            if (bgmSource == null || !bgmSource.isPlaying) return;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(CoFadeOutBGM(fadeDuration));
        }

        public void PlayCaveAmbience(float fadeDuration = 0.5f)
        {
            if (caveAmbientClips == null || caveAmbientClips.Length == 0) return;
            if (ambientPlaylistRoutine != null && ambientSource != null && ambientSource.isPlaying) return;

            if (ambientPlaylistRoutine != null)
            {
                StopCoroutine(ambientPlaylistRoutine);
                ambientPlaylistRoutine = null;
            }

            ambientPlaylistRoutine = StartCoroutine(CoCaveAmbientLoop(fadeDuration));
        }

        public void PlayAmbience(AudioClip clip, bool loop = true, float fadeDuration = 0.5f)
        {
            if (ambientPlaylistRoutine != null)
            {
                StopCoroutine(ambientPlaylistRoutine);
                ambientPlaylistRoutine = null;
            }

            if (clip == null) return;
            if (ambientSource == null) InitializeAudioSources();

            if (ambientSource.clip == clip && ambientSource.isPlaying) return;

            if (ambientFadeCoroutine != null) StopCoroutine(ambientFadeCoroutine);
            ambientFadeCoroutine = StartCoroutine(CoCrossFadeAmbience(clip, loop, fadeDuration));
        }

        public void StopAmbience(float fadeDuration = 0.5f)
        {
            if (ambientPlaylistRoutine != null)
            {
                StopCoroutine(ambientPlaylistRoutine);
                ambientPlaylistRoutine = null;
            }

            if (ambientSource == null || !ambientSource.isPlaying) return;

            if (ambientFadeCoroutine != null) StopCoroutine(ambientFadeCoroutine);
            ambientFadeCoroutine = StartCoroutine(CoFadeOutAmbience(fadeDuration));
        }

        public void PlayVictoryMusic()
        {
            EnsureClipsLoaded();

            if (victoryMusic != null)
            {
                Debug.Log("[SoundManager] Playing Victory Music (Win_End_Game.mp3)");
                PlayBGM(victoryMusic, true, 0.5f);
            }
            else
            {
                Debug.LogWarning("[SoundManager] Victory music clip not found.");
            }
        }

        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            if (sfxSource == null) InitializeAudioSources();
            if (sfxSource == null)
            {
                Debug.LogWarning("[SoundManager] sfxSource is null — cannot play SFX.");
                return;
            }

            sfxSource.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeMultiplier));
        }

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, sfxVolume * Mathf.Clamp01(volumeMultiplier));
        }

        // ==========================================
        // SFX Trigger Methods (with variations & procedural fallback)
        // ==========================================
        private AudioClip GetRandomClip(AudioClip[] clips, AudioClip fallback)
        {
            if (clips != null && clips.Length > 0)
            {
                int idx = Random.Range(0, clips.Length);
                if (clips[idx] != null) return clips[idx];
            }
            return fallback;
        }

        public void PlayGunshotSFX()
        {
            var clip = GetRandomClip(gunshotSFXClips, gunshotSFX) ?? (procGunshot ??= CreateProceduralGunshot());
            PlaySFX(clip, 0.9f);
        }

        public void PlayGunReloadSFX()
        {
            if (reloadSequenceCoroutine != null)
            {
                StopCoroutine(reloadSequenceCoroutine);
                reloadSequenceCoroutine = null;
            }

            if (gunReloadSequenceClips != null && gunReloadSequenceClips.Length > 0)
            {
                reloadSequenceCoroutine = StartCoroutine(CoPlayReloadSequence());
            }
            else
            {
                var clip = gunReloadSFX != null ? gunReloadSFX : (procGunReload ??= CreateProceduralGunReload());
                PlaySFX(clip, 0.85f);
            }
        }

        public void StopGunReloadSFX()
        {
            if (reloadSequenceCoroutine != null)
            {
                StopCoroutine(reloadSequenceCoroutine);
                reloadSequenceCoroutine = null;
            }
        }

        private IEnumerator CoPlayReloadSequence()
        {
            for (int i = 0; i < gunReloadSequenceClips.Length; i++)
            {
                var clip = gunReloadSequenceClips[i];
                if (clip == null) continue;

                PlaySFX(clip, 0.85f);

                if (i < gunReloadSequenceClips.Length - 1)
                {
                    yield return new WaitForSeconds(clip.length + reloadSequenceDelay);
                }
            }

            reloadSequenceCoroutine = null;
        }

        public float GetReloadDuration()
        {
            if (gunReloadSequenceClips != null && gunReloadSequenceClips.Length > 0)
            {
                float total = 0f;
                for (int i = 0; i < gunReloadSequenceClips.Length; i++)
                {
                    var c = gunReloadSequenceClips[i];
                    if (c != null)
                    {
                        total += c.length;
                        if (i < gunReloadSequenceClips.Length - 1)
                        {
                            total += reloadSequenceDelay;
                        }
                    }
                }
                return total > 0f ? total : 2.5f;
            }
            return gunReloadSFX != null ? gunReloadSFX.length : 2.5f;
        }

        public void PlayJumpSFX()
        {
            var clip = GetRandomClip(jumpSFXClips, playerJumpSFX) ?? (procJump ??= CreateProceduralJump());
            PlaySFX(clip, 0.75f);
        }

        public void PlayDashSFX()
        {
            var clip = GetRandomClip(dashSFXClips, playerDashSFX) ?? (procDash ??= CreateProceduralDash());
            PlaySFX(clip, 0.85f);
        }

        public void PlayFootstepSFX()
        {
            var clip = GetRandomClip(footstepSFXClips, playerFootstepSFX) ?? (procFootstep ??= CreateProceduralFootstep());
            PlaySFX(clip, 0.45f);
        }

        public void PlayPlayerHurtSFX()
        {
            var clip = playerHurtSFX != null ? playerHurtSFX : (procHurt ??= CreateProceduralHurt());
            PlaySFX(clip, 1.0f);
        }

        public void PlayPlayerDeathSFX()
        {
            var clip = playerDeathSFX != null ? playerDeathSFX : (procDeath ??= CreateProceduralDeath());
            PlaySFX(clip, 1.0f);
        }

        public void PlayMonsterMeleeSFX()
        {
            var clip = GetRandomClip(monsterMeleeSFXClips, monsterMeleeSFX) ?? (procMonsterMelee ??= CreateProceduralMonsterMelee());
            PlaySFX(clip, 0.85f);
        }

        public void PlayMonsterRangedSFX()
        {
            var clip = GetRandomClip(monsterRangedSFXClips, monsterRangedSFX) ?? (procMonsterRanged ??= CreateProceduralMonsterRanged());
            PlaySFX(clip, 0.85f);
        }

        public void PlayMonsterDeathSFX()
        {
            var clip = GetRandomClip(monsterDeathSFXClips, monsterDeathSFX);
            if (clip != null)
            {
                PlaySFX(clip, 0.95f);
            }
        }

        public void PlayEnemyDieSFX() => PlayMonsterDeathSFX();

        public void PlayButtonClickSFX()
        {
            var clip = GetRandomClip(buttonClickSFXClips, buttonClickSFX) ?? (procClick ??= CreateProceduralClick());
            PlaySFX(clip, 0.8f);
        }

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        private IEnumerator CoCrossFadeBGM(AudioClip newClip, bool loop, float duration)
        {
            float startVol = bgmSource.volume;

            if (bgmSource.isPlaying && duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                    yield return null;
                }
            }

            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.loop = loop;
            bgmSource.Play();

            if (duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t / duration);
                    yield return null;
                }
            }

            bgmSource.volume = bgmVolume;
            fadeCoroutine = null;
        }

        private IEnumerator CoFadeOutBGM(float duration)
        {
            float startVol = bgmSource.volume;

            if (duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                    yield return null;
                }
            }

            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = bgmVolume;
            fadeCoroutine = null;
        }

        public void SetAmbientVolume(float volume)
        {
            ambientVolume = Mathf.Clamp01(volume);
            if (ambientSource != null) ambientSource.volume = ambientVolume;
        }

        private IEnumerator CoCrossFadeAmbience(AudioClip newClip, bool loop, float duration)
        {
            float startVol = ambientSource.volume;

            if (ambientSource.isPlaying && duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    ambientSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                    yield return null;
                }
            }

            ambientSource.Stop();
            ambientSource.clip = newClip;
            ambientSource.loop = loop;
            ambientSource.Play();

            if (duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    ambientSource.volume = Mathf.Lerp(0f, ambientVolume, t / duration);
                    yield return null;
                }
            }

            ambientSource.volume = ambientVolume;
            ambientFadeCoroutine = null;
        }

        private IEnumerator CoFadeOutAmbience(float duration)
        {
            float startVol = ambientSource.volume;

            if (duration > 0f)
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    ambientSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                    yield return null;
                }
            }

            ambientSource.Stop();
            ambientSource.clip = null;
            ambientSource.volume = ambientVolume;
            ambientFadeCoroutine = null;
        }

        private IEnumerator CoCaveAmbientLoop(float fadeDuration)
        {
            while (true)
            {
                if (caveAmbientClips == null || caveAmbientClips.Length == 0) yield break;

                AudioClip clip = caveAmbientClips[Random.Range(0, caveAmbientClips.Length)];
                if (clip == null) yield break;

                yield return StartCoroutine(CoCrossFadeAmbience(clip, false, fadeDuration));

                while (ambientSource != null && ambientSource.isPlaying)
                {
                    yield return null;
                }
            }
        }

        // ==========================================
        // Procedural Audio Generators (Zero External Files)
        // ==========================================
        private AudioClip CreateProceduralGunshot()
        {
            int sampleRate = 44100;
            float duration = 0.16f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-26f * t);
                float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-16f * t) * 0.6f;
                samples[i] = Mathf.Clamp(noise + thump, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_Gunshot", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralGunReload()
        {
            int sampleRate = 44100;
            float duration = 0.65f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sound = 0f;

                // Click 1: Magazine eject at ~0.05s
                if (t >= 0.05f && t < 0.12f)
                {
                    float dt = t - 0.05f;
                    sound += Mathf.Sin(2f * Mathf.PI * 1200f * dt) * Mathf.Exp(-80f * dt) * 0.4f;
                    sound += (Random.value * 2f - 1f) * Mathf.Exp(-120f * dt) * 0.3f;
                }

                // Click 2: Magazine insert at ~0.30s
                if (t >= 0.30f && t < 0.40f)
                {
                    float dt = t - 0.30f;
                    sound += Mathf.Sin(2f * Mathf.PI * 800f * dt) * Mathf.Exp(-60f * dt) * 0.5f;
                    sound += (Random.value * 2f - 1f) * Mathf.Exp(-90f * dt) * 0.35f;
                }

                // Click 3: Bolt rack/slide rack at ~0.50s
                if (t >= 0.50f && t < 0.62f)
                {
                    float dt = t - 0.50f;
                    sound += Mathf.Sin(2f * Mathf.PI * 1500f * dt) * Mathf.Exp(-70f * dt) * 0.45f;
                    sound += (Random.value * 2f - 1f) * Mathf.Exp(-100f * dt) * 0.3f;
                }

                samples[i] = Mathf.Clamp(sound, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_GunReload", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralJump()
        {
            int sampleRate = 44100;
            float duration = 0.13f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(220f, 480f, t / duration);
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.45f;
            }

            var clip = AudioClip.Create("Proc_Jump", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralDash()
        {
            int sampleRate = 44100;
            float duration = 0.20f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / duration));
                float noise = (Random.value * 2f - 1f) * env * 0.5f;
                float pitch = Mathf.Sin(2f * Mathf.PI * 300f * (1f - t / duration) * t) * env * 0.25f;
                samples[i] = Mathf.Clamp(noise + pitch, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_Dash", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralFootstep()
        {
            int sampleRate = 44100;
            float duration = 0.05f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-40f * t);
                float low = Mathf.Sin(2f * Mathf.PI * 90f * t) * env * 0.35f;
                float noise = (Random.value * 2f - 1f) * env * 0.15f;
                samples[i] = low + noise;
            }

            var clip = AudioClip.Create("Proc_Footstep", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralHurt()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(220f, 95f, t / duration);
                float env = Mathf.Exp(-12f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (Random.value * 2f - 1f) * 0.35f;
                samples[i] = Mathf.Clamp((tone + noise) * env * 0.6f, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_Hurt", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralDeath()
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(180f, 40f, t / duration);
                float env = Mathf.Exp(-4f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float sub = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.5f;
                samples[i] = Mathf.Clamp((tone + sub) * env * 0.7f, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_Death", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralMonsterMelee()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-10f * t);
                float growl = Mathf.Sin(2f * Mathf.PI * 130f * t) * Mathf.Sin(2f * Mathf.PI * 45f * t);
                float noise = (Random.value * 2f - 1f) * 0.4f;
                samples[i] = Mathf.Clamp((growl + noise) * env * 0.65f, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_MonsterMelee", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralMonsterRanged()
        {
            int sampleRate = 44100;
            float duration = 0.20f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(600f, 150f, t / duration);
                float env = Mathf.Exp(-12f * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
            }

            var clip = AudioClip.Create("Proc_MonsterRanged", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralClick()
        {
            int sampleRate = 44100;
            float duration = 0.04f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-70f * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.4f;
            }

            var clip = AudioClip.Create("Proc_Click", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralCaveAmbient()
        {
            int sampleRate = 44100;
            float duration = 3.0f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Eerie low drone + resonant cave tone
                float lowDrone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.2f;
                float hollow = Mathf.Sin(2f * Mathf.PI * 110f * t + Mathf.Sin(2f * Mathf.PI * 0.3f * t)) * 0.12f;
                float sub = Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.15f;
                samples[i] = Mathf.Clamp(lowDrone + hollow + sub, -1f, 1f);
            }

            var clip = AudioClip.Create("Proc_CaveAmbient", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
