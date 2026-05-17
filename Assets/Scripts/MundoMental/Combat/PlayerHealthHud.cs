using TMPro;
using UnityEngine;

namespace MundoMental.VR.Combat
{
    /// <summary>Muestra vida actual del <see cref="PlayerHealth"/> en un canvas mundo ante el HMD.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] float m_DistanceFromCamera = 0.65f;
        [SerializeField] float m_WorldWidth = 0.55f;
        PlayerHealth m_Health;
        TextMeshProUGUI m_Text;
        Canvas m_Canvas;
        Transform m_HudRoot;

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

        void LateUpdate()
        {
            if (m_HudRoot == null || Camera.main == null)
                return;
            var cam = Camera.main.transform;
            m_HudRoot.position = cam.position + cam.forward * m_DistanceFromCamera;
            m_HudRoot.rotation = Quaternion.LookRotation(m_HudRoot.position - cam.position, Vector3.up);
        }

        void EnsureCanvas()
        {
            if (m_HudRoot != null)
                return;
            m_HudRoot = new GameObject("PlayerHealthHud").transform;
            m_HudRoot.SetParent(transform, false);
            m_Canvas = m_HudRoot.gameObject.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            m_Canvas.worldCamera = Camera.main;
            var rt = m_Canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 120f);
            var scale = m_WorldWidth / rt.rect.width;
            rt.localScale = Vector3.one * scale;

            var bg = new GameObject("Panel").transform;
            bg.SetParent(m_HudRoot, false);
            var bgRt = bg.gameObject.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.gameObject.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.45f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(m_HudRoot, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(22f, 12f);
            txtRt.offsetMax = new Vector2(-22f, -12f);
            m_Text = txtGo.AddComponent<TextMeshProUGUI>();
            m_Text.alignment = TextAlignmentOptions.MidlineLeft;
            m_Text.fontSize = 38f;
            m_Text.color = Color.white;
            m_Text.text = "Vida: ---";
        }

        void OnHealthChanged(float current, float max)
        {
            if (m_Text == null)
                return;
            m_Text.text = $"Vida: {current:F0} / {max:F0}";
        }
    }
}
