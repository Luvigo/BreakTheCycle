#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MundoMental.VR.Combat
{
    /// <summary>Cuando no queden <see cref="EnemyTestDummy"/> vivos, muestra texto y opción de reiniciar nivel.</summary>
    [DisallowMultipleComponent]
    public sealed class AllSkeletonsClearedMenu : MonoBehaviour
    {
        [SerializeField] KeyCode m_RestartKey = KeyCode.R;

        bool m_PanelVisible;
        GameObject m_RootPanel;

        void OnEnable()
        {
            EnemyTestDummy.Died += OnEnemyDied;
        }

        void OnDisable()
        {
            EnemyTestDummy.Died -= OnEnemyDied;
        }

        void Update()
        {
            if (!m_PanelVisible)
                return;
#if ENABLE_INPUT_SYSTEM
            var kbd = Keyboard.current;
            if (kbd != null && kbd.rKey.wasPressedThisFrame)
                ReloadScene();
#else
            if (Input.GetKeyDown(m_RestartKey))
                ReloadScene();
#endif
        }

        void OnEnemyDied(EnemyTestDummy _)
        {
            if (CountLivingDummies() > 0)
                return;
            ShowPanel();
        }

        int CountLivingDummies()
        {
            var list = FindObjectsByType<EnemyTestDummy>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].IsAlive)
                    n++;
            }
            return n;
        }

        void LateUpdate()
        {
            if (!m_PanelVisible || m_RootPanel == null || Camera.main == null)
                return;
            var cam = Camera.main.transform;
            m_RootPanel.transform.position = cam.position + cam.forward * 0.95f;
            m_RootPanel.transform.rotation = Quaternion.LookRotation(m_RootPanel.transform.position - cam.position, Vector3.up);
        }

        void ShowPanel()
        {
            if (m_PanelVisible)
                return;
            m_PanelVisible = true;
            EnsureXrUiInput();
            BuildUi();
            CombatLog.Log("Nivel completado: no quedan enemigos (prueba).", "Game");
        }

        void BuildUi()
        {
            if (m_RootPanel != null)
            {
                m_RootPanel.SetActive(true);
                return;
            }

            var cam = Camera.main;
            if (cam == null)
                return;

            m_RootPanel = new GameObject("VictoryRestartPanel");
            var canvas = m_RootPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 100;
            var rt = m_RootPanel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 420f);
            float scale = 0.0015f;
            rt.localScale = Vector3.one * scale;
            m_RootPanel.transform.position = cam.transform.position + cam.transform.forward * 0.9f;
            m_RootPanel.transform.rotation = Quaternion.LookRotation(m_RootPanel.transform.position - cam.transform.position, Vector3.up);
            m_RootPanel.AddComponent<TrackedDeviceGraphicRaycaster>();

            var bg = new GameObject("Backdrop");
            bg.transform.SetParent(m_RootPanel.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.12f, 0.92f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(m_RootPanel.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.06f, 0.58f);
            titleRt.anchorMax = new Vector2(0.94f, 0.94f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "¡Zona despejada!";
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 56f;
            title.color = Color.white;

            var hint = new GameObject("Hint");
            hint.transform.SetParent(m_RootPanel.transform, false);
            hint.AddComponent<RectTransform>();
            var hintRt = hint.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.06f, 0.38f);
            hintRt.anchorMax = new Vector2(0.94f, 0.56f);
            var hintTxt = hint.AddComponent<TextMeshProUGUI>();
            hintTxt.text = "Apunta con el mando (rayo) al botón y pulsa el disparador, o «R» en teclado (PC).";
            hintTxt.fontSize = 30f;
            hintTxt.alignment = TextAlignmentOptions.Center;
            hintTxt.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var btnGo = new GameObject("RestartButton");
            btnGo.transform.SetParent(m_RootPanel.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.25f, 0.1f);
            btnRt.anchorMax = new Vector2(0.75f, 0.32f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.55f, 0.95f, 1f);
            var restartBtn = btnGo.AddComponent<Button>();
            restartBtn.onClick.AddListener(ReloadScene);

            var btnLabelGo = new GameObject("Label");
            btnLabelGo.transform.SetParent(btnGo.transform, false);
            var lblRt = btnLabelGo.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;
            var btnTxt = btnLabelGo.AddComponent<TextMeshProUGUI>();
            btnTxt.text = "Reiniciar nivel";
            btnTxt.fontSize = 38f;
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.color = Color.white;
        }

        void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
        }

        /// <summary>
        /// StandaloneInputModule no recibe el rayo XR; hace falta <see cref="XRUIInputModule"/> + <see cref="TrackedDeviceGraphicRaycaster"/>.
        /// </summary>
        static void EnsureXrUiInput()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem_Auto");
                es = go.AddComponent<EventSystem>();
            }

            if (es.GetComponent<XRUIInputModule>() != null)
                return;

            foreach (var sim in es.GetComponents<StandaloneInputModule>())
                Object.Destroy(sim);
#if ENABLE_INPUT_SYSTEM
            foreach (var im in es.GetComponents<UnityEngine.InputSystem.UI.InputSystemUIInputModule>())
                Object.Destroy(im);
#endif
            es.gameObject.AddComponent<XRUIInputModule>();
        }
    }
}
