using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MundoMental.VR
{
    /// <summary>
    /// Lee grip/gatillo del mando XR y opcionalmente anima dedos del prefab SimpleHands.
    /// Pulgar: <c>Thumb</c> (metacarpiano) y <c>Thumb2</c>; dedos: <c>Index2</c>…<c>Little3</c>.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Mundo Mental VR/Simple Hands XR Input Driver")]
    public sealed class SimpleHandsXRInputDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Si es true, usa el mando derecho; si no, el izquierdo.")]
        bool m_RightHand = true;

        public bool IsRightHand => m_RightHand;

        [SerializeField]
        [Tooltip("Parámetro float del Animator para el agarre (vacío = no enviar).")]
        string m_GripParameter = "Grip";

        [SerializeField]
        [Tooltip("Parámetro float del Animator para el gatillo (vacío = no enviar).")]
        string m_TriggerParameter = "Trigger";

        [Header("Dedos (si el Animator Controller falta)")]
        [SerializeField]
        [Tooltip("Rota huesos nombrados según grip/gatillo cuando el Animator no puede.")]
        bool m_ProceduralFingerCurl = true;

        [SerializeField]
        [Tooltip("Flexión máxima base en nudillos (PIP). Los dedos tienen multiplicadores distintos para un puño más natural.")]
        float m_FingerCurlDegrees = 56f;

        [SerializeField]
        [Range(0.2f, 1f)]
        [Tooltip("Cuánto más cierra la falange distal (DIP) respecto a la proximal.")]
        float m_DistalCurlScale = 0.82f;

        [SerializeField]
        [Tooltip("Rotación del hueso Thumb hacia la palma (oposición), eje local Y.")]
        float m_ThumbMetacarpalDegrees = 56f;

        [SerializeField]
        [Tooltip("Flexión en la falange del hueso Thumb2, eje local X.")]
        float m_ThumbDistalDegrees = 34f;

        [SerializeField]
        [Range(0.5f, 1.2f)]
        [Tooltip("Exponente menor que 1: el cierre acelera al final (más natural que lineal).")]
        float m_CurlShapeExponent = 0.84f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Cuánto influye el gatillo en dedos que no son índice (0 = solo grip).")]
        float m_NonIndexTriggerMix = 0.62f;

        [SerializeField]
        [Range(0f, 0.35f)]
        [Tooltip("Con grip/gatillo en 0, aplica esta fracción del cierre máximo (dedos ligeramente flexionados en reposo).")]
        float m_RestCurlBlend = 0.11f;

        [SerializeField]
        [Range(0f, 0.25f)]
        [Tooltip("Fracción extra de cierre solo en pulgar en reposo (oposición natural).")]
        float m_ThumbRestCurlExtra = 0.06f;

        [SerializeField]
        [Tooltip("Desactiva el Animator si no hay Runtime Controller asignado (evita coste y conflictos).")]
        bool m_DisableAnimatorIfNoController = true;

        Animator m_Animator;
        InputDevice m_Device;
        bool m_LoggedMissingParams;

        struct DrivenBone
        {
            public Transform Transform;
            public Quaternion BaseLocalRotation;
            public float MaxAngle;
            public Vector3 LocalAxis;
            public float CurlSign;
            public bool IsIndex;
            public bool IsThumb;
        }

        readonly List<DrivenBone> m_DrivenBones = new List<DrivenBone>(14);
        static readonly string[] kBoneNames =
        {
            "Index2", "Middle2", "Ring2", "Little2",
            "Index3", "Middle3", "Ring3", "Little3",
            "Thumb", "Thumb2",
        };

        void Awake()
        {
            m_Animator = GetComponent<Animator>();
            if (m_DisableAnimatorIfNoController && m_Animator != null && m_Animator.runtimeAnimatorController == null)
                m_Animator.enabled = false;

            CacheFingerBones();
        }

        void OnEnable()
        {
            m_Device = default;
            m_LoggedMissingParams = false;
            LogAnimatorParamsOnce();
        }

        void LogAnimatorParamsOnce()
        {
            if (m_Animator == null || !m_Animator.enabled || m_Animator.runtimeAnimatorController == null)
                return;

            var hasGrip = HasFloatParameter(m_GripParameter);
            var hasTrig = HasFloatParameter(m_TriggerParameter);
            if (hasGrip || hasTrig || m_LoggedMissingParams)
                return;

            m_LoggedMissingParams = true;
            var names = new List<string>();
            foreach (var p in m_Animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float)
                    names.Add(p.name);
            }

            Debug.LogWarning(
                $"SimpleHandsXRInputDriver en '{name}': el Animator no tiene floats '{m_GripParameter}' / '{m_TriggerParameter}'. " +
                "Se usan dedos procedurales si están activados. Floats en el controller: " +
                (names.Count > 0 ? string.Join(", ", names) : "(ninguno)"), this);
        }

        bool HasFloatParameter(string paramName)
        {
            if (string.IsNullOrEmpty(paramName) || m_Animator == null)
                return false;
            foreach (var p in m_Animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float && p.name == paramName)
                    return true;
            }

            return false;
        }

        static bool IsFingerBoneName(string boneName)
        {
            return boneName.StartsWith("Index") || boneName.StartsWith("Middle") ||
                   boneName.StartsWith("Ring") || boneName.StartsWith("Little");
        }

        static float FingerCurlMultiplier(string boneName)
        {
            if (boneName.StartsWith("Index"))
                return 0.9f;
            if (boneName.StartsWith("Middle"))
                return 1f;
            if (boneName.StartsWith("Ring"))
                return 0.96f;
            if (boneName.StartsWith("Little"))
                return 1.08f;
            return 1f;
        }

        void CacheFingerBones()
        {
            m_DrivenBones.Clear();
            foreach (var boneName in kBoneNames)
            {
                var t = FindDeepChild(transform, boneName);
                if (t == null)
                    continue;

                float max;
                Vector3 axis;
                if (boneName == "Thumb")
                {
                    max = m_ThumbMetacarpalDegrees;
                    axis = Vector3.up;
                }
                else if (boneName == "Thumb2")
                {
                    max = m_ThumbDistalDegrees;
                    axis = Vector3.right;
                }
                else
                {
                    var isDistal = boneName.Length > 0 && boneName[^1] == '3';
                    max = m_FingerCurlDegrees * FingerCurlMultiplier(boneName) * (isDistal ? m_DistalCurlScale : 1f);
                    axis = Vector3.right;
                }

                var isThumb = boneName == "Thumb" || boneName == "Thumb2";
                // Pulgar con el mismo signo que los dedos en ambas manos (antes +1 en la izquierda cerraba al revés).
                const float curlSign = -1f;

                m_DrivenBones.Add(new DrivenBone
                {
                    Transform = t,
                    BaseLocalRotation = t.localRotation,
                    MaxAngle = max,
                    LocalAxis = axis.normalized,
                    CurlSign = curlSign,
                    IsIndex = boneName.StartsWith("Index"),
                    IsThumb = isThumb,
                });
            }
        }

        static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                    return t;
            }

            return null;
        }

        void LateUpdate()
        {
            if (!TryGetGripTrigger(out var grip, out var trigger))
                return;

            if (m_Animator != null && m_Animator.enabled && m_Animator.runtimeAnimatorController != null)
            {
                if (!string.IsNullOrEmpty(m_GripParameter) && HasFloatParameter(m_GripParameter))
                    m_Animator.SetFloat(m_GripParameter, grip);
                if (!string.IsNullOrEmpty(m_TriggerParameter) && HasFloatParameter(m_TriggerParameter))
                    m_Animator.SetFloat(m_TriggerParameter, trigger);
            }

            if (!m_ProceduralFingerCurl || m_DrivenBones.Count == 0)
                return;

            if (m_Animator != null && m_Animator.enabled && m_Animator.runtimeAnimatorController != null)
                return;

            var rawIndex = Mathf.Max(grip, trigger);
            var rawOthers = Mathf.Max(grip, Mathf.Lerp(grip, trigger, m_NonIndexTriggerMix));
            var exp = Mathf.Clamp(m_CurlShapeExponent, 0.5f, 1.2f);
            var curlIndex = Mathf.Pow(Mathf.Clamp01(rawIndex), exp);
            var curlOthers = Mathf.Pow(Mathf.Clamp01(rawOthers), exp);

            for (var i = 0; i < m_DrivenBones.Count; i++)
            {
                var b = m_DrivenBones[i];
                if (b.Transform == null)
                    continue;
                var curl = b.IsIndex ? curlIndex : curlOthers;
                var rest = Mathf.Clamp01(m_RestCurlBlend + (b.IsThumb ? m_ThumbRestCurlExtra : 0f));
                var curlWithRest = Mathf.Lerp(rest, 1f, curl);
                var angle = b.CurlSign * curlWithRest * b.MaxAngle;
                b.Transform.localRotation = b.BaseLocalRotation * Quaternion.AngleAxis(angle, b.LocalAxis);
            }
        }

        bool TryGetGripTrigger(out float grip, out float trigger)
        {
            grip = 0f;
            trigger = 0f;

            if (!m_Device.isValid)
                m_Device = InputDevices.GetDeviceAtXRNode(m_RightHand ? XRNode.RightHand : XRNode.LeftHand);

            if (!m_Device.isValid)
                return false;

            m_Device.TryGetFeatureValue(CommonUsages.grip, out grip);
            m_Device.TryGetFeatureValue(CommonUsages.trigger, out trigger);
            return true;
        }
    }
}
