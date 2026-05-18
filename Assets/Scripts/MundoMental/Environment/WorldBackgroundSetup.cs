using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MundoMental.VR.Environment
{
    /// <summary>Fondo (cielo/horizonte). Visible en Scene y en Play.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [DefaultExecutionOrder(-300)]
    public sealed class WorldBackgroundSetup : MonoBehaviour
    {
        const string DomeChildName = "WorldBackgroundDome";

        [SerializeField] Texture2D m_BackgroundTexture;
        [SerializeField] [Tooltip("Recomendado: se ve en la vista Scene sin Play.")]
        bool m_UsePanoramicSkybox = true;
        [SerializeField] float m_DomeRadius = 220f;
        [SerializeField] Vector3 m_WorldCenter = new Vector3(0f, 8f, 0f);
        [SerializeField] bool m_FollowPlayerXZ = true;
        [SerializeField] [Range(0.15f, 1.5f)] float m_Exposure = 0.35f;

        Transform m_DomeTransform;
        Material m_RuntimeMaterial;

        void OnEnable() => Apply();

        void OnDisable()
        {
            if (Application.isPlaying)
                Cleanup();
        }

        void OnDestroy() => Cleanup();

        void LateUpdate()
        {
            if (!Application.isPlaying || !m_FollowPlayerXZ || m_UsePanoramicSkybox)
                return;
            if (m_DomeTransform == null)
                return;

            var origin = FindFirstObjectByType<XROrigin>();
            Vector3 anchor = origin != null ? origin.transform.position : m_WorldCenter;
            m_DomeTransform.position = new Vector3(anchor.x, m_WorldCenter.y, anchor.z);
        }

        public void Apply()
        {
            CleanupDomeOnly();
            if (m_BackgroundTexture == null)
                return;

            if (m_UsePanoramicSkybox)
                ApplyPanoramicSkybox();
            else
                ApplyInteriorDome();
        }

        void ApplyPanoramicSkybox()
        {
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogWarning("WorldBackgroundSetup: Skybox/Panoramic no encontrado; usando cúpula.");
                m_UsePanoramicSkybox = false;
                ApplyInteriorDome();
                return;
            }

            if (m_RuntimeMaterial == null || m_RuntimeMaterial.shader != shader)
            {
                if (m_RuntimeMaterial != null)
                    DestroyMaterial(m_RuntimeMaterial);
                m_RuntimeMaterial = new Material(shader);
                m_RuntimeMaterial.name = "WorldBackground_Skybox";
            }

            m_RuntimeMaterial.SetTexture("_MainTex", m_BackgroundTexture);
            m_RuntimeMaterial.SetFloat("_Exposure", m_Exposure);
            m_RuntimeMaterial.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = m_RuntimeMaterial;
            RenderSettings.ambientMode = AmbientMode.Skybox;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneView.RepaintAll();
#endif
        }

        void ApplyInteriorDome()
        {
            RenderSettings.skybox = null;

            var existing = transform.Find(DomeChildName);
            GameObject domeGo;
            if (existing != null)
                domeGo = existing.gameObject;
            else
            {
                domeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                domeGo.name = DomeChildName;
                domeGo.transform.SetParent(transform, false);
                var col = domeGo.GetComponent<Collider>();
                if (col != null)
                    DestroyObject(col);
            }

            float r = Mathf.Max(20f, m_DomeRadius);
            domeGo.transform.localScale = new Vector3(-r, r, r);
            domeGo.transform.position = m_WorldCenter;
            m_DomeTransform = domeGo.transform;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");

            if (m_RuntimeMaterial == null || m_RuntimeMaterial.shader != shader)
            {
                if (m_RuntimeMaterial != null)
                    DestroyMaterial(m_RuntimeMaterial);
                m_RuntimeMaterial = new Material(shader);
                m_RuntimeMaterial.name = "WorldBackground_Dome";
            }

            if (m_RuntimeMaterial.HasProperty("_BaseMap"))
                m_RuntimeMaterial.SetTexture("_BaseMap", m_BackgroundTexture);
            if (m_RuntimeMaterial.HasProperty("_MainTex"))
                m_RuntimeMaterial.SetTexture("_MainTex", m_BackgroundTexture);
            if (m_RuntimeMaterial.HasProperty("_BaseColor"))
                m_RuntimeMaterial.SetColor("_BaseColor", Color.white * m_Exposure);
            if (m_RuntimeMaterial.HasProperty("_Cull"))
                m_RuntimeMaterial.SetInt("_Cull", (int)CullMode.Front);
            m_RuntimeMaterial.renderQueue = (int)RenderQueue.Background;

            var mr = domeGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m_RuntimeMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void CleanupDomeOnly()
        {
            var existing = transform.Find(DomeChildName);
            if (existing != null)
                DestroyObject(existing.gameObject);
            m_DomeTransform = null;
        }

        void Cleanup()
        {
            CleanupDomeOnly();
            if (m_RuntimeMaterial != null)
            {
                DestroyMaterial(m_RuntimeMaterial);
                m_RuntimeMaterial = null;
            }
            if (RenderSettings.skybox != null && RenderSettings.skybox.name.Contains("WorldBackground"))
                RenderSettings.skybox = null;
        }

        static void DestroyObject(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        static void DestroyMaterial(Material mat) => DestroyObject(mat);
    }
}
