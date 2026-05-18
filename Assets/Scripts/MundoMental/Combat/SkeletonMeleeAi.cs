using System;
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
        [SerializeField] [Tooltip("Si es false, anima/ persigue pero no quita vida (ej. Boss decorativo).")]
        bool m_CanDealMeleeDamage = true;

        [Header("Separación (evita que copias converjan al mismo punto)")]
        [SerializeField] float m_SeparationRadius = 1.15f;
        [SerializeField] float m_SeparationWeight = 1.35f;
        [SerializeField] int m_OverlapBufferSize = 24;

        [Header("Suelo del mapa")]
        [SerializeField] bool m_SnapToWalkableGround = true;
        [SerializeField] float m_FootHeightOffset;
        [SerializeField] float m_GroundRayStartHeight = 4f;
        [SerializeField] float m_GroundRayMaxDistance = 14f;
        [SerializeField] float m_MinGroundNormalY = 0.35f;
        [SerializeField] float m_MaxGroundSnapPerFrame = 0.45f;

        Rigidbody m_Rb;
        Animator m_Animator;
        EnemyTestDummy m_EnemyHealth;
        EnemyMeleeHitbox m_Hitbox;
        Transform m_Target;
        float m_NextAttackTime;
        float m_MeleeEndsTime;
        bool m_MeleeWindow;
        Collider[] m_NeighborBuffer;
        CapsuleCollider m_BodyCapsule;
        float m_LastAnimSpeed;

        public bool CanDealMeleeDamage => m_CanDealMeleeDamage;

        void Awake()
        {
            if (gameObject.name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                m_CanDealMeleeDamage = false;
                m_ChaseSpeed = 0f;
                m_SnapToWalkableGround = false;
            }

            m_Rb = GetComponent<Rigidbody>();
            m_Animator = GetComponentInChildren<Animator>();
            m_EnemyHealth = GetComponent<EnemyTestDummy>();
            m_Hitbox = GetComponentInChildren<EnemyMeleeHitbox>(true);
            if (m_Hitbox == null)
                m_Hitbox = CreateHitbox();
            m_Hitbox.Configure(this, m_MeleeDamage, m_MinHitIntervalToPlayer);
            m_NeighborBuffer = new Collider[Mathf.Clamp(m_OverlapBufferSize, 8, 64)];
            m_BodyCapsule = GetComponent<CapsuleCollider>();
            if (m_Rb != null)
            {
                m_Rb.useGravity = false;
                m_Rb.isKinematic = true;
                m_Rb.interpolation = RigidbodyInterpolation.None;
                m_Rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
            if (m_BodyCapsule != null)
                m_BodyCapsule.isTrigger = true;
            if (m_SnapToWalkableGround)
                WalkableMapGroundSetup.PrepareSceneWalkableGround();
            IgnoreWalkableGroundCollisions();

            if (!m_CanDealMeleeDamage)
                DisableMeleeHitbox();
        }

        void DisableMeleeHitbox()
        {
            if (m_Hitbox == null)
                return;
            m_Hitbox.EnableDamageWindow(false);
            var col = m_Hitbox.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        void IgnoreWalkableGroundCollisions()
        {
            if (m_BodyCapsule == null)
                return;
            var markers = FindObjectsByType<WalkableGroundMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                var col = markers[i].GetComponent<Collider>();
                if (col != null)
                    Physics.IgnoreCollision(m_BodyCapsule, col, true);
            }
        }

        void OnEnable()
        {
            if (m_SnapToWalkableGround)
                SnapToGroundImmediate();
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
            hb.transform.localPosition = new Vector3(0f, 1.1f, 0.65f);
            hb.transform.localRotation = Quaternion.identity;
            var bx = hb.AddComponent<BoxCollider>();
            bx.isTrigger = true;
            bx.center = Vector3.zero;
            bx.size = new Vector3(1.35f, 1.1f, 1.05f);
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
                m_LastAnimSpeed = 0f;
                if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                    m_Animator.SetFloat(m_AnimatorSpeedFloatName, 0f);
                return;
            }

            ResolveTarget();
            if (m_Target == null)
            {
                m_LastAnimSpeed = 0f;
                if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                    m_Animator.SetFloat(m_AnimatorSpeedFloatName, 0f);
                return;
            }

            if (m_Animator != null && !string.IsNullOrEmpty(m_AnimatorSpeedFloatName))
                m_Animator.SetFloat(m_AnimatorSpeedFloatName, m_LastAnimSpeed);

            var planar = m_Target.position - transform.position;
            planar.y = 0f;
            float distPlanar = planar.magnitude;
            if (distPlanar <= m_AttackRange && Time.time >= m_NextAttackTime)
                StartAttackPulse();

            if (m_MeleeWindow && Time.time >= m_MeleeEndsTime)
                StopMeleeVisual();
        }

        void FixedUpdate()
        {
            if (!IsOperational())
                return;

            ResolveTarget();
            if (m_Target == null)
            {
                m_LastAnimSpeed = 0f;
                return;
            }

            Vector3 posBefore = transform.position;

            var planar = m_Target.position - posBefore;
            planar.y = 0f;
            float distPlanar = planar.magnitude;
            Vector3 planarDir = distPlanar > 0.05f ? planar / distPlanar : transform.forward;
            Vector3 separation = ComputeSeparationPush();
            Vector3 combined = planarDir + separation;
            combined.y = 0f;
            Vector3 moveDir = combined.sqrMagnitude > 1e-6f ? combined.normalized : planarDir;

            if (distPlanar > 0.05f)
            {
                var look = Quaternion.LookRotation(planarDir, Vector3.up);
                var rot = Quaternion.RotateTowards(transform.rotation, look, m_TurnSpeedDegrees * Time.fixedDeltaTime);
                transform.rotation = rot;
                if (m_Rb != null)
                    m_Rb.rotation = rot;
            }

            float stopDistance = Mathf.Max(0f, m_AttackRange + m_AttackWarmupBias);
            if (distPlanar > stopDistance)
            {
                float stepMag = Mathf.Min(m_ChaseSpeed * Time.fixedDeltaTime, distPlanar - stopDistance);
                var step = moveDir * stepMag;
                var next = posBefore + new Vector3(step.x, 0f, step.z);
                transform.position = next;
                if (m_Rb != null)
                    m_Rb.position = next;
            }

            Vector3 planarDelta = transform.position - posBefore;
            planarDelta.y = 0f;
            float planarSpeed = planarDelta.magnitude / Mathf.Max(Time.fixedDeltaTime, 1e-5f);
            m_LastAnimSpeed = Mathf.Clamp01(planarSpeed / Mathf.Max(0.01f, m_ChaseSpeed));
            if (distPlanar <= m_AttackRange)
                m_LastAnimSpeed *= 0.25f;

        }

        void LateUpdate()
        {
            if (!m_SnapToWalkableGround || !IsOperational())
                return;
            SnapToGroundSmooth();
        }

        void SnapToGroundImmediate()
        {
            if (!TrySampleGroundY(out float y))
                return;
            var p = transform.position;
            p.y = y;
            ApplyPosition(p);
        }

        void SnapToGroundSmooth()
        {
            if (!TrySampleGroundY(out float targetY))
                return;
            var p = transform.position;
            float maxStep = Mathf.Max(0.01f, m_MaxGroundSnapPerFrame);
            p.y = Mathf.MoveTowards(p.y, targetY, maxStep);
            ApplyPosition(p);
        }

        void ApplyPosition(Vector3 p)
        {
            transform.position = p;
            if (m_Rb != null)
                m_Rb.position = p;
        }

        bool TrySampleGroundY(out float groundY)
        {
            return WalkableGroundUtility.TryGetGroundHeight(
                transform.position,
                transform,
                m_FootHeightOffset,
                m_GroundRayStartHeight,
                m_GroundRayMaxDistance,
                m_MinGroundNormalY,
                out groundY);
        }

        bool IsOperational() => m_EnemyHealth != null && m_EnemyHealth.IsAlive && m_Rb != null;

        void ResolveTarget()
        {
            if (m_PlayerTargetOverride != null)
                m_Target = m_PlayerTargetOverride;
            else
            {
                var ph = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
                m_Target = ph != null ? ph.transform : null;
            }
        }

        Vector3 ComputeSeparationPush()
        {
            if (m_SeparationRadius <= 0f || m_SeparationWeight <= 0f || m_NeighborBuffer == null)
                return Vector3.zero;

            int found = Physics.OverlapSphereNonAlloc(
                transform.position,
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
                Vector3 delta = transform.position - c.bounds.center;
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
            m_MeleeWindow = m_CanDealMeleeDamage;
            if (m_Hitbox != null && m_CanDealMeleeDamage)
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
        const int OverlapBufferLen = 16;

        SkeletonMeleeAi m_OwnerAi;
        float m_DamageAmount;
        float m_HitCooldown;
        bool m_WindowEnabled;
        float m_NextHitTime;
        BoxCollider m_Box;
        Collider[] m_OverlapBuffer;

        public void Configure(SkeletonMeleeAi owner, float dmg, float minIntervalSeconds)
        {
            m_OwnerAi = owner;
            m_DamageAmount = dmg;
            m_HitCooldown = Mathf.Max(0.05f, minIntervalSeconds);
            m_Box = GetComponent<BoxCollider>();
            if (m_OverlapBuffer == null)
                m_OverlapBuffer = new Collider[OverlapBufferLen];
            EnableDamageWindow(false);
        }

        public void EnableDamageWindow(bool on) => m_WindowEnabled = on;

        public void ResetHitCooldownGate() => m_NextHitTime = 0f;

        void FixedUpdate()
        {
            if (!m_WindowEnabled || m_Box == null || !m_Box.enabled || !m_Box.isTrigger)
                return;
            Bounds b = m_Box.bounds;
            int n = Physics.OverlapBoxNonAlloc(
                b.center,
                b.extents,
                m_OverlapBuffer,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = m_OverlapBuffer[i];
                if (c != null)
                    TryApplyMeleeDamage(c);
            }
        }

        void OnTriggerEnter(Collider other) => TryApplyMeleeDamage(other);

        void OnTriggerStay(Collider other) => TryApplyMeleeDamage(other);

        void TryApplyMeleeDamage(Collider other)
        {
            if (!m_WindowEnabled)
                return;
            if (m_OwnerAi != null && !m_OwnerAi.CanDealMeleeDamage)
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
