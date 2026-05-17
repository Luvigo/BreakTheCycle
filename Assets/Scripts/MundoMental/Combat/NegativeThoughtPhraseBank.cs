using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Banco editable de frases para “pensamientos negativos”. Asigna un asset en <see cref="NegativeThoughtActor"/>.</summary>
    [CreateAssetMenu(fileName = "NegativeThoughtPhraseBank", menuName = "Mundo Mental/Pensamiento negativo/Frases", order = 0)]
    public sealed class NegativeThoughtPhraseBank : ScriptableObject
    {
        [SerializeField] string[] m_Phrases =
        {
            "No vas a poder.",
            "Siempre fallas igual.",
            "No mereces que te presten atención."
        };

        public int PhraseCount => m_Phrases != null ? m_Phrases.Length : 0;

        public string PickRandom()
        {
            if (m_Phrases == null || m_Phrases.Length == 0)
                return string.Empty;
            int guard = 0;
            while (guard++ < 32)
            {
                var p = m_Phrases[Random.Range(0, m_Phrases.Length)];
                if (!string.IsNullOrWhiteSpace(p))
                    return p.Trim();
            }
            return string.Empty;
        }
    }
}
