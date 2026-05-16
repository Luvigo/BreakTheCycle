using UnityEngine;

namespace MundoMental.VR.Combat
{
    public static class CombatLog
    {
        public const string Prefix = "[MentalVR]";

        public static void Log(string message, string subsystem)
        {
            Debug.Log($"{Prefix} [{subsystem}] {message}", null);
        }

        public static void Warn(string message, string subsystem)
        {
            Debug.LogWarning($"{Prefix} [{subsystem}] {message}", null);
        }
    }
}
