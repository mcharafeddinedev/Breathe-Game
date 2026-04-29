using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Plays bubble pop/blow sounds with random clip selection and pitch variation.
    /// Attach to the BubbleWandController or a dedicated audio object in the Bubbles scene.
    /// </summary>
    public sealed class BubbleSfxPlayer : MonoBehaviour
    {
        [Header("Bubble Clips (assign in inspector or auto-load from Resources)")]
        [SerializeField] AudioClip[] _bubbleClips;

        [Header("Pitch Variation")]
        [SerializeField, Range(0.7f, 1.0f)] float _minPitch = 0.85f;
        [SerializeField, Range(1.0f, 1.5f)] float _maxPitch = 1.25f;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float _volume = 0.5f;
        [SerializeField, Tooltip("Volume reduction for gentle/small bubbles.")]
        float _gentleBubbleVolumeMult = 0.4f;

        [Header("Pooling")]
        [SerializeField] int _poolSize = 8;

        AudioSource[] _pool;
        int _poolIndex;

        static BubbleSfxPlayer _instance;
        public static BubbleSfxPlayer Instance => _instance;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            LoadClipsIfNeeded();
            BuildPool();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Assign clips at runtime (called by BubbleWandController if clips are assigned there).
        /// </summary>
        public void SetClips(AudioClip[] clips, float volume)
        {
            if (clips != null && clips.Length > 0)
            {
                _bubbleClips = clips;
                _volume = volume;
            }
        }

        void LoadClipsIfNeeded()
        {
            if (_bubbleClips != null && _bubbleClips.Length > 0) return;

            // Clips must live under a Resources folder, e.g. Assets/Resources/Audio/ (path below omits "Resources/")
            var clip1 = Resources.Load<AudioClip>("Audio/nomentero_bubble1");
            var clip2 = Resources.Load<AudioClip>("Audio/benzix2_bubbel2");

            if (clip1 != null || clip2 != null)
            {
                var clips = new System.Collections.Generic.List<AudioClip>();
                if (clip1 != null) clips.Add(clip1);
                if (clip2 != null) clips.Add(clip2);
                _bubbleClips = clips.ToArray();
            }

            if (_bubbleClips == null || _bubbleClips.Length == 0)
                Debug.LogWarning("[BubbleSfxPlayer] No bubble clips assigned or found in Resources/Audio.");
        }

        /// <summary>Call after SetClips or if Awake may have run before clips existed (no-op when already loaded).</summary>
        public void EnsureClipsLoaded() => LoadClipsIfNeeded();

        void BuildPool()
        {
            _pool = new AudioSource[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"BubbleSfx_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D
                src.loop = false;
                _pool[i] = src;
            }
        }

        /// <summary>
        /// Play a random bubble sound with pitch variation.
        /// </summary>
        /// <param name="isSweetSpot">True for full-size sweet spot bubbles, false for gentle/small bubbles.</param>
        public void PlayBubble(bool isSweetSpot = true)
        {
            if (_bubbleClips == null || _bubbleClips.Length == 0) return;

            var src = _pool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _pool.Length;

            // Random clip selection
            src.clip = _bubbleClips[Random.Range(0, _bubbleClips.Length)];

            // Random pitch within range
            src.pitch = Random.Range(_minPitch, _maxPitch);

            // Volume based on bubble type, with SFX setting
            float vol = _volume * SfxLinear();
            if (!isSweetSpot)
                vol *= _gentleBubbleVolumeMult;
            
            // Slight random volume variation
            vol *= Random.Range(0.85f, 1.0f);
            src.volume = vol;

            src.Play();
        }

        static float SfxLinear() => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear));

        /// <summary>
        /// Play bubble sound with custom pitch multiplier (stacks with random variation).
        /// </summary>
        public void PlayBubbleWithPitch(float pitchMult, bool isSweetSpot = true)
        {
            if (_bubbleClips == null || _bubbleClips.Length == 0) return;

            var src = _pool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _pool.Length;

            src.clip = _bubbleClips[Random.Range(0, _bubbleClips.Length)];
            src.pitch = Random.Range(_minPitch, _maxPitch) * pitchMult;

            float vol = _volume * SfxLinear() * (isSweetSpot ? 1f : _gentleBubbleVolumeMult);
            vol *= Random.Range(0.85f, 1.0f);
            src.volume = vol;

            src.Play();
        }

        /// <summary>
        /// Play a splash/pop sound (higher pitch, for aggressive blowing).
        /// </summary>
        public void PlaySplash()
        {
            if (_bubbleClips == null || _bubbleClips.Length == 0) return;

            var src = _pool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _pool.Length;

            src.clip = _bubbleClips[Random.Range(0, _bubbleClips.Length)];
            // Higher pitch for splash effect
            src.pitch = Random.Range(1.3f, 1.6f);
            src.volume = _volume * 0.6f * SfxLinear();

            src.Play();
        }
    }
}
