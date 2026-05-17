using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Ia muy simple para el prefab Skeleton: sigue en XZ al jugador y ventana melee con trigger delante.</summary>
    [DisallowMultipleComponent]
    public sealed class SkeletonMeleeAi : MonoBehaviour
    {
        const string HitboxObjName = "MeleeHitbox";

        [SerializeField] Transform m_PlayerTargetOverride;
        [SerializeField] float m_ChaseSpeed = 2.2f;
        [SerializeField] float m_TurnSpeedDegrees = 280f;
        [SerializeField] float m_AttackRange = 1.55f;
        [SerializeField] float m_AttackWarmupBias = -0.12f;
        [SerializeField] float m_MeleeDamageWindow = 0.36f;
        [SerializeField] float m_AttackCooldownSeconds = 1.95f;
        [SerializeField] float m_MeleeDamage = 11f;
        [SerializeField] float m_MinHitIntervalToPlayer = 0.72f;
        [SerializeField] string m_AnimatorSpeedFloatName = "Speed";
        [SerializeField] string m_AttackTriggerName = "RightAttack";

        [Header("Separación (evita que copias converjan al mismo punto)")]
        [SerializeField] float m_SeparationRadius = 1.15f;
        [SerializeField] float m_SeparationWeight = 1.35f;
        [SerializeField] int m_OverlapBufferSize = 24;

        Rigidbody m_Rb;
        Animator m_Animator;
        EnemyTestDummy m_EnemyHealth;
        EnemyMeleeHitbox m_Hitbox;
        Transform m_Target;
        float m_NextAttackTime;
        float m_MeleeEndsTime;
        bool m_MeleeWindow;
        Collider[] m_NeighborBuffer;

        void Awake()
        {
            m_Rb = GetComponent<Rigidbody>();
            m_Animator = GetComponentInChildren<Animator>();
            m_EnemyHealth = GetComponent<EnemyTestDummy>();
            m_Hitbox = GetComponentInChildren<EnemyMeleeHitbox>(true);
            if (m_Hitbox == null)
                m_Hitbox = CreateHitbox();
            m_Hitbox.Configure(this, m_MeleeDamage, m_MinHitIntervalToPlayer);
            m_NeighborBuffer = new Collider[Mathf.Clamp(m_OverlapBufferSize, 8, 64)];
        }

        EnemyMeleeHitbox CreateHitbox()
        {
            var existing = transform.Find(HitboxObjName);
            if (existing != null)
            {
                var existHit = existing.GetComponent<EnemyMeleeHitbox>();
                if (existHit != null)
                    return existHit;
                return existing.gameObject.AddComponent<EnemyMeleeHitbox>();
            }

            var hb = new GameObject(HitboxObjName);
            hb.layer = gameObject.layer;
            hb.transform.SetParent(transform, false);
            hb.transform.localPosition = new Vector3(0f, 1.05f, 0.5f);
            hb.transform.localRotation = Quaternion.identity;
            var bx = hb.AddComponent<BoxCollider>();
            bx.isTrigger = true;
            bx.center = Vector3.zero;
            bx.size = new Vector3(1.15f, 1f, 0.85f);
            return hb.AddComponent<EnemyMeleeHitbox>();
        }

        void OnDisable()
        {
            StopMeleeVisual();
            if (m_Hitbox != null)
                m_Hitbox.EnableDamageWindow(false);
            if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                m_Animator.SetFloat(m_AnimatorSpeedFloatName, 0f);
        }

        void Update()
        {
            if (!IsOperational())
            {
                if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                    m_Animator.SetFloat(m_AnimatorSpeedFloatName, 0f);
                return;
            }

            ResolveTarget();
            if (m_Target == null)
            {
                if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                    m_Animator.SetFloat(m_AnimatorSpeedFloatName, 0f);
                return;
            }

            var planar = m_Target.position - m_Rb.position;
            planar.y = 0f;
            float distPlanar = planar.magnitude;
            Vector3 planarDir = distPlanar > 0.05f ? planar / distPlanar : Vector3.forward;
            Vector3 separation = ComputeSeparationPush();
            Vector3 combined = planarDir + separation;
            combined.y = 0f;
            Vector3 moveDir = combined.sqrMagnitude > 1e-6f ? combined.normalized : planarDir;

            if (distPlanar > 0.05f)
            {
                var look = Quaternion.LookRotation(planarDir, Vector3.up);
                m_Rb.MoveRotation(Quaternion.RotateTowards(m_Rb.rotation, look, m_TurnSpeedDegrees * Time.deltaTime));
            }

            float locomotion = Mathf.Clamp01(distPlanar / Mathf.Max(0.05f, m_ChaseSpeed * 4f));
            if (distPlanar <= m_AttackRange)
                locomotion *= 0.25f;

            if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                m_Animator.SetFloat(m_AnimatorSpeedFloatName, locomotion);

            float stopDistance = Mathf.Max(0f, m_AttackRange + m_AttackWarmupBias);
            if (distPlanar > stopDistance)
            {
                float stepMag = Mathf.Min(m_ChaseSpeed * Time.deltaTime, distPlanar - stopDistance);
                var step = moveDir * stepMag;
                m_Rb.MovePosition(m_Rb.position + new Vector3(step.x, 0f, step.z));
            }

            if (distPlanar <= m_AttackRange && Time.time >= m_NextAttackTime)
                StartAttackPulse();

            if (m_MeleeWindow && Time.time >= m_MeleeEndsTime)
                StopMeleeVisual();
        }

        bool IsOperational() => m_EnemyHealth != null && m_EnemyHealth.IsAlive && m_Rb != null;

        void ResolveTarget()
        {
            if (m_PlayerTargetOverride != null)
                m_Target = m_PlayerTargetOverride;
            else
            {
                var ph = Object.FindFirstObjectByType<PlayerHealth>();
                m_Target = ph != null ? ph.transform : null;
            }
        }

        Vector3 ComputeSeparationPush()
        {
            if (m_SeparationRadius <= 0f || m_SeparationWeight <= 0f || m_NeighborBuffer == null)
                return Vector3.zero;

            int found = Physics.OverlapSphereNonAlloc(
                m_Rb.position,
                m_SeparationRadius,
                m_NeighborBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);
            Vector3 sum = Vector3.zero;
            int neighbors = 0;
            for (int i = 0; i < found; i++)
            {
                var c = m_NeighborBuffer[i];
                if (c == null)
                    continue;
                var otherAi = c.GetComponentInParent<SkeletonMeleeAi>();
                if (otherAi == null || otherAi == this || !otherAi.enabled)
                    continue;
                Vector3 delta = m_Rb.position - c.bounds.center;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist < 1e-4f)
                    continue;
                float t = 1f - Mathf.Clamp01(dist / m_SeparationRadius);
                sum += (delta / dist) * (t * t);
                neighbors++;
            }
            if (neighbors == 0 || sum.sqrMagnitude < 1e-10f)
                return Vector3.zero;
            sum.Normalize();
            return sum * m_SeparationWeight;
        }

        void StartAttackPulse()
        {
            m_NextAttackTime = Time.time + m_AttackCooldownSeconds;
            m_MeleeEndsTime = Time.time + m_MeleeDamageWindow;
            m_MeleeWindow = true;
            if (m_Hitbox != null)
            {
                m_Hitbox.EnableDamageWindow(true);
                m_Hitbox.ResetHitCooldownGate();
            }
            if (m_Animator != null && !string.IsNullOrEmpty(m_AttackTriggerName))
                m_Animator.SetTrigger(m_AttackTriggerName);
        }

        void StopMeleeVisual()
        {
            m_MeleeWindow = false;
            if (m_Hitbox != null)
                m_Hitbox.EnableDamageWindow(false);
        }
    }

    /// <summary>Trigger delantero temporal controlado por <see cref="SkeletonMeleeAi"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyMeleeHitbox : MonoBehaviour
    {
        SkeletonMeleeAi m_OwnerAi;
        float m_DamageAmount;
        float m_HitCooldown;
        bool m_WindowEnabled;
        float m_NextHitTime;

        public void Configure(SkeletonMeleeAi owner, float dmg, float minIntervalSeconds)
        {
            m_OwnerAi = owner;
            m_DamageAmount = dmg;
            m_HitCooldown = Mathf.Max(0.05f, minIntervalSeconds);
            EnableDamageWindow(false);
        }

        public void EnableDamageWindow(bool on) => m_WindowEnabled = on;

        public void ResetHitCooldownGate() => m_NextHitTime = 0f;

        void OnTriggerStay(Collider other)
        {
            if (!m_WindowEnabled)
                return;
            if (!DamageableUtility.TryGetDamageable(other, out var d) || d is not PlayerHealth ph)
                return;
            if (ShieldController.IsAnyShieldBlockingPlayerRoot(other.transform))
            {
                CombatLog.Log("Escudo: bloqueaste el ataque del esqueleto", "Shield");
                return;
            }
            if (!d.IsAlive || Time.time < m_NextHitTime)
                return;

            GameObject inst = m_OwnerAi != null ? m_OwnerAi.gameObject : gameObject;
            Vector3 hp = other.ClosestPoint(transform.position);
            Vector3 n = hp - transform.position;
            if (n.sqrMagnitude < 1e-6f)
                n = transform.forward;
            var info = DamageInfo.Make(DamageSource.EnemyMelee, m_DamageAmount, hp, n, other, inst);
            ph.TakeDamage(m_DamageAmount, in info);
            m_NextHitTime = Time.time + m_HitCooldown;
            CombatLog.Log($"Esqueleto: impacto melee al jugador (-{m_DamageAmount:F0})", "Enemy");
        }
    }
}
