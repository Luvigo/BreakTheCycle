using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MundoMental.VR.Combat
{
    /// <summary>
    /// Tras soltar un <see cref="XRGrabInteractable"/>, refuerza la velocidad de tiro para que sueltos “flojos”
    /// sigan saliendo con impulso hacia el forward del mando.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Mundo Mental VR/Grabbable Throw Boost")]
    public class GrabbableThrowBoost : MonoBehaviour
    {
        [SerializeField] float m_MinLinearSpeed = 2.8f;
        [SerializeField] float m_MinAngularSpeed = 1.15f;
        [SerializeField] [Tooltip("Si la velocidad de tiro del toolkit está por debajo de este valor (m/s), se alinea al forward del interactor y se aplica al menos m_MinLinearSpeed.")]
        float m_ComfortThrowSpeedThreshold = 4.25f;
        [SerializeField] [Tooltip("Multiplicador extra cuando forzamos un tiro “cómodo” (suelto flojo).")] float m_EasyThrowSpeedMultiplier = 1.2f;

        XRGrabInteractable m_Grab;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            m_Grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            m_Grab.selectExited.RemoveListener(OnReleased);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            var interactor = args.interactorObject as Component;
            StartCoroutine(BoostAfterPhysicsThrow(interactor));
        }

        IEnumerator BoostAfterPhysicsThrow(Component interactor)
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();

            if (!isActiveAndEnabled || m_Grab == null || m_Grab.isSelected)
                yield break;

            var rb = GetComponent<Rigidbody>();
            if (rb == null)
                yield break;

            if (rb.isKinematic && !m_Grab.isSelected)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            if (rb.isKinematic)
                yield break;

            rb.useGravity = true;

            var v = rb.linearVelocity;
            float mag = v.magnitude;

            Vector3 interactorFwd = Vector3.forward;
            if (interactor != null && interactor.transform.forward.sqrMagnitude > 1e-6f)
                interactorFwd = interactor.transform.forward.normalized;
            else if (mag > 0.12f)
                interactorFwd = v.normalized;

            if (mag < m_MinLinearSpeed)
                rb.linearVelocity = interactorFwd * m_MinLinearSpeed;
            else if (mag < m_ComfortThrowSpeedThreshold)
            {
                var boosted = mag * m_EasyThrowSpeedMultiplier;
                rb.linearVelocity = Vector3.Slerp(v.normalized, interactorFwd, 0.55f) * Mathf.Max(boosted, m_MinLinearSpeed);
            }

            v = rb.linearVelocity;
            mag = v.magnitude;
            var dir = mag > 0.12f ? v.normalized : interactorFwd;

            var w = rb.angularVelocity;
            if (w.magnitude < m_MinAngularSpeed && m_MinAngularSpeed > 0f)
            {
                var spin = Vector3.Cross(dir, Vector3.up);
                if (spin.sqrMagnitude < 1e-4f)
                    spin = Vector3.right;
                rb.angularVelocity = spin.normalized * m_MinAngularSpeed;
            }

            rb.WakeUp();
        }
    }
}
