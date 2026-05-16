using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MundoMental.VR.Combat
{
    /// <summary>
    /// Recall con B: trae el martillo en arco cerca del mando usando un ancla estable, sin forzar agarre.
    /// </summary>
    [AddComponentMenu("Mundo Mental VR/Hammer Recall To Hand")]
    public class HammerRecallToHand : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable m_Hammer;
        [SerializeField] [Tooltip("NearFarInteractor del mando (derecho).")] XRBaseInteractor m_HammerHandInteractor;
        [SerializeField] [Tooltip("Ancla fija en el mando (p. ej. \"Right Controller Stabilized Attach\").")] Transform m_RecallHandAnchor;
        [SerializeField] InputActionReference m_RecallAction;
        [SerializeField] [Tooltip("Si está vacío, se usa el Interaction Manager del martillo.")] XRInteractionManager m_InteractionManager;

        [Header("Animación hacia la mano")]
        [SerializeField] float m_RecallSeconds = 0.55f;
        [SerializeField] float m_ArcLiftMin = 0.12f;
        [SerializeField] float m_ArcLiftMax = 0.55f;
        [SerializeField] [Tooltip("Altura extra del arco según distancia horizontal al objetivo.")] float m_ArcLiftPerMeter = 0.28f;
        [SerializeField] [Tooltip("Metros a lo largo del forward del ancla (suele ser hacia los dedos).")] float m_RecallHoverForwardOffset = 0.07f;

        Coroutine m_RecallCoroutine;
        Rigidbody m_RecallRb;
        bool m_RecallPrevKinematic;
        bool m_RecallPrevUseGravity;

        void OnEnable()
        {
            if (m_RecallAction == null || m_RecallAction.action == null)
                return;
            var action = m_RecallAction.action;
            action.actionMap?.Enable();
            action.Enable();
            action.performed += OnRecallPerformed;
        }

        void OnDisable()
        {
            if (m_RecallCoroutine != null)
            {
                StopCoroutine(m_RecallCoroutine);
                m_RecallCoroutine = null;
                RestoreRbAfterInterruptedRecall();
            }

            if (m_RecallAction == null || m_RecallAction.action == null)
                return;
            var action = m_RecallAction.action;
            action.performed -= OnRecallPerformed;
            action.Disable();
        }

        void OnRecallPerformed(InputAction.CallbackContext _)
        {
            TryRecall();
        }

        Transform GetRecallAnchor()
        {
            if (m_RecallHandAnchor != null)
                return m_RecallHandAnchor;
            return m_HammerHandInteractor != null ? m_HammerHandInteractor.transform : null;
        }

        /// <summary>Llamada opcional desde UI u otro script.</summary>
        public void TryRecall()
        {
            if (m_RecallCoroutine != null)
                return;
            if (m_Hammer == null || m_HammerHandInteractor == null)
                return;

            var manager = m_InteractionManager != null ? m_InteractionManager : m_Hammer.interactionManager;
            if (manager == null)
                return;

            var handAsSelect = (IXRSelectInteractor)m_HammerHandInteractor;

            if (m_Hammer.isSelected && m_Hammer.interactorsSelecting.Count == 1 &&
                ReferenceEquals(m_Hammer.interactorsSelecting[0], handAsSelect))
                return;

            m_RecallCoroutine = StartCoroutine(RecallSequence(manager));
        }

        IEnumerator RecallSequence(XRInteractionManager manager)
        {
            m_RecallRb = null;
            var toExit = m_Hammer.interactorsSelecting.ToList();
            foreach (var ixr in toExit)
                manager.SelectExit(ixr, m_Hammer);

            // Dejar que XRInteractionManager aplique Drop/Detach antes de poner kinematic en el recall.
            yield return null;
            yield return null;
            if (m_Hammer.isSelected)
            {
                m_RecallCoroutine = null;
                yield break;
            }

            var rb = m_Hammer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                m_RecallRb = rb;
                m_RecallPrevKinematic = rb.isKinematic;
                m_RecallPrevUseGravity = rb.useGravity;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var startPos = m_Hammer.transform.position;
            var startRot = m_Hammer.transform.rotation;

            float duration = Mathf.Max(0.08f, m_RecallSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / duration);
                float s = u * u * (3f - 2f * u);

                var anchor = GetRecallAnchor();
                ComputeAlignedRootPoseFromAnchor(anchor, out var goalPos, out var goalRot);
                ApplyHoverOffset(anchor, ref goalPos, goalRot);

                var horizontal = goalPos - startPos;
                horizontal.y = 0f;
                float distH = horizontal.magnitude;
                float lift = Mathf.Clamp(distH * m_ArcLiftPerMeter + m_ArcLiftMin * 0.5f, m_ArcLiftMin, m_ArcLiftMax);
                var mid = (startPos + goalPos) * 0.5f + Vector3.up * lift;

                m_Hammer.transform.SetPositionAndRotation(
                    QuadraticBezier(startPos, mid, goalPos, s),
                    Quaternion.Slerp(startRot, goalRot, s));
                yield return null;
            }

            SnapHammerNearHandForManualGrab();

            if (rb != null)
            {
                // Tras soltar del agarre el cuerpo debe ser dinámico para poder cogerlo de nuevo y caer con gravedad.
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            ClearRecallRbState();
            m_RecallCoroutine = null;
        }

        void ApplyHoverOffset(Transform anchor, ref Vector3 rootPos, Quaternion rootRot)
        {
            if (m_RecallHoverForwardOffset <= 0f || anchor == null)
                return;
            rootPos += anchor.forward * m_RecallHoverForwardOffset;
        }

        void SnapHammerNearHandForManualGrab()
        {
            var anchor = GetRecallAnchor();
            ComputeAlignedRootPoseFromAnchor(anchor, out var pos, out var rot);
            ApplyHoverOffset(anchor, ref pos, rot);
            m_Hammer.transform.SetPositionAndRotation(pos, rot);

            if (m_Hammer.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        void RestoreRbAfterInterruptedRecall()
        {
            if (m_RecallRb != null && m_Hammer != null && !m_Hammer.isSelected)
            {
                m_RecallRb.isKinematic = m_RecallPrevKinematic;
                m_RecallRb.useGravity = m_RecallPrevUseGravity;
                m_RecallRb.WakeUp();
            }

            ClearRecallRbState();
        }

        void ClearRecallRbState()
        {
            m_RecallRb = null;
        }

        static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        void ComputeAlignedRootPoseFromAnchor(Transform anchor, out Vector3 worldPos, out Quaternion worldRot)
        {
            if (anchor == null)
            {
                worldPos = m_Hammer.transform.position;
                worldRot = m_Hammer.transform.rotation;
                return;
            }

            var hammerAttach = m_Hammer.attachTransform != null ? m_Hammer.attachTransform : m_Hammer.transform;
            var deltaPos = anchor.position - hammerAttach.position;
            worldPos = m_Hammer.transform.position + deltaPos;
            if (m_Hammer.trackRotation)
            {
                var rotDelta = anchor.rotation * Quaternion.Inverse(hammerAttach.rotation);
                worldRot = rotDelta * m_Hammer.transform.rotation;
            }
            else
                worldRot = m_Hammer.transform.rotation;
        }
    }
}
