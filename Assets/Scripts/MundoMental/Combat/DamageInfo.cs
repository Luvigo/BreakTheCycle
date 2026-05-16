using UnityEngine;

namespace MundoMental.VR.Combat
{
    public enum DamageSource
    {
        None = 0,
        HandEnergy = 1,
        Gun = 2,
        Sword = 3,
        Environment = 4,
        Blunt = 5,
    }

    public struct DamageInfo
    {
        public DamageSource Source;
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public Collider Collider;
        public GameObject Instigator;

        public static DamageInfo Make(
            DamageSource source,
            float amount,
            Vector3 hitPoint,
            Vector3 normal,
            Collider c,
            GameObject instigator)
        {
            return new DamageInfo
            {
                Source = source,
                Amount = amount,
                HitPoint = hitPoint,
                HitNormal = normal,
                Collider = c,
                Instigator = instigator,
            };
        }
    }
}
