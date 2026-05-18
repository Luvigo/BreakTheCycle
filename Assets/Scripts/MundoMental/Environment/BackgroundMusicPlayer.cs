using UnityEngine;

namespace MundoMental.VR.Environment
{
    /// <summary>Reproduce música de fondo en loop (2D).</summary>
    [DisallowMultipleComponent]
    public sealed class BackgroundMusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip m_Clip;
        [SerializeField] [Range(0f, 1f)] float m_Volume = 0.35f;
        [SerializeField] bool m_PlayOnEnable = true;

        AudioSource m_Source;

        void Awake() => EnsureSource();

        void OnEnable()
        {
            EnsureSource();
            if (m_PlayOnEnable)
                Play();
        }

        void EnsureSource()
        {
            if (m_Source == null)
                m_Source = GetComponent<AudioSource>();
            if (m_Source == null)
                m_Source = gameObject.AddComponent<AudioSource>();

            m_Source.playOnAwake = false;
            m_Source.loop = true;
            m_Source.spatialBlend = 0f;
            m_Source.volume = m_Volume;
            m_Source.clip = m_Clip;
        }

        public void Play()
        {
            if (m_Source == null || m_Source.clip == null)
                return;
            if (!m_Source.isPlaying)
                m_Source.Play();
        }
    }
}
