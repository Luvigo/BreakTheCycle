using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MundoMental.VR.Combat
{
    /// <summary>Registra al soltar/coger armas hacia <see cref="HandEquipmentTracker"/>. Añade esto a la misma entidad que <see cref="XRGrabInteractable"/>.</summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class GrabbableWeapon : MonoBehaviour
    {
        public enum Kind
        {
            Pistol = 0,
            Sword = 1,
        }

        [SerializeField] HandEquipmentTracker m_Tracker;
        [SerializeField] Kind m_Kind;
        [SerializeField] GunWeapon m_Pistol;
        [SerializeField] SwordWeapon m_Sword;
        [SerializeField] [Tooltip("Raíz del mando DERECHO (debe coincidir con la de HandEquipmentTracker)")] Transform m_RightHandRoot;
        [SerializeField] [Tooltip("Si está vacío, se usa m_RightHandRoot de HandEquipmentTracker")] Transform m_ExplicitRightRoot;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_Grabbable;

        void Awake()
        {
            m_Grabbable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (m_Tracker == null)
                m_Tracker = FindFirstObjectByType<HandEquipmentTracker>();
        }

        void OnEnable()
        {
            m_Grabbable.selectEntered.AddListener(OnSelectEntered);
            m_Grabbable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            m_Grabbable.selectEntered.RemoveListener(OnSelectEntered);
            m_Grabbable.selectExited.RemoveListener(OnSelectExited);
        }

        bool IsInteractingRightHand(IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;
            var interactorComp = interactor as Component;
            if (interactorComp == null)
                return false;
            var root = m_ExplicitRightRoot != null ? m_ExplicitRightRoot : (m_Tracker != null ? m_Tracker.RightHandControllerRoot : m_RightHandRoot);
            if (root == null)
            {
                // Fallback para setup rápido: si no hay root configurado, asumimos mano derecha
                // y priorizamos que el arma sí registre el estado de equipo.
                CombatLog.Warn("GrabbableWeapon: Right Hand Root no asignado, usando fallback (acepta interactor actual).", "GrabbableWeapon");
                return true;
            }

            if (interactorComp.transform == root || interactorComp.transform.IsChildOf(root))
                return true;

            // Fallback adicional por nombre para rigs con jerarquías ligeramente distintas.
            return interactorComp.name.ToLowerInvariant().Contains("right");
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!IsInteractingRightHand(args.interactorObject))
            {
                CombatLog.Log("Arma agarrada (no mano derecha; ignora registro de modo)", "GrabbableWeapon");
                return;
            }

            if (m_Kind == Kind.Pistol && m_Pistol != null)
            {
                m_Tracker?.RegisterRightPistol(m_Pistol);
            }
            else if (m_Kind == Kind.Sword && m_Sword != null)
            {
                m_Tracker?.RegisterRightSword(m_Sword);
            }
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (!IsInteractingRightHand(args.interactorObject))
                return;
            m_Tracker?.ClearRightHand();
        }
    }
}
