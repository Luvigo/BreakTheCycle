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
        [SerializeField] [Tooltip("Opcional: parámetro Damage trigger en Animator esqueleto")] bool m_UseAnimatorTriggers = true;
        [SerializeField] Animator m_Animator;
        [SerializeField] string m_TriggerDamage = "Damage";
        [SerializeField] string m_TriggerDeath = "Death";
        Color m_Initial;
        float m_Health;
        public bool IsAlive => m_Health > 0f;
        public float Health => m_Health;
        public float MaxHealth => m_MaxHealth;

        /// <summary>Se invoca al morir (vida &lt;= 0).</summary>
        public static event System.Action<EnemyTestDummy> Died;

        void Awake()
        {
            m_Health = m_MaxHealth;
            TryResolveAnimator();
            TryResolveVisual();
            if (m_Visual != null)
                m_Initial = m_Visual.material.color;
        }

        void TryResolveAnimator()
        {
            if (m_Animator == null)
                m_Animator = GetComponentInChildren<Animator>();
        }

        void TryResolveVisual()
        {
            if (m_Visual != null)
                return;
            m_Visual = GetComponentInChildren<SkinnedMeshRenderer>();
            if (m_Visual == null)
                m_Visual = GetComponentInChildren<MeshRenderer>();
        }

        public void TakeDamage(float amount, in DamageInfo info)
        {
            if (!IsAlive || amount <= 0f)
                return;
            m_Health = Mathf.Max(0f, m_Health - amount);
            TryResolveAnimator();
            if (m_UseAnimatorTriggers && m_Animator != null && !string.IsNullOrEmpty(m_TriggerDamage))
                m_Animator.SetTrigger(m_TriggerDamage);
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
            Died?.Invoke(this);
            var melee = GetComponent<SkeletonMeleeAi>();
            if (melee != null)
                melee.enabled = false;
            TryResolveAnimator();
            if (m_UseAnimatorTriggers && m_Animator != null && !string.IsNullOrEmpty(m_TriggerDeath))
                m_Animator.SetTrigger(m_TriggerDeath);
            if (m_Visual != null)
                m_Visual.enabled = false;
        }
    }
}
