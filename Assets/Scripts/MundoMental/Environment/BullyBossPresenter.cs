using System;
using System.Collections.Generic;
using MundoMental.VR.Combat;
using UnityEngine;

namespace MundoMental.VR.Environment
{
    /// <summary>
    /// Bully decorativo: cabeza/cuello animados + brazos en pose colgada (offsets locales, sin romper el rig).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class BullyBossPresenter : MonoBehaviour
    {
        enum Gesture { Watching, Laughing, Taunting }

        enum BoneKind
        {
            Spine,
            Spine1,
            Neck,
            Head,
            ShoulderLeft,
            ShoulderRight,
            UpperArmLeft,
            UpperArmRight,
            ForeArmLeft,
            ForeArmRight,
            HandLeft,
            HandRight,
        }

        [SerializeField] bool m_FacePlayer = true;
        [SerializeField] float m_TurnSpeedDegrees = 42f;
        [SerializeField] float m_LookAtMinDistance = 1.5f;
        [SerializeField] bool m_UseProceduralIdle = true;
        [SerializeField] float m_GestureMinSeconds = 3.5f;
        [SerializeField] float m_GestureMaxSeconds = 7f;
        [SerializeField] float m_LaughDurationSeconds = 2.4f;

        [Header("Brazos")]
        [SerializeField] bool m_AnimateArms;
        [SerializeField] bool m_ApplyArmHangPose = true;
        [SerializeField] bool m_FixArmsOutOfTPose = true;
        [SerializeField] float m_ArmDropAngle = 38f;
        [SerializeField] float m_ElbowRestBend = 18f;
        [SerializeField] float m_ShoulderDropZ = 82f;
        [SerializeField] float m_ElbowBendX = 28f;
        [SerializeField] bool m_ForceHandsRestOnGround = true;
        [SerializeField] bool m_AutoFlipShoulderDrop = true;
        [SerializeField] float m_ArmSwayDegrees = 5f;
        [SerializeField] float m_HandSwayDegrees = 7f;
        [Tooltip("Si los brazos suben en V, activar.")]
        [SerializeField] bool m_InvertShoulderDrop;

        Transform m_Player;
        readonly List<BoneRig> m_Bones = new();
        Gesture m_Gesture = Gesture.Watching;
        float m_GestureElapsed;
        float m_GestureDuration = 5f;
        float m_LaughBurst;

        struct BoneRig
        {
            public Transform Bone;
            public Quaternion BindLocal;
            public Quaternion RestLocal;
            public BoneKind Kind;
        }

        void Awake()
        {
            var anim = GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.enabled = false;
                anim.runtimeAnimatorController = null;
            }

            CacheBones();
            BuildRestPose();

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            PickNextGesture(true);
        }

        void Start() => m_Player = FindFirstObjectByType<PlayerHealth>()?.transform;

        void LateUpdate()
        {
            FacePlayer();
            if (!m_UseProceduralIdle)
                return;

            AdvanceGesture();
            ApplyPose();
        }

        void CacheBones()
        {
            m_Bones.Clear();
            AddBone("Spine", BoneKind.Spine);
            AddBone("Spine1", BoneKind.Spine1);
            AddBone("Neck", BoneKind.Neck);
            AddBone("Head", BoneKind.Head);
            AddBone("LeftShoulder", BoneKind.ShoulderLeft);
            AddBone("RightShoulder", BoneKind.ShoulderRight);
            AddBone("LeftArm", BoneKind.UpperArmLeft);
            AddBone("RightArm", BoneKind.UpperArmRight);
            AddBone("LeftForeArm", BoneKind.ForeArmLeft);
            AddBone("RightForeArm", BoneKind.ForeArmRight);
            AddBone("LeftHand", BoneKind.HandLeft);
            AddBone("RightHand", BoneKind.HandRight);
        }

        void AddBone(string boneName, BoneKind kind)
        {
            var t = FindBone(boneName);
            if (t == null)
                return;

            m_Bones.Add(new BoneRig
            {
                Bone = t,
                BindLocal = t.localRotation,
                RestLocal = t.localRotation,
                Kind = kind,
            });
        }

        void BuildRestPose()
        {
            float zDrop = m_ShoulderDropZ * (m_InvertShoulderDrop ? -1f : 1f);
            float elbow = m_ElbowBendX;

            void ApplyRest(float dropAmount)
            {
                for (int i = 0; i < m_Bones.Count; i++)
                {
                    var rig = m_Bones[i];
                    Quaternion hang = Quaternion.identity;

                    if (m_AnimateArms && m_ApplyArmHangPose)
                    {
                        // Solo offsets locales sobre la pose del FBX (no tocar world rotation).
                        hang = rig.Kind switch
                        {
                            BoneKind.ShoulderLeft => Quaternion.Euler(0f, 0f, -dropAmount),
                            BoneKind.ShoulderRight => Quaternion.Euler(0f, 0f, dropAmount),
                            BoneKind.UpperArmLeft => Quaternion.Euler(-12f, 0f, 0f),
                            BoneKind.UpperArmRight => Quaternion.Euler(-12f, 0f, 0f),
                            BoneKind.ForeArmLeft => Quaternion.Euler(-elbow, 0f, 0f),
                            BoneKind.ForeArmRight => Quaternion.Euler(-elbow, 0f, 0f),
                            _ => Quaternion.identity,
                        };
                    }

                    rig.RestLocal = rig.BindLocal * hang;
                    m_Bones[i] = rig;
                }
            }

            ApplyRest(zDrop);

            if (m_AnimateArms && m_ApplyArmHangPose && m_AutoFlipShoulderDrop && ArmsAreRaised())
                ApplyRest(-zDrop);

            if (m_AnimateArms && m_ForceHandsRestOnGround)
                CalibrateArmsDownward();

            if (m_FixArmsOutOfTPose)
                ApplyBestEffortArmRestPose();
        }

        bool ArmsAreRaised()
        {
            int lShoulder = FindRigIndex(BoneKind.ShoulderLeft);
            int rShoulder = FindRigIndex(BoneKind.ShoulderRight);
            int lHand = FindRigIndex(BoneKind.HandLeft);
            int rHand = FindRigIndex(BoneKind.HandRight);
            if (lShoulder < 0 || rShoulder < 0 || lHand < 0 || rHand < 0)
                return false;

            var leftRaised = m_Bones[lHand].Bone.position.y > m_Bones[lShoulder].Bone.position.y + 0.01f;
            var rightRaised = m_Bones[rHand].Bone.position.y > m_Bones[rShoulder].Bone.position.y + 0.01f;
            return leftRaised || rightRaised;
        }

        void CalibrateArmsDownward()
        {
            // Alinea brazo y antebrazo hacia abajo para que manos queden "apoyadas".
            CalibrateArmDownward(BoneKind.UpperArmLeft, BoneKind.ForeArmLeft, BoneKind.HandLeft);
            CalibrateArmDownward(BoneKind.UpperArmRight, BoneKind.ForeArmRight, BoneKind.HandRight);
        }

        void CalibrateArmDownward(BoneKind upperKind, BoneKind foreKind, BoneKind handKind)
        {
            int upperIdx = FindRigIndex(upperKind);
            int foreIdx = FindRigIndex(foreKind);
            int handIdx = FindRigIndex(handKind);
            if (upperIdx < 0 || foreIdx < 0 || handIdx < 0)
                return;

            var upper = m_Bones[upperIdx];
            var fore = m_Bones[foreIdx];
            var hand = m_Bones[handIdx];
            if (upper.Bone == null || fore.Bone == null || hand.Bone == null)
                return;

            Vector3 downDir = (-transform.up + transform.forward * 0.08f).normalized;

            Vector3 upperDir = (fore.Bone.position - upper.Bone.position).normalized;
            if (upperDir.sqrMagnitude > 1e-6f)
                upper.Bone.rotation = Quaternion.FromToRotation(upperDir, downDir) * upper.Bone.rotation;

            Vector3 foreDir = (hand.Bone.position - fore.Bone.position).normalized;
            if (foreDir.sqrMagnitude > 1e-6f)
                fore.Bone.rotation = Quaternion.FromToRotation(foreDir, downDir) * fore.Bone.rotation;

            upper.RestLocal = upper.Bone.localRotation;
            fore.RestLocal = fore.Bone.localRotation;
            hand.RestLocal = hand.Bone.localRotation;
            m_Bones[upperIdx] = upper;
            m_Bones[foreIdx] = fore;
            m_Bones[handIdx] = hand;
        }

        int FindRigIndex(BoneKind kind)
        {
            for (int i = 0; i < m_Bones.Count; i++)
            {
                if (m_Bones[i].Kind == kind)
                    return i;
            }
            return -1;
        }

        void ApplyBestEffortArmRestPose()
        {
            ApplyBestEffortForSide(BoneKind.UpperArmLeft, BoneKind.ForeArmLeft, BoneKind.HandLeft);
            ApplyBestEffortForSide(BoneKind.UpperArmRight, BoneKind.ForeArmRight, BoneKind.HandRight);
        }

        void ApplyBestEffortForSide(BoneKind upperKind, BoneKind foreKind, BoneKind handKind)
        {
            int upperIdx = FindRigIndex(upperKind);
            int foreIdx = FindRigIndex(foreKind);
            int handIdx = FindRigIndex(handKind);
            if (upperIdx < 0 || foreIdx < 0 || handIdx < 0)
                return;

            var upper = m_Bones[upperIdx];
            var fore = m_Bones[foreIdx];
            var hand = m_Bones[handIdx];
            if (upper.Bone == null || fore.Bone == null || hand.Bone == null)
                return;

            var upperBind = upper.BindLocal;
            var foreBind = fore.BindLocal;

            Vector3[] upperCandidates =
            {
                new Vector3(m_ArmDropAngle, 0f, 0f),
                new Vector3(-m_ArmDropAngle, 0f, 0f),
                new Vector3(0f, m_ArmDropAngle, 0f),
                new Vector3(0f, -m_ArmDropAngle, 0f),
                new Vector3(0f, 0f, m_ArmDropAngle),
                new Vector3(0f, 0f, -m_ArmDropAngle),
            };

            Vector3[] foreCandidates =
            {
                new Vector3(m_ElbowRestBend, 0f, 0f),
                new Vector3(-m_ElbowRestBend, 0f, 0f),
                new Vector3(0f, 0f, m_ElbowRestBend),
                new Vector3(0f, 0f, -m_ElbowRestBend),
                Vector3.zero,
            };

            float bestScore = float.PositiveInfinity;
            Quaternion bestUpper = upperBind;
            Quaternion bestFore = foreBind;

            for (int i = 0; i < upperCandidates.Length; i++)
            {
                upper.Bone.localRotation = upperBind * Quaternion.Euler(upperCandidates[i]);
                for (int j = 0; j < foreCandidates.Length; j++)
                {
                    fore.Bone.localRotation = foreBind * Quaternion.Euler(foreCandidates[j]);
                    float score = hand.Bone.position.y;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestUpper = upper.Bone.localRotation;
                        bestFore = fore.Bone.localRotation;
                    }
                }
            }

            upper.Bone.localRotation = bestUpper;
            fore.Bone.localRotation = bestFore;
            upper.RestLocal = bestUpper;
            fore.RestLocal = bestFore;
            hand.RestLocal = hand.Bone.localRotation;
            m_Bones[upperIdx] = upper;
            m_Bones[foreIdx] = fore;
            m_Bones[handIdx] = hand;
        }

        void ApplyPose()
        {
            float t = Time.time;
            float breath = Mathf.Sin(t * 1.25f);
            float sway = Mathf.Sin(t * 0.85f);
            float sway2 = Mathf.Sin(t * 0.85f + 1.7f);
            float laughShake = m_Gesture == Gesture.Laughing
                ? Mathf.Sin(t * 14f) * m_LaughBurst
                : Mathf.Sin(t * 14f) * m_LaughBurst * 0.35f;

            for (int i = 0; i < m_Bones.Count; i++)
            {
                var rig = m_Bones[i];
                if (rig.Bone == null)
                    continue;

                Quaternion motion = rig.Kind switch
                {
                    BoneKind.Spine => Quaternion.Euler(breath * 2.5f + laughShake * 3f, sway * 1.2f, 0f),
                    BoneKind.Spine1 => Quaternion.Euler(breath * 2f + laughShake * 3.5f, sway * 1.5f, 0f),
                    BoneKind.Neck => Quaternion.Euler(ComputeHeadLookEuler() * 0.45f
                        + new Vector3(breath * 1.5f, sway * 2.5f, sway2 * 1.2f)),
                    BoneKind.Head => Quaternion.Euler(ComputeHeadLookEuler()
                        + new Vector3(-laughShake * 5f, sway * 3f + sway2 * 2f, laughShake * 2f)),
                    _ when m_AnimateArms && IsArmBone(rig.Kind) => ArmMotion(rig.Kind, sway, sway2, laughShake, t),
                    _ => Quaternion.identity,
                };

                rig.Bone.localRotation = rig.RestLocal * motion;
            }
        }

        static Quaternion ArmMotion(BoneKind kind, float sway, float sway2, float laughShake, float time)
        {
            float side = kind is BoneKind.ShoulderLeft or BoneKind.UpperArmLeft
                or BoneKind.ForeArmLeft or BoneKind.HandLeft
                ? 1f
                : -1f;

            return kind switch
            {
                BoneKind.ShoulderLeft or BoneKind.ShoulderRight => Quaternion.Euler(
                    0f, side * sway2 * 1.5f, side * sway * 2f),
                BoneKind.UpperArmLeft or BoneKind.UpperArmRight => Quaternion.Euler(
                    laughShake * 3f, side * sway * 2f, side * sway2 * 3f),
                BoneKind.ForeArmLeft or BoneKind.ForeArmRight => Quaternion.Euler(
                    sway * 3f + laughShake * 5f, side * laughShake * 2f, 0f),
                BoneKind.HandLeft or BoneKind.HandRight => Quaternion.Euler(
                    Mathf.Sin(time * 1.2f) * 2f, side * Mathf.Sin(time * 0.9f) * 5f, side * laughShake * 2f),
                _ => Quaternion.identity,
            };
        }

        static bool IsArmBone(BoneKind kind)
        {
            return kind is BoneKind.ShoulderLeft or BoneKind.ShoulderRight
                or BoneKind.UpperArmLeft or BoneKind.UpperArmRight
                or BoneKind.ForeArmLeft or BoneKind.ForeArmRight
                or BoneKind.HandLeft or BoneKind.HandRight;
        }

        Transform FindBone(string boneName)
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].name, boneName, StringComparison.Ordinal))
                    return all[i];
            }
            return null;
        }

        void AdvanceGesture()
        {
            m_GestureElapsed += Time.deltaTime;
            if (m_Gesture == Gesture.Laughing)
                m_LaughBurst = Mathf.Clamp01(m_LaughBurst + Time.deltaTime * 2.8f);
            else
                m_LaughBurst = Mathf.Clamp01(m_LaughBurst - Time.deltaTime * 3.5f);

            if (m_GestureElapsed < m_GestureDuration)
                return;

            PickNextGesture(false);
        }

        void PickNextGesture(bool firstPick)
        {
            m_GestureElapsed = 0f;
            if (firstPick)
            {
                m_Gesture = Gesture.Watching;
                m_GestureDuration = UnityEngine.Random.Range(m_GestureMinSeconds, m_GestureMaxSeconds);
                return;
            }

            float roll = UnityEngine.Random.value;
            if (roll < 0.38f)
            {
                m_Gesture = Gesture.Laughing;
                m_GestureDuration = m_LaughDurationSeconds;
            }
            else if (roll < 0.62f)
            {
                m_Gesture = Gesture.Taunting;
                m_GestureDuration = UnityEngine.Random.Range(2.8f, 4.2f);
            }
            else
            {
                m_Gesture = Gesture.Watching;
                m_GestureDuration = UnityEngine.Random.Range(m_GestureMinSeconds, m_GestureMaxSeconds);
            }
        }

        Vector3 ComputeHeadLookEuler()
        {
            if (m_Player == null)
                return Vector3.zero;

            var toPlayer = m_Player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f)
                return Vector3.zero;

            float yawToPlayer = Vector3.SignedAngle(transform.forward, toPlayer.normalized, Vector3.up);
            yawToPlayer = Mathf.Clamp(yawToPlayer, -28f, 28f);

            float heightDelta = m_Player.position.y - (transform.position.y + 1.4f);
            float pitch = Mathf.Clamp(heightDelta * 10f, -12f, 16f);

            return new Vector3(pitch, yawToPlayer * 0.55f, 0f);
        }

        void FacePlayer()
        {
            if (!m_FacePlayer || m_Player == null)
                return;

            var planar = m_Player.position - transform.position;
            planar.y = 0f;
            if (planar.sqrMagnitude < m_LookAtMinDistance * m_LookAtMinDistance)
                return;

            var target = Quaternion.LookRotation(planar.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                m_TurnSpeedDegrees * Time.deltaTime);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!isActiveAndEnabled || m_Bones.Count == 0)
                return;
            CacheBones();
            BuildRestPose();
        }
#endif
    }
}
