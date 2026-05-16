using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Colócalo en un hijo con Collider (is trigger) a lo largo de la hoja.</summary>
    [AddComponentMenu("Mundo Mental VR/Sword Blade Trigger")]
    [RequireComponent(typeof(Collider))]
    public class SwordBladeTrigger : MonoBehaviour
    {
        [SerializeField] SwordWeapon m_Sword;
        Rigidbody m_SwordRootBody;
        Vector3 m_LastPosition;
        bool m_HasLastPosition;

        void Awake()
        {
            if (m_Sword == null)
                m_Sword = GetComponentInParent<SwordWeapon>();
            m_SwordRootBody = m_Sword != null ? m_Sword.Rigidbody : null;
        }

        void OnTriggerEnter(Collider other)
        {
            if (m_Sword == null)
                return;
            var v = ComputeBladeVelocity();
            if (v.sqrMagnitude < 0.0001f)
                v = transform.forward * 0.5f;
            m_Sword.ApplyDamageTo(other, v);
        }

        void OnTriggerStay(Collider other)
        {
            if (m_Sword == null)
                return;
            var v = ComputeBladeVelocity();
            m_Sword.ApplyDamageTo(other, v);
        }

        void OnCollisionEnter(Collision collision)
        {
            ApplyCollisionDamage(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            ApplyCollisionDamage(collision);
        }

        void ApplyCollisionDamage(Collision collision)
        {
            if (m_Sword == null || collision.contactCount == 0)
                return;
            if (collision.relativeVelocity.sqrMagnitude < 0.0001f)
                return;
            var col = collision.collider;
            if (col == null)
                return;
            m_Sword.ApplyDamageTo(col, collision.relativeVelocity);
        }

        Vector3 ComputeBladeVelocity()
        {
            if (m_SwordRootBody != null && m_SwordRootBody.linearVelocity.sqrMagnitude > 0.0001f)
                return m_SwordRootBody.linearVelocity;

            if (!m_HasLastPosition)
            {
                m_LastPosition = transform.position;
                m_HasLastPosition = true;
                return Vector3.zero;
            }

            var delta = transform.position - m_LastPosition;
            m_LastPosition = transform.position;
            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            return delta / dt;
        }
    }
}
