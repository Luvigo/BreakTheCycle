namespace MundoMental.VR.Combat
{
    /// <summary>Entidades que reciben daño (enemigos, dummies, objetos rompibles).</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, in DamageInfo info);
        bool IsAlive { get; }
    }
}
