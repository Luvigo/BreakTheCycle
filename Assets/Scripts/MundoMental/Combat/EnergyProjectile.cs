using System.Collections;
using UnityEngine;

namespace MundoMental.VR.Combat
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnergyProjectile : MonoBehaviour
    {
        [SerializeField] float m_LifeSeconds = 4f;
        [SerializeField] [Tooltip("Multiplicador colisión/continuo (opcional)")] bool m_ExplodeOnHit = true;
        Rigidbody m_Body;
        bool m_Started;

        float m_Damage;
        [SerializeField] [Tooltip("Asignada en Launch (filtro futuro)")] LayerMask m_HitFilter;
        GameObject m_Instigator;
        DamageSource m_Source;
        int m_ForcedLayer = -1;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
        }

        /// <summary>Configuración post-instanciación (prefab reutilizable).</summary>
        public void Launch(
            Vector3 worldVelocity,
            float damage,
            LayerMask hitMask,
            GameObject instigator,
            int overrideLayer = -1,
            DamageSource source = DamageSource.HandEnergy)
        {
            m_HitFilter = hitMask;
            m_Damage = damage;
            m_Instigator = instigator;
            m_Source = source;
            m_ForcedLayer = overrideLayer;
            m_Body.useGravity = false;
            m_Body.linearVelocity = worldVelocity;
            m_Started = true;
            if (m_ForcedLayer >= 0)
                gameObject.layer = m_ForcedLayer;
            StartCoroutine(Despawn());
        }

        IEnumerator Despawn()
        {
            yield return new WaitForSeconds(m_LifeSeconds);
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!m_Started)
                return;
            if (m_Instigator != null && other.transform.IsChildOf(m_Instigator.transform))
                return;

            if (other.GetComponentInParent<ShieldController>() is { } shield && shield.IsBlocking)
            {
                shield.RegisterBlock();
                CombatLog.Log("Escudo: proyectil de energía bloqueado", "Shield");
                if (m_ExplodeOnHit)
                    Destroy(gameObject);
                return;
            }

            if (DamageableUtility.TryGetDamageable(other, out var d))
            {
                var info = DamageInfo.Make(
                    m_Source,
                    m_Damage,
                    other.ClosestPoint(transform.position),
                    -transform.forward,
                    other,
                    m_Instigator);
                d.TakeDamage(m_Damage, in info);
                CombatLog.Log($"Proyectil: impacto → {other.gameObject.name}", "Projectile");
            }

            if (m_ExplodeOnHit)
                Destroy(gameObject);
        }
    }
}
