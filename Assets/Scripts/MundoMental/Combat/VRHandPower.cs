using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MundoMental.VR.Combat
{
    /// <summary>Disparo de energía desde la mano cuando no hay arma. No sustituye a <c>LaunchProjectile</c> del VRTemplate.</summary>
    [AddComponentMenu("Mundo Mental VR/VR Hand Power")]
    public class VRHandPower : MonoBehaviour
    {
        [Header("Proyectil")]
        [SerializeField] EnergyProjectile m_ProjectilePrefab;
        [SerializeField] Transform m_Muzzle;
        [SerializeField] [Tooltip("Velocidad inicial del proyectil (unidades/s)")] float m_ProjectileSpeed = 18f;
        [SerializeField] float m_CooldownSeconds = 0.35f;
        [SerializeField] [Tooltip("Capa con la que chocan las esferas de energía (debe excluir al jugador si hace falta)")] LayerMask m_HitMask = ~0;
        [SerializeField] int m_ProjectileLayerOverride = -1;
        [SerializeField] [Tooltip("Daño al impactar (IDamageable)")] float m_Damage = 6f;
        [SerializeField] [Tooltip("Escala forzada del proyectil al instanciar (evita bolas enormes)")] float m_ProjectileScale = 0.08f;
        [Header("Efectos opcionales")]
        [SerializeField] ParticleSystem m_MuzzleVfx;
        [SerializeField] AudioSource m_Audio;
        [SerializeField] AudioClip m_FireClip;
        [SerializeField] bool m_DrawDebug = true;
        [SerializeField] [Tooltip("Usado como instigador y para ignorar colisiones consigo mismo")] Transform m_InstigatorRoot;

        float m_NextTime;

        public bool TryFire()
        {
            if (m_Muzzle == null)
            {
                CombatLog.Warn("VRHandPower: muzzle no asignado.", "HandEnergy");
                return false;
            }

            if (Time.time < m_NextTime)
            {
                CombatLog.Log("HandEnergy: en cooldown, disparo ignorado", "HandEnergy");
                return false;
            }

            if (m_ProjectilePrefab == null)
            {
                CombatLog.Warn("VRHandPower: asigna EnergyProjectile prefab.", "HandEnergy");
                return false;
            }

            m_NextTime = Time.time + m_CooldownSeconds;
            var go = Instantiate(m_ProjectilePrefab, m_Muzzle.position, m_Muzzle.rotation);
            if (m_ProjectileScale > 0f)
                go.transform.localScale = Vector3.one * m_ProjectileScale;
            var p = go.GetComponent<EnergyProjectile>();
            if (p != null)
            {
                var instigator = m_InstigatorRoot != null ? m_InstigatorRoot.gameObject : m_Muzzle.root.gameObject;
                p.Launch(
                    m_Muzzle.forward * m_ProjectileSpeed,
                    m_Damage,
                    m_HitMask,
                    instigator,
                    m_ProjectileLayerOverride,
                    DamageSource.HandEnergy);
            }
            if (m_MuzzleVfx != null)
                m_MuzzleVfx.Play();
            if (m_Audio != null && m_FireClip != null)
                m_Audio.PlayOneShot(m_FireClip);
            if (m_DrawDebug)
                Debug.DrawRay(m_Muzzle.position, m_Muzzle.forward * 2f, Color.cyan, 0.5f, true);
            CombatLog.Log("HandEnergy: disparo lanzado", "HandEnergy");
            return true;
        }
    }
}
