using UnityEngine;
using UnityEngine.InputSystem;

namespace MundoMental.VR.Combat
{
    /// <summary>Controla disparo VR (energia / pistola / espada) y el trigger del mando derecho. Sustituye a cualquier version anterior con otro nombre de archivo en el proyecto.</summary>
    [AddComponentMenu("Mundo Mental VR/VR Combat (disparo)")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public class MundoMentalVrCombat : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] HandEquipmentTracker m_Equipment;
        [SerializeField] VRHandPower m_HandPower;
        [Header("Input mando derecho")]
        [SerializeField] InputActionReference m_RightHandFire;
        [SerializeField] bool m_AutoBindInOnEnable = true;

        void OnEnable()
        {
            if (!m_AutoBindInOnEnable || m_RightHandFire == null || m_RightHandFire.action == null)
                return;
            m_RightHandFire.action.performed += OnRightFire;
            m_RightHandFire.action.Enable();
        }

        void OnDisable()
        {
            if (m_RightHandFire != null && m_RightHandFire.action != null)
                m_RightHandFire.action.performed -= OnRightFire;
        }

        void OnRightFire(InputAction.CallbackContext ctx) => TryRouteRightAttack();

        [ContextMenu("Debug/Try right attack (editor)")]
        public void TryRouteRightAttack()
        {
            if (m_Equipment == null)
            {
                CombatLog.Warn("MundoMentalVrCombat: asigna HandEquipmentTracker (XR Origin).", "Combat");
                return;
            }

            switch (m_Equipment.CurrentRightMode)
            {
                case HandEquipmentTracker.RightHandMode.Sword:
                    CombatLog.Log("Ruta: ESPADA - sin energia, solo corte / contacto.", "Combat");
                    return;
                case HandEquipmentTracker.RightHandMode.Pistol:
                {
                    var g = m_Equipment.EquippedPistol;
                    if (g != null)
                    {
                        g.TryFire();
                        CombatLog.Log("Ruta: PISTOLA", "Combat");
                    }
                    return;
                }
                case HandEquipmentTracker.RightHandMode.Empty:
                default:
                {
                    if (m_HandPower != null)
                    {
                        m_HandPower.TryFire();
                        CombatLog.Log("Ruta: ENERGIA mano vacia", "Combat");
                    }
                    return;
                }
            }
        }
    }
}
