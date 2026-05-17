using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

namespace MundoMental.VR.Combat
{
    /// <summary>Vida del jugador: HUD ante el HMD o anclado a la mano derecha (barra + texto).</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] float m_DistanceFromCamera = 0.65f;
        [SerializeField] float m_WorldWidth = 0.55f;

        [Header("Mano (derecha)")]
        [SerializeField] Transform m_FollowHand;
        [SerializeField] Vector3 m_HandLocalPosition = new Vector3(0.08f, 0.03f, 0.2f);
        [SerializeField] Vector3 m_HandLocalEuler = new Vector3(-72f, 12f, 0f);
        [SerializeField] float m_HandHudWorldWidth = 0.34f;
        [SerializeField] [Tooltip("Rota el HUD hacia la cámara del casco aunque esté parentado a la mano (más legible en VR).")]
        bool m_FaceHmdWhileOnHand = true;

        [Header("VR / capas")]
        [SerializeField] int m_CanvasSortingOrder = 32000;
        [SerializeField] [Tooltip("Si >= 0, fuerza esta layer en todo el HUD. -1 = misma layer que la cámara del XR Origin.")]
        int m_ForceUiLayer = -1;

        PlayerHealth m_Health;
        TextMeshProUGUI m_Text;
        Image m_FillImage;
        Canvas m_Canvas;
        Transform m_HudRoot;
        bool m_PendingRecreate;

        public void SetFollowHand(Transform hand)
        {
            if (m_FollowHand == hand)
                return;
            m_FollowHand = hand;
            m_PendingRecreate = true;
        }

        void Awake()
        {
            m_Health = GetComponent<PlayerHealth>();
            if (m_Health == null)
                m_Health = GetComponentInParent<PlayerHealth>();
        }

        void OnEnable()
        {
            if (m_Health == null)
            {
                enabled = false;
                return;
            }
            m_Health.HealthChanged += OnHealthChanged;
            EnsureCanvas();
            OnHealthChanged(m_Health.Health, m_Health.MaxHealth);
        }

        void OnDisable()
        {
            if (m_Health != null)
                m_Health.HealthChanged -= OnHealthChanged;
        }

        Camera ResolveRigCamera()
        {
            var origin = GetComponentInParent<XROrigin>();
            if (origin != null && origin.Camera != null)
                return origin.Camera;
            return Camera.main;
        }

        static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer);
        }

        void LateUpdate()
        {
            if (m_PendingRecreate)
            {
                DestroyHudRoot();
                EnsureCanvas();
                OnHealthChanged(m_Health.Health, m_Health.MaxHealth);
                m_PendingRecreate = false;
            }

            if (m_HudRoot == null)
                return;

            var cam = ResolveRigCamera();
            if (cam != null && m_Canvas != null && m_Canvas.worldCamera != cam)
                m_Canvas.worldCamera = cam;

            if (m_FollowHand == null)
            {
                if (cam == null)
                    return;
                var ctr = cam.transform;
                m_HudRoot.position = ctr.position + ctr.forward * m_DistanceFromCamera;
                m_HudRoot.rotation = Quaternion.LookRotation(m_HudRoot.position - ctr.position, Vector3.up);
                return;
            }

            if (m_FaceHmdWhileOnHand && cam != null)
            {
                var p = m_HudRoot.position;
                var toCam = p - cam.transform.position;
                if (toCam.sqrMagnitude > 1e-6f)
                    m_HudRoot.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }
        }

        void DestroyHudRoot()
        {
            if (m_HudRoot == null)
                return;
            if (Application.isPlaying)
                Destroy(m_HudRoot.gameObject);
            else
                DestroyImmediate(m_HudRoot.gameObject);
            m_HudRoot = null;
            m_Canvas = null;
            m_Text = null;
            m_FillImage = null;
        }

        void EnsureCanvas()
        {
            if (m_HudRoot != null)
                return;

            m_HudRoot = new GameObject("PlayerHealthHud").transform;
            if (m_FollowHand != null)
            {
                m_HudRoot.SetParent(m_FollowHand, false);
                m_HudRoot.localPosition = m_HandLocalPosition;
                m_HudRoot.localRotation = Quaternion.Euler(m_HandLocalEuler);
            }
            else
                m_HudRoot.SetParent(transform, false);

            m_Canvas = m_HudRoot.gameObject.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            var cam = ResolveRigCamera();
            m_Canvas.worldCamera = cam;
            m_Canvas.sortingOrder = m_CanvasSortingOrder;
            m_Canvas.vertexColorAlwaysGammaSpace = true;

            var rt = m_Canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(560f, 72f);
            float targetWidth = m_FollowHand != null ? m_HandHudWorldWidth : m_WorldWidth;
            var scale = targetWidth / rt.rect.width;
            rt.localScale = Vector3.one * scale;

            int uiLayer = m_ForceUiLayer >= 0 ? m_ForceUiLayer : (cam != null ? cam.gameObject.layer : 0);
            SetLayerRecursively(m_HudRoot, uiLayer);

            var bg = new GameObject("Panel").transform;
            bg.SetParent(m_HudRoot, false);
            var bgRt = bg.gameObject.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);

            var barGo = new GameObject("HealthBar");
            barGo.transform.SetParent(m_HudRoot, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.06f, 0.48f);
            barRt.anchorMax = new Vector2(0.94f, 0.82f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;
            var barBg = barGo.AddComponent<Image>();
            barBg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            m_FillImage = fillGo.AddComponent<Image>();
            m_FillImage.color = new Color(0.2f, 0.85f, 0.35f, 1f);
            m_FillImage.type = Image.Type.Filled;
            m_FillImage.fillMethod = Image.FillMethod.Horizontal;
            m_FillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            m_FillImage.fillAmount = 1f;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(m_HudRoot, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 0.45f);
            txtRt.offsetMin = new Vector2(16f, 6f);
            txtRt.offsetMax = new Vector2(-16f, 6f);
            m_Text = txtGo.AddComponent<TextMeshProUGUI>();
            m_Text.alignment = TextAlignmentOptions.Midline;
            m_Text.fontSize = 30f;
            m_Text.color = Color.white;
            m_Text.text = "Vida: ---";
        }

        void OnHealthChanged(float current, float max)
        {
            if (m_Text != null)
                m_Text.text = max > 0f ? $"{current:F0} / {max:F0}" : "---";

            if (m_FillImage != null)
            {
                float t = max > 0f ? Mathf.Clamp01(current / max) : 0f;
                m_FillImage.fillAmount = t;
                m_FillImage.color = Color.Lerp(new Color(0.85f, 0.18f, 0.15f), new Color(0.2f, 0.85f, 0.35f), t);
            }
        }
    }
}
