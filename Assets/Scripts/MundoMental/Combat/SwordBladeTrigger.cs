using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Daño melee: suele llevar collider físico/trigger consistente con el Rig del arma. Opcionalmente genera hijo BladeHit como trigger volumétrico.</summary>
    [AddComponentMenu("Mundo Mental VR/Sword Blade Trigger")]
    [DisallowMultipleComponent]
    public class SwordBladeTrigger : MonoBehaviour
    {
        [SerializeField] SwordWeapon m_Sword;
        [Header("Strike volume (recommended)")]
        [SerializeField] bool m_SpawnStrikeTriggerChild = true;
        [SerializeField] string m_StrikeChildName = "BladeHit";
        [SerializeField] Vector3 m_StrikeLocalCenter = new Vector3(0f, 0f, 0.25f);
        [SerializeField] Vector3 m_StrikeLocalHalfExtents = new Vector3(0.09f, 0.22f, 0.35f);

        Rigidbody m_SwordRootBody;
        Vector3 m_LastPosition;
        bool m_HasLastPosition;

        void Awake()
        {
            EnsureStrikeChildIfNeeded();

            if (m_Sword == null)
                m_Sword = GetComponentInParent<SwordWeapon>();

            if (m_Sword == null)
                enabled = false;
            else
                m_SwordRootBody = m_Sword.Rigidbody;
        }

        void EnsureStrikeChildIfNeeded()
        {
            if (!m_SpawnStrikeTriggerChild)
                return;
            if (!string.IsNullOrEmpty(m_StrikeChildName) && transform.Find(m_StrikeChildName) != null)
                return;

            var ch = new GameObject(string.IsNullOrEmpty(m_StrikeChildName) ? "BladeHit" : m_StrikeChildName);
            ch.layer = gameObject.layer;
            ch.transform.SetParent(transform, false);
            ch.transform.localRotation = Quaternion.identity;
            var box = ch.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = m_StrikeLocalCenter;
            box.size = m_StrikeLocalHalfExtents * 2f;
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
