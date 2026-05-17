using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MundoMental.VR.Combat
{
    /// <summary>Escudo de mano izquierda. Activa malla y collider; detiene <see cref="EnergyProjectile"/> por trigger.</summary>
    public class ShieldController : MonoBehaviour
    {
        [Header("Visuales y colisión")]
        [SerializeField] GameObject m_ShieldRoot;
        [SerializeField] Collider m_ShieldTrigger;
        [SerializeField] [Tooltip("Si el mismo botón que agarra dispara la acción de bloqueo, evita toggles mientras XRGrabInteractable está seleccionado.")] bool m_SuppressToggleWhileGrabbed = true;
        [SerializeField] [Tooltip("Si está activo, la malla se oculta cuando el escudo está desactivado. Para escudos agarrables suele ser false.")] bool m_HideVisualWhenInactive = false;
        [Header("Energía / cooldown básico")]
        [SerializeField] float m_Energy = 12f;
        [SerializeField] float m_EnergyBlockCost = 1f;
        [SerializeField] float m_RecoverPerSecond = 1.5f;
        [Header("Input (mando izquierdo) — arrastra de XRI Default Input Actions")]
        [SerializeField] InputActionReference m_ToggleOrHoldAction;
        [SerializeField] bool m_ToggleOnPress = true;
        [SerializeField] [Tooltip("Si el escudo requiere energía mínima para levantar")] float m_MinEnergyToRaise = 0.5f;

        XRGrabInteractable m_Grab;

        public bool IsBlocking { get; private set; }
        public float Energy => m_Energy;
        public float MaxEnergy { get; private set; } = 12f;

        void Reset()
        {
            MaxEnergy = m_Energy;
        }

        void Awake()
        {
            MaxEnergy = m_Energy;
            TryGetComponent(out m_Grab);
        }

        void OnEnable()
        {
            if (m_ToggleOrHoldAction != null && m_ToggleOrHoldAction.action != null)
            {
                m_ToggleOrHoldAction.action.performed += OnAction;
                m_ToggleOrHoldAction.action.canceled += OnActionCancel;
            }

            if (m_ShieldRoot != null)
                m_ShieldRoot.SetActive(!m_HideVisualWhenInactive);
            IsBlocking = false;
            if (m_ShieldTrigger != null)
                m_ShieldTrigger.enabled = false;
        }

        void OnDisable()
        {
            if (m_ToggleOrHoldAction != null && m_ToggleOrHoldAction.action != null)
            {
                m_ToggleOrHoldAction.action.performed -= OnAction;
                m_ToggleOrHoldAction.action.canceled -= OnActionCancel;
            }
        }

        void Update()
        {
            m_Energy = Mathf.Min(MaxEnergy, m_Energy + m_RecoverPerSecond * Time.deltaTime);
        }

        void OnAction(InputAction.CallbackContext ctx)
        {
            if (m_SuppressToggleWhileGrabbed && m_Grab != null && m_Grab.isSelected)
                return;

            if (m_ToggleOnPress)
            {
                if (m_Energy < m_MinEnergyToRaise)
                {
                    CombatLog.Log("Escudo: sin energía; no se activa", "Shield");
                    return;
                }
                IsBlocking = !IsBlocking;
                ApplyVisual();
                CombatLog.Log(IsBlocking ? "Escudo: ACTIVO" : "Escudo: desactivado", "Shield");
            }
            else
            {
                if (m_Energy < m_MinEnergyToRaise)
                    return;
                IsBlocking = true;
                ApplyVisual();
                CombatLog.Log("Escudo: ACTIVO (sostenido)", "Shield");
            }
        }

        void OnActionCancel(InputAction.CallbackContext ctx)
        {
            if (m_ToggleOnPress)
                return;
            IsBlocking = false;
            ApplyVisual();
            CombatLog.Log("Escudo: desactivado (suelto)", "Shield");
        }

        void ApplyVisual()
        {
            if (m_ShieldRoot != null)
                m_ShieldRoot.SetActive(m_HideVisualWhenInactive ? IsBlocking : true);
            if (m_ShieldTrigger != null)
                m_ShieldTrigger.enabled = IsBlocking;
        }

        /// <summary>Llamada opcional por proyectiles avanzados para consumo.</summary>
        public void RegisterBlock()
        {
            m_Energy = Mathf.Max(0f, m_Energy - m_EnergyBlockCost);
            if (m_Energy < m_MinEnergyToRaise)
            {
                IsBlocking = false;
                ApplyVisual();
                CombatLog.Log("Escudo: sin energía — se baja", "Shield");
            }
        }

        /// <summary>Bloqueo de daño melee: cualquier escudo activo bajo el mismo rig que el jugador.</summary>
        public static bool IsAnyShieldBlockingPlayerRoot(Transform anyTransformOnPlayer)
        {
            if (anyTransformOnPlayer == null)
                return false;
            foreach (var s in anyTransformOnPlayer.root.GetComponentsInChildren<ShieldController>(true))
            {
                if (s != null && s.IsBlocking)
                    return true;
            }
            return false;
        }
    }
}
