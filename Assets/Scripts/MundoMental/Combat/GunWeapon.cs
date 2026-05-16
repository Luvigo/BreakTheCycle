using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Pistola agarrable. Dispara con rayos; la entrada la delega <see cref="MundoMentalVrCombat"/>.</summary>
    public class GunWeapon : MonoBehaviour, IWeapon
    {
        [SerializeField] string m_WeaponId = "pistol_01";
        [SerializeField] Transform m_Muzzle;
        [SerializeField] float m_Damage = 8f;
        [SerializeField] float m_Cooldown = 0.2f;
        [SerializeField] float m_Range = 30f;
        [SerializeField] LayerMask m_HitMask = ~0;
        [Header("Efecto opcional")]
        [SerializeField] ParticleSystem m_MuzzleVfx;
        [SerializeField] AudioSource m_Audio;
        [SerializeField] AudioClip m_FireClip;
        [SerializeField] [Tooltip("GameObject instigador (mano/arma) para capas lógicas")] GameObject m_Instigator;

        public string WeaponId => m_WeaponId;
        public Transform Muzzle => m_Muzzle;
        public float LastFireTime { get; private set; } = -999f;
        public float FireCooldown => m_Cooldown;

        public bool TryFire()
        {
            if (m_Muzzle == null)
            {
                CombatLog.Warn("Pistola: muzzle faltante.", "Gun");
                return false;
            }

            if (Time.time < LastFireTime + m_Cooldown)
            {
                CombatLog.Log("Pistola: en cooldown", "Gun");
                return false;
            }

            LastFireTime = Time.time;
            var origin = m_Muzzle.position;
            var direction = m_Muzzle.forward;
            if (Physics.Raycast(origin, direction, out var hit, m_Range, m_HitMask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider != null)
                {
                    if (DamageableUtility.TryGetDamageable(hit.collider, out var d))
                    {
                        var go = m_Instigator != null ? m_Instigator : gameObject;
                        var info = DamageInfo.Make(
                            DamageSource.Gun,
                            m_Damage,
                            hit.point,
                            hit.normal,
                            hit.collider,
                            go);
                        d.TakeDamage(m_Damage, in info);
                        CombatLog.Log($"Pistola: impacto a {hit.collider.gameObject.name}", "Gun");
                    }
                }
            }
            else
            {
                CombatLog.Log("Pistola: disparo (sin impacto en rango)", "Gun");
            }

            if (m_MuzzleVfx != null)
                m_MuzzleVfx.Play();
            if (m_Audio != null && m_FireClip != null)
                m_Audio.PlayOneShot(m_FireClip);
            return true;
        }
    }
}
