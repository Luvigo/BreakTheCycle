using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Raycasts y comprobaciones de suelo para IA y jugador.</summary>
    public static class WalkableGroundUtility
    {
        public static bool IsWalkableCollider(Collider col)
        {
            if (col == null || !col.enabled || col.isTrigger)
                return false;
            if (col.GetComponent<WalkableGroundMarker>() != null)
                return true;
            if (col.GetComponentInParent<WalkableGroundMarker>() != null)
                return true;
            return LooksLikeWalkableMeshName(col.gameObject.name);
        }

        public static bool LooksLikeWalkableMeshName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;
            var n = objectName.ToLowerInvariant();
            if (n.Contains("tree") || n.Contains("stone") || n.Contains("rock") || n.Contains("bush") ||
                n.Contains("crystal") || n.Contains("antenna") || n.Contains("planet") ||
                n.Contains("mountain") || n.Contains("mushroom") || n.Contains("flower") ||
                n.Contains("weapon") || n.Contains("skeleton") || n.Contains("hand") ||
                n.Contains("controller") || n.Contains("interactor"))
                return false;
            return n.Contains("ground") || n.Contains("landscape") || n.Contains("terrain") ||
                   n.Contains("floor") || n.Contains("island") || n.Contains("platform") ||
                   n.Contains("sp_ground") || n.Contains("land_");
        }

        /// <summary>
        /// Busca la altura del suelo bajo <paramref name="worldPos"/> (ignora <paramref name="ignoreRoot"/>).
        /// </summary>
        public static bool TryGetGroundHeight(
            Vector3 worldPos,
            Transform ignoreRoot,
            float footOffset,
            float rayStartHeight,
            float rayMaxDistance,
            float minNormalY,
            out float groundY)
        {
            groundY = worldPos.y;
            var origin = worldPos + Vector3.up * Mathf.Max(0.5f, rayStartHeight);
            float maxDist = rayMaxDistance + rayStartHeight;
            var hits = Physics.RaycastAll(origin, Vector3.down, maxDist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return false;

            float bestY = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null)
                    continue;
                if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                    continue;
                if (hit.collider.GetComponentInParent<SkeletonMeleeAi>() != null)
                    continue;
                if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
                    continue;
                if (!IsWalkableCollider(hit.collider))
                    continue;
                if (Vector3.Dot(hit.normal, Vector3.up) < minNormalY)
                    continue;
                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
                return false;

            groundY = bestY + footOffset;
            return true;
        }
    }
}
