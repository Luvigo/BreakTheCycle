using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Estado de la mano derecha: vacía, pistola o espada. Las armas se registran vía <see cref="GrabbableWeapon"/>.</summary>
    [AddComponentMenu("Mundo Mental VR/Hand Equipment Tracker")]
    public class HandEquipmentTracker : MonoBehaviour
    {
        public enum RightHandMode
        {
            Empty = 0,
            Pistol = 1,
            Sword = 2,
        }

        [Header("Mano dominante (referencia para lógica)")]
        [SerializeField] Transform m_RightHandControllerRoot;
        [SerializeField] Transform m_LeftHandControllerRoot;

        public RightHandMode CurrentRightMode { get; private set; } = RightHandMode.Empty;
        public GunWeapon EquippedPistol { get; private set; }
        public SwordWeapon EquippedSword { get; private set; }

        public Transform RightHandControllerRoot => m_RightHandControllerRoot;
        public Transform LeftHandControllerRoot => m_LeftHandControllerRoot;

        public void RegisterRightPistol(GunWeapon gun)
        {
            if (gun == null)
                return;
            CurrentRightMode = RightHandMode.Pistol;
            EquippedPistol = gun;
            EquippedSword = null;
            CombatLog.Log("Equipo: mano derecha = PISTOLA", "HandEquipment");
        }

        public void RegisterRightSword(SwordWeapon sword)
        {
            if (sword == null)
                return;
            CurrentRightMode = RightHandMode.Sword;
            EquippedSword = sword;
            EquippedPistol = null;
            CombatLog.Log("Equipo: mano derecha = ESPADA", "HandEquipment");
        }

        public void ClearRightHand()
        {
            CurrentRightMode = RightHandMode.Empty;
            EquippedPistol = null;
            EquippedSword = null;
            CombatLog.Log("Equipo: mano derecha = VACÍA", "HandEquipment");
        }
    }
}
