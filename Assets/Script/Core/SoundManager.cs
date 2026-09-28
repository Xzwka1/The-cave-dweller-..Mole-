using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaveDweller.Core
{
    /// <summary>
    /// SoundManager Singleton (DontDestroyOnLoad).
    /// Handles BGM cross-fading, victory music, and all game SFX.
    /// Provides high-quality procedural audio synthesis fallback if audio assets from PDM are not yet loaded.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager instance;

        public static SoundManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<SoundManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("SoundManager");
                        instance = go.AddComponent<SoundManager>();
                    }
                }
                return instance;
            }
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music Tracks (BGM)")]
        [SerializeField] private AudioClip victoryMusic;
        [SerializeField] private AudioClip mainMenuMusic;
        [SerializeField] private AudioClip caveAmbientMusic;

        [Header("Volume Settings")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;

        [Header("Sound Effects (SFX)")]
        [SerializeField] private AudioClip gunshotSFX;
        [SerializeField] private AudioClip playerJumpSFX;
        [SerializeField] private AudioClip playerDashSFX;
        [SerializeField] private AudioClip playerFootstepSFX;
        [SerializeField] private AudioClip playerHurtSFX;
        [SerializeField] private AudioClip playerDeathSFX;
        [SerializeField] private AudioClip monsterMeleeSFX;
        [SerializeField] private AudioClip monsterRangedSFX;
        [SerializeField] private AudioClip buttonClickSFX;

        // Procedural fallbacks
        private AudioClip procGunshot;
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

        public float BGMVolume => bgmVolume;
        public float SFXVolume => sfxVolume;
        public AudioClip VictoryMusic => victoryMusic;

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
            EnsureVictoryClipLoaded();
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

        private void EnsureVictoryClipLoaded()
        {
            if (victoryMusic == null)
            {
                // Try load from Resources or AssetDatabase if running
#if UNITY_EDITOR
                victoryMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music_Game/Win_End_Game.mp3");
#endif
                if (victoryMusic == null)
                {
                    victoryMusic = Resources.Load<AudioClip>("Music_Game/Win_End_Game");
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string sceneName = scene.name;

            if (sceneName.Equals("Winning_Map", System.StringComparison.OrdinalIgnoreCase))
            {
                PlayVictoryMusic();
            }
            else if (sceneName.Equals("Main_Menu", System.StringComparison.OrdinalIgnoreCase))
            {
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
                var amb = caveAmbientMusic != null ? caveAmbientMusic : (procCaveAmbient ??= CreateProceduralCaveAmbient());
                PlayBGM(amb, true, 1.0f);
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

        public void PlayVictoryMusic()
        {
            EnsureVictoryClipLoaded();

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

            sfxSource.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeMultiplier));
        }

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, sfxVolume * Mathf.Clamp01(volumeMultiplier));
        }

        // ==========================================
        // SFX Trigger Methods (with procedural fallback)
        // ==========================================
        public void PlayGunshotSFX()
        {
            var clip = gunshotSFX != null ? gunshotSFX : (procGunshot ??= CreateProceduralGunshot());
            PlaySFX(clip, 0.9f);
        }

        public void PlayJumpSFX()
        {
            var clip = playerJumpSFX != null ? playerJumpSFX : (procJump ??= CreateProceduralJump());
            PlaySFX(clip, 0.7f);
        }

        public void PlayDashSFX()
        {
            var clip = playerDashSFX != null ? playerDashSFX : (procDash ??= CreateProceduralDash());
            PlaySFX(clip, 0.85f);
        }

        public void PlayFootstepSFX()
        {
            var clip = playerFootstepSFX != null ? playerFootstepSFX : (procFootstep ??= CreateProceduralFootstep());
            PlaySFX(clip, 0.35f);
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
            var clip = monsterMeleeSFX != null ? monsterMeleeSFX : (procMonsterMelee ??= CreateProceduralMonsterMelee());
            PlaySFX(clip, 0.85f);
        }

        public void PlayMonsterRangedSFX()
        {
            var clip = monsterRangedSFX != null ? monsterRangedSFX : (procMonsterRanged ??= CreateProceduralMonsterRanged());
            PlaySFX(clip, 0.85f);
        }

        public void PlayButtonClickSFX()
        {
            var clip = buttonClickSFX != null ? buttonClickSFX : (procClick ??= CreateProceduralClick());
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
