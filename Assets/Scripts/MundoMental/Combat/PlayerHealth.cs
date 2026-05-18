using MundoMental.VR;
using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Vida del jugador (XR Origin): hitbox física secundaria compatible con triggers de enemigos. El Character Controller no sirve solo para física de arma/enemigos.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        const string DefaultHurtChildName = "MeleeHurtVolume";

        [SerializeField] float m_MaxHealth = 100f;
        [SerializeField] bool m_LogDamage = true;
        [SerializeField] bool m_CreateHud = true;
        [SerializeField] bool m_CreateVictoryUi = true;
        [SerializeField] [Tooltip("Si no es null, la barra de vida se parenta a este Transform (ej. Right Controller).")] Transform m_HealthHudHandAnchor;
        [SerializeField] float m_HurtCapsuleRadius = 0.28f;
        [SerializeField] float m_HurtCapsuleHeight = 1.62f;
        [SerializeField] Vector3 m_HurtCapsuleCenter = new Vector3(0f, 0.9f, 0f);
        [SerializeField] [Tooltip("Layer del volumen de daño. Default (0) para que los triggers enemigos en Default lo detecten; el XR Rig a veces está en Ignore Raycast (2).")]
        int m_HurtVolumePhysicsLayer = 0;

        float m_Current;

        public event System.Action<float, float> HealthChanged;

        public bool IsAlive => m_Current > 0f;
        public float Health => m_Current;
        public float MaxHealth => m_MaxHealth;

        void Awake()
        {
            m_Current = m_MaxHealth;
            EnsureHurtVolume();
            RaiseChanged();
        }

        public void ResetHealthToFull()
        {
            m_Current = m_MaxHealth;
            RaiseChanged();
        }

        void RaiseChanged() => HealthChanged?.Invoke(m_Current, m_MaxHealth);

        void Start()
        {
            VrAudioListenerUtility.EnsureSingleListenerOnRigCamera();

            if (m_CreateHud)
            {
                var hud = GetComponent<PlayerHealthHud>();
                if (hud == null)
                    hud = gameObject.AddComponent<PlayerHealthHud>();
                hud.SetFollowHand(m_HealthHudHandAnchor);
            }
            if (m_CreateVictoryUi && FindFirstObjectByType<AllSkeletonsClearedMenu>() == null)
                gameObject.AddComponent<AllSkeletonsClearedMenu>();
        }

        void EnsureHurtVolume()
        {
            var existingTf = transform.Find(DefaultHurtChildName);
            if (existingTf != null)
            {
                if (m_HurtVolumePhysicsLayer >= 0)
                    existingTf.gameObject.layer = m_HurtVolumePhysicsLayer;
                return;
            }

            var go = new GameObject(DefaultHurtChildName);
            go.layer = m_HurtVolumePhysicsLayer >= 0 ? m_HurtVolumePhysicsLayer : gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = m_HurtCapsuleCenter;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = m_HurtCapsuleRadius;
            cap.height = m_HurtCapsuleHeight;
            cap.center = Vector3.zero;
            cap.isTrigger = false;
        }

        public void TakeDamage(float amount, in DamageInfo info)
        {
            if (!IsAlive || amount <= 0f)
                return;
            m_Current = Mathf.Max(0f, m_Current - amount);
            RaiseChanged();
            if (m_LogDamage)
            {
                CombatLog.Log($"Jugador: -{amount:F1} ({info.Source}) | vida {m_Current:F0}/{m_MaxHealth:F0}", "Player");
            }
            if (m_Current <= 0f)
                Die();
        }

        void Die()
        {
            CombatLog.Log("Jugador derrotado (prototipo).", "Player");
        }
    }
}
