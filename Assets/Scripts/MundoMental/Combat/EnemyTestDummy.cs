using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Dummy con vida para probar daño. Requiere un Collider (no trigger) en un hijo o raíz.</summary>
    [DisallowMultipleComponent]
    public class EnemyTestDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] float m_MaxHealth = 30f;
        [SerializeField] bool m_LogEachHit = true;
        [SerializeField] [Tooltip("Opcional: flash / ocultar al morir (MeshRenderer o SkinnedMeshRenderer)")] Renderer m_Visual;
        [SerializeField] [Tooltip("Color al recibir impacto (opcional)")] bool m_Flash = true;
        [SerializeField] Color m_DamageColor = Color.red;
        Color m_Initial;
        float m_Health;
        public bool IsAlive => m_Health > 0f;
        public float Health => m_Health;
        public float MaxHealth => m_MaxHealth;

        void Awake()
        {
            m_Health = m_MaxHealth;
            if (m_Visual != null)
                m_Initial = m_Visual.material.color;
        }

        public void TakeDamage(float amount, in DamageInfo info)
        {
            if (!IsAlive || amount <= 0f)
                return;
            m_Health = Mathf.Max(0f, m_Health - amount);
            if (m_LogEachHit)
            {
                CombatLog.Log(
                    $"Dummy: daño {info.Source} -{amount:F1} | vida {m_Health:F0}/{m_MaxHealth:F0} @ {name}",
                    "Enemy");
            }
            if (m_Flash && m_Visual != null)
            {
                m_Visual.material.color = m_DamageColor;
                CancelInvoke(nameof(ResetColor));
                Invoke(nameof(ResetColor), 0.12f);
            }
            if (m_Health <= 0f)
                Die();
        }

        void ResetColor()
        {
            if (m_Visual != null)
                m_Visual.material.color = m_Initial;
        }

        void Die()
        {
            CombatLog.Log($"Dummy: {name} derrotado (fin prueba de daño).", "Enemy");
            if (m_Visual != null)
                m_Visual.enabled = false;
        }
    }
}
