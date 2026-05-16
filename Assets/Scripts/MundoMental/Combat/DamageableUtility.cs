using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Unity no expone GetComponent&lt;IDamageable&gt; de forma fiable; usamos búsqueda en padres.</summary>
    public static class DamageableUtility
    {
        public static bool TryGetDamageable(Collider collider, out IDamageable damageable)
        {
            damageable = null;
            if (collider == null)
                return false;

            var t = collider.transform;
            while (t != null)
            {
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c is IDamageable d && d.IsAlive)
                    {
                        damageable = d;
                        return true;
                    }
                }

                t = t.parent;
            }

            // Fallback: enemigos con IDamageable en la raíz del personaje (skinned mesh en hijos, etc.)
            var root = collider.transform.root;
            if (root == null)
                return false;

            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is IDamageable d && d.IsAlive)
                {
                    damageable = d;
                    return true;
                }
            }

            return false;
        }
    }
}
