using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Raíz lógica de la espada. Coloca un hijo con trigger (espada) y <see cref="SwordBladeTrigger"/> o usa este script en el cuerpo con Rigidbody.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SwordWeapon : MonoBehaviour, IWeapon
    {
        [SerializeField] string m_WeaponId = "sword_01";
        [SerializeField] DamageSource m_DamageSource = DamageSource.Sword;
        [SerializeField] float m_Damage = 10f;
        [SerializeField] [Tooltip("Velocidad mínima (m/s) en la cuchilla para contar corte")] float m_MinBladeSpeed = 0.35f;
        [SerializeField] [Tooltip("Enemigos: por golpe, cooldown hacia el mismo cuerpo")] float m_HitIntervalPerTarget = 0.35f;
        Rigidbody m_Body;
        float m_NextTimeForTarget;
        int m_LastTargetId;
        [SerializeField] [Tooltip("Instigador (opcional)")] GameObject m_Instigator;

        public string WeaponId => m_WeaponId;

        public Rigidbody Rigidbody => m_Body;

        public float GetBladeSpeed(Vector3 pointVelocity) => pointVelocity.magnitude;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
        }

        public bool CanDamageNow(Collider other, Vector3 pointVelocity, out string reason)
        {
            reason = null;
            if (pointVelocity.magnitude < m_MinBladeSpeed)
            {
                reason = "velocidad baja (no es golpe)";
                return false;
            }

            if (!DamageableUtility.TryGetDamageable(other, out var td) || !td.IsAlive)
            {
                reason = "no es dañable";
                return false;
            }

            return CanHitTarget(other.gameObject, out reason);
        }

        public void ApplyDamageTo(Collider other, Vector3 pointVelocity)
        {
            if (!CanDamageNow(other, pointVelocity, out var reason))
            {
                CombatLog.Log($"{m_WeaponId}: impacto ignorado ({reason}) sobre {other.gameObject.name}", "Sword");
                return;
            }
            if (!DamageableUtility.TryGetDamageable(other, out var d))
                return;
            var go = m_Instigator != null ? m_Instigator : gameObject;
            var info = DamageInfo.Make(
                m_DamageSource,
                m_Damage,
                other.ClosestPoint(transform.position),
                other.transform.position - transform.position,
                other,
                go);
            d.TakeDamage(m_Damage, in info);
            RegisterHitOn(other.gameObject);
            CombatLog.Log($"{m_WeaponId}: golpe a {other.gameObject.name} (v={pointVelocity.magnitude:F2})", "Sword");
        }

        bool CanHitTarget(GameObject target, out string reason)
        {
            var id = target.GetInstanceID();
            if (id == m_LastTargetId && Time.time < m_NextTimeForTarget)
            {
                reason = "i-frames (mismo blanco)";
                return false;
            }
            reason = null;
            return true;
        }

        void RegisterHitOn(GameObject target)
        {
            m_LastTargetId = target.GetInstanceID();
            m_NextTimeForTarget = Time.time + m_HitIntervalPerTarget;
        }
    }
}
