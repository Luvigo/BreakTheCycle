using MundoMental.VR.Combat;
using UnityEngine;

namespace MundoMental.VR.Environment
{
    /// <summary>Esqueleto Boss visible en Scene y Play, hijo de BossAnchor.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class BossSpawner : MonoBehaviour
    {
        const string SkeletonPrefabPath = "Assets/Skeleton/Prefab/Skeleton.prefab";
        const string SpawnedChildName = "Boss";

        [SerializeField] GameObject m_SkeletonPrefab;
        [SerializeField] float m_Scale = 10f;
        [SerializeField] bool m_FacePlayerOnSpawn = true;

        GameObject m_Spawned;

        void OnEnable()
        {
            TryResolvePrefab();
            SpawnIfNeeded();
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                CleanupSpawned();
        }

        void OnDestroy() => CleanupSpawned();

        void TryResolvePrefab()
        {
            if (m_SkeletonPrefab != null)
                return;
#if UNITY_EDITOR
            m_SkeletonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);
#endif
        }

        void SpawnIfNeeded()
        {
            if (m_SkeletonPrefab == null)
                return;

            var existing = transform.Find(SpawnedChildName);
            if (existing != null)
            {
                m_Spawned = existing.gameObject;
                ApplyBossSettings();
                return;
            }

            m_Spawned = Instantiate(m_SkeletonPrefab, transform);
            m_Spawned.name = SpawnedChildName;
            ApplyBossSettings();

            if (Application.isPlaying)
                CombatLog.Log($"Boss listo en {m_Spawned.transform.position} escala {m_Scale}", "Boss");
        }

        void ApplyBossSettings()
        {
            if (m_Spawned == null)
                return;

            m_Spawned.transform.localPosition = Vector3.zero;
            m_Spawned.transform.localRotation = Quaternion.identity;
            m_Spawned.transform.localScale = Vector3.one * Mathf.Max(1f, m_Scale);

            foreach (var r in m_Spawned.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                if (r is SkinnedMeshRenderer smr)
                    smr.updateWhenOffscreen = true;
            }

            var anim = m_Spawned.GetComponentInChildren<Animator>(true);
            if (anim != null)
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (Application.isPlaying && m_FacePlayerOnSpawn)
            {
                var player = FindFirstObjectByType<PlayerHealth>();
                if (player != null)
                {
                    var planar = player.transform.position - m_Spawned.transform.position;
                    planar.y = 0f;
                    if (planar.sqrMagnitude > 0.01f)
                        m_Spawned.transform.rotation = Quaternion.LookRotation(planar.normalized, Vector3.up);
                }
            }
        }

        void CleanupSpawned()
        {
            if (m_Spawned == null)
                return;
            if (Application.isPlaying)
                Destroy(m_Spawned);
            else
                DestroyImmediate(m_Spawned);
            m_Spawned = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 3f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 14f);
        }
    }
}
