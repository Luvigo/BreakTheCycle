using UnityEngine;
using UnityEngine.SceneManagement;

namespace MundoMental.VR.Combat
{
    /// <summary>
    /// Añade MeshColliders al suelo visible del mapa (p. ej. Space_Forest) para que enemigos y raycasts encuentren piso.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class WalkableMapGroundSetup : MonoBehaviour
    {
        static bool s_ScenePrepared;

        [SerializeField] bool m_ApplyOnAwake = true;
        [SerializeField] bool m_LogSetup;

        public static void PrepareSceneWalkableGround()
        {
            if (s_ScenePrepared)
                return;

            var setups = FindObjectsByType<WalkableMapGroundSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (setups.Length > 0)
            {
                for (int i = 0; i < setups.Length; i++)
                    setups[i].ApplyToHierarchy();
            }
            else
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    TryApplyByName(root.transform);
            }

            s_ScenePrepared = true;
        }

        static void TryApplyByName(Transform root)
        {
            if (root == null)
                return;
            var n = root.name;
            if (n.Contains("Space_Forest") || n.Contains("Forest Variant") || n.Contains("SP_Ground"))
            {
                var setup = root.GetComponent<WalkableMapGroundSetup>();
                if (setup == null)
                    setup = root.gameObject.AddComponent<WalkableMapGroundSetup>();
                setup.ApplyToHierarchy();
            }

            for (int i = 0; i < root.childCount; i++)
                TryApplyByName(root.GetChild(i));
        }

        void Awake()
        {
            if (m_ApplyOnAwake)
                ApplyToHierarchy();
        }

        void OnEnable()
        {
            if (m_ApplyOnAwake)
                s_ScenePrepared = true;
        }

        [ContextMenu("Apply walkable colliders now")]
        public void ApplyToHierarchy()
        {
            int added = 0;
            var filters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null)
                    continue;
                if (!WalkableGroundUtility.LooksLikeWalkableMeshName(mf.gameObject.name))
                    continue;

                var go = mf.gameObject;
                if (go.GetComponent<WalkableGroundMarker>() == null)
                    go.AddComponent<WalkableGroundMarker>();

                var mc = go.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                    added++;
                }
                else if (mc.sharedMesh == null)
                {
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                }
            }

            int marked = MarkExistingTerrainColliders();
            if (m_LogSetup)
                CombatLog.Log($"Mapa '{name}': +{added} MeshCollider(s), {marked} marcados como suelo.", "Combat");
        }

        int MarkExistingTerrainColliders()
        {
            int marked = 0;
            var meshColliders = GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                var mc = meshColliders[i];
                if (mc == null || !mc.enabled || mc.isTrigger || mc.convex)
                    continue;
                if (!WalkableGroundUtility.LooksLikeWalkableMeshName(mc.gameObject.name) &&
                    !mc.gameObject.name.StartsWith("Landscape", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (mc.GetComponent<WalkableGroundMarker>() == null)
                {
                    mc.gameObject.AddComponent<WalkableGroundMarker>();
                    marked++;
                }
            }
            return marked;
        }
    }
}
