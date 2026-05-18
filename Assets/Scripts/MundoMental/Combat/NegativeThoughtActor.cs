using System.Collections;
using System.Reflection;
using MundoMental.VR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace MundoMental.VR.Combat
{
    /// <summary>Aura estilo “ki”, frase en TextMeshPro 3D y audio 3D.</summary>
    [DisallowMultipleComponent]
    public sealed class NegativeThoughtActor : MonoBehaviour
    {
        const string AuraChildName = "NegativeThoughtAura";
        const string LabelChildName = "ThoughtLabel";

        [Header("Frase")]
        [SerializeField] NegativeThoughtPhraseBank m_PhraseBank;
        [SerializeField] string m_FallbackPhrase = "No estás solo en esto.";
        [Tooltip("Metros por encima del hueso Head (humanoides).")]
        [SerializeField] float m_LabelOffsetY = 0.58f;
        [Tooltip("Si no hay cabeza / no es humanoide, altura sobre la raíz del enemigo.")]
        [SerializeField] float m_LabelFallbackWorldHeight = 2.25f;
        [SerializeField] bool m_AnchorLabelToHeadBone = true;
        [SerializeField] float m_WorldLabelWidthMeters = 1.55f;
        [Tooltip("Escala del objeto TMP en mundo (VR: ~0.02–0.04 suele verse bien).")]
        [SerializeField] float m_LabelRootWorldScale = 0.028f;
        [SerializeField] float m_TmpFontSize = 42f;
        [SerializeField] Color m_LabelColor = new Color(0.96f, 0.93f, 0.93f, 1f);

        [Header("Aura estilo ki (partículas desde el cuerpo)")]
        [Tooltip("Activo: brillo tipo ki — partículas suaves emitidas desde el mesh del personaje.")]
        [SerializeField] bool m_UseSoftBillboardAura = true;
        [SerializeField] [Range(8f, 200f)] float m_KiEmissionRate = 118f;
        [SerializeField] [Range(0.02f, 0.35f)] float m_KiStartSpeedMax = 0.22f;
        [SerializeField] [Range(0.015f, 0.25f)] float m_KiStartSizeMin = 0.04f;
        [SerializeField] [Range(0.02f, 0.35f)] float m_KiStartSizeMax = 0.11f;
        [SerializeField] [Range(0.15f, 1.2f)] float m_KiLifetimeMin = 0.38f;
        [SerializeField] [Range(0.2f, 1.6f)] float m_KiLifetimeMax = 0.85f;
        [SerializeField] [Range(0.02f, 0.55f)] float m_KiMaxAlpha = 0.32f;
        [SerializeField] Vector3 m_AuraLocalCenter = new Vector3(0f, 0f, 0f);
        [SerializeField] Color m_AuraColor = new Color(0.06f, 0.06f, 0.1f, 1f);
        [SerializeField] int m_AuraLayer = -1;
        [SerializeField, FormerlySerializedAs("m_AuraMaterialOptionalOverride")]
        Material m_KiParticleMaterialOptionalOverride;

        [Header("Susurro (3D)")]
        [SerializeField] AudioClip m_WhisperLoopClip;
        [SerializeField] float m_WhisperBaseVolume = 0.08f;
        [SerializeField] float m_WhisperMinDistance = 1.2f;
        [SerializeField] float m_WhisperMaxDistance = 14f;
        [SerializeField] AnimationCurve m_VolumeByNormalizedDistance =
            AnimationCurve.Linear(0f, 1f, 1f, 0.08f);

        [Header("Susurro — filtro")]
        [SerializeField] bool m_UseLowPassByDistance = true;
        [SerializeField] float m_LowpassFarHz = 900f;
        [SerializeField] float m_LowpassNearHz = 12000f;
        [SerializeField] AnimationCurve m_LowpassCurve =
            AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Alivio al derrotar")]
        [SerializeField] AudioClip m_AudioCut;
        [SerializeField] AudioClip m_AudioBreath;
        [SerializeField] AudioClip m_AudioBell;
        [SerializeField] float m_ReliefVolume = 0.55f;
        [SerializeField] float m_WhisperFadeOutSeconds = 0.35f;
        [SerializeField] float m_ReliefStaggerSeconds = 0.04f;

        Transform m_AuraTransform;
        Transform m_LabelRoot;
        TextMeshPro m_LabelTmp;
        AudioSource m_Whisper;
        AudioSource m_Relief;
        AudioLowPassFilter m_LowPass;
        Animator m_Animator;
        bool m_Defeated;
        Coroutine m_FadeRoutine;

        public void OnDefeated()
        {
            if (m_Defeated)
                return;
            m_Defeated = true;
            if (m_FadeRoutine != null)
                StopCoroutine(m_FadeRoutine);
            m_FadeRoutine = StartCoroutine(CoDefeatAudioAndHide());
        }

        void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
            VrAudioListenerUtility.EnsureSingleListenerOnRigCamera();
            EnsureAura();
            EnsureLabel();
            EnsureAudio();
        }

        void Start()
        {
            StartCoroutine(CoBindLabelNextFrames());
        }

        IEnumerator CoBindLabelNextFrames()
        {
            yield return null;
            yield return null;
            RefreshLabelTmp();
            yield return null;
            RefreshLabelTmp();
        }

        static Texture2D s_SharedBlobTexture;

        void ApplyPhrase()
        {
            if (m_LabelTmp == null)
                return;
            string t = m_PhraseBank != null && m_PhraseBank.PhraseCount > 0
                ? m_PhraseBank.PickRandom()
                : m_FallbackPhrase;
            if (string.IsNullOrWhiteSpace(t))
                t = m_FallbackPhrase;
            m_LabelTmp.text = t;
            m_LabelTmp.ForceMeshUpdate(true);
        }

        void RefreshLabelTmp()
        {
            if (m_LabelTmp == null)
                return;
            var font = m_LabelTmp.font != null ? m_LabelTmp.font : ResolveTmpFont();
            if (font != null)
            {
                m_LabelTmp.font = font;
                if (font.material != null)
                    m_LabelTmp.fontSharedMaterial = font.material;
            }
            float pt = Mathf.Clamp(m_TmpFontSize * 0.45f, 14f, 56f);
            m_LabelTmp.enableAutoSizing = true;
            m_LabelTmp.fontSizeMin = 12f;
            m_LabelTmp.fontSizeMax = pt;
            m_LabelTmp.fontSize = pt;
            m_LabelTmp.color = new Color(m_LabelColor.r, m_LabelColor.g, m_LabelColor.b, 1f);
            ApplyPhrase();
            m_LabelTmp.ForceMeshUpdate(true);
        }

        static TMP_FontAsset ResolveTmpFont()
        {
            var f = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (f != null)
                return f;
            f = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            if (f != null)
                return f;
            return TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
        }

        void EnsureAura()
        {
            if (!IsAlive(this) || !IsAlive(transform))
                return;

            DestroyNamedChild(transform, AuraChildName);

            var auraRoot = new GameObject(AuraChildName);
            auraRoot.transform.SetParent(transform, false);
            auraRoot.transform.localPosition = m_AuraLocalCenter;
            m_AuraTransform = auraRoot.transform;

            if (m_UseSoftBillboardAura)
                BuildKiParticleAura(auraRoot.transform);
            else
                BuildLegacySphereAura(auraRoot.transform);

            int layer = m_AuraLayer >= 0 ? m_AuraLayer : gameObject.layer;
            if (IsAlive(auraRoot.transform))
                SetLayerRecursively(auraRoot.transform, layer);
        }

        void BuildKiParticleAura(Transform parent)
        {
            var smr = PickPrimarySkinnedMeshRenderer();
            var psGo = new GameObject("KiParticles");
            psGo.transform.SetParent(parent, false);
            psGo.transform.localPosition = Vector3.zero;
            psGo.transform.localRotation = Quaternion.identity;
            psGo.transform.localScale = Vector3.one;

            var ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = false;
            main.maxParticles = 1100;
            main.gravityModifier = 0f;

            main.startLifetime = new ParticleSystem.MinMaxCurve(m_KiLifetimeMin, m_KiLifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, m_KiStartSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(m_KiStartSizeMin, m_KiStartSizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Color c0 = new Color(m_AuraColor.r, m_AuraColor.g, m_AuraColor.b, m_KiMaxAlpha);
            main.startColor = new ParticleSystem.MinMaxGradient(c0);

            var emission = ps.emission;
            emission.rateOverTime = m_KiEmissionRate;

            var shape = ps.shape;
            shape.enabled = true;
            if (TryBindShapeToBodyMesh(shape, smr))
            {
                // ok
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.45f;
                shape.radiusThickness = 0.85f;
            }

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            // Radial alto + emisión en volumen parece “burbuja”; el ki sale más de vértices/normales + ruido.
            vel.radial = new ParticleSystem.MinMaxCurve(0f, 0.06f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            var a0 = c0.a;
            g.SetKeys(
                new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c0, 1f) },
                new[] { new GradientAlphaKey(Mathf.Clamp01(a0 * 0.95f), 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sz = new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.45f, 1f), new Keyframe(1f, 0.08f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sz);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.18f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.sortingOrder = 1800;
            rend.motionVectorGenerationMode = MotionVectorGenerationMode.Object;

            Material mat;
            if (m_KiParticleMaterialOptionalOverride != null)
                mat = new Material(m_KiParticleMaterialOptionalOverride);
            else
                mat = CreateKiParticleMaterial(c0);
            if (mat != null)
                rend.material = mat;

            DisableSmallMeshCulling(rend);
            ps.Play();
        }

        static Material CreateKiParticleMaterial(Color tint)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
                return null;

            if (s_SharedBlobTexture == null)
                s_SharedBlobTexture = BuildSoftBlobTexture(48);

            var mat = new Material(sh) { name = "NegativeThought_Ki_Runtime" };
            if (!mat.HasProperty("_BaseMap"))
            {
                Object.Destroy(mat);
                return null;
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.renderQueue = (int)RenderQueue.Transparent + 50;
            mat.SetTexture("_BaseMap", s_SharedBlobTexture);
            mat.SetColor("_BaseColor", Color.white);
            return mat;
        }

        static Texture2D BuildSoftBlobTexture(int size)
        {
            int n = Mathf.Clamp(size, 16, 128);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float cx = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - cx) / Mathf.Max(cx, 1f);
                    float dy = (y - cx) / Mathf.Max(cx, 1f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d * d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, true);
            return tex;
        }

        bool TryBindShapeToBodyMesh(ParticleSystem.ShapeModule shape, SkinnedMeshRenderer smrPreferred)
        {
            if (smrPreferred != null && smrPreferred.sharedMesh != null)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = smrPreferred;
                shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
                return true;
            }

            var meshPair = PickPrimaryReadableMeshFilter();
            if (meshPair.mf != null && meshPair.mf.sharedMesh != null)
            {
                shape.shapeType = ParticleSystemShapeType.Mesh;
                shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
                shape.mesh = meshPair.mf.sharedMesh;
                if (meshPair.mr != null)
                    shape.meshRenderer = meshPair.mr;
                return true;
            }

            return false;
        }

        (MeshFilter mf, MeshRenderer mr) PickPrimaryReadableMeshFilter()
        {
            var mfs = GetComponentsInChildren<MeshFilter>();
            MeshFilter best = null;
            MeshRenderer bestMr = null;
            float bestV = -1f;
            if (mfs != null)
            {
                for (var i = 0; i < mfs.Length; i++)
                {
                    var mf = mfs[i];
                    if (mf == null || mf.sharedMesh == null)
                        continue;
                    if (IsUnderNamedTransform(mf.transform, AuraChildName))
                        continue;
                    var mr = mf.GetComponent<MeshRenderer>();
                    var b = mr != null ? mr.bounds : new Bounds(mf.transform.position, Vector3.zero);
                    float v = b.size.sqrMagnitude;
                    if (v > bestV)
                    {
                        bestV = v;
                        best = mf;
                        bestMr = mr;
                    }
                }
            }

            return (best, bestMr);
        }

        static bool IsUnderNamedTransform(Transform leaf, string ancestorName)
        {
            for (var t = leaf; t != null; t = t.parent)
            {
                if (t.name == ancestorName)
                    return true;
            }

            return false;
        }

        SkinnedMeshRenderer PickPrimarySkinnedMeshRenderer()
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (smrs == null || smrs.Length == 0)
                return null;
            SkinnedMeshRenderer best = smrs[0];
            float bestV = best.bounds.size.sqrMagnitude;
            for (var i = 1; i < smrs.Length; i++)
            {
                float v = smrs[i].bounds.size.sqrMagnitude;
                if (v > bestV)
                {
                    bestV = v;
                    best = smrs[i];
                }
            }
            return best;
        }

        void BuildLegacySphereAura(Transform auraRoot)
        {
            var tint = new Color(m_AuraColor.r, m_AuraColor.g, m_AuraColor.b, Mathf.Clamp01(m_KiMaxAlpha * 1.15f));
            Material mat = CreateSolidUnlitSphereMaterial(tint);
            if (mat == null)
                return;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "LegacySphereAura";
            sphere.transform.SetParent(auraRoot, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 1.05f;
            Object.Destroy(sphere.GetComponent<Collider>());
            var mr = sphere.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            DisableSmallMeshCulling(mr);
        }

        static Material CreateSolidUnlitSphereMaterial(Color baseColor)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
                return null;
            var mat = new Material(sh) { name = "NegativeThought_LegacySphere" };
            if (!mat.HasProperty("_BaseMap"))
            {
                Object.Destroy(mat);
                return null;
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            mat.SetColor("_BaseColor", baseColor);
            return mat;
        }

        static void DisableSmallMeshCulling(Renderer r)
        {
            if (r == null)
                return;
            var p = typeof(Renderer).GetProperty("allowSmallMeshCulling", BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite)
                p.SetValue(r, false);
        }

        static bool IsAlive(Object o) => o != null && o;

        static void SetLayerRecursively(Transform t, int layer)
        {
            if (!IsAlive(t))
                return;
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (IsAlive(child))
                    SetLayerRecursively(child, layer);
            }
        }

        static void DestroyNamedChild(Transform parent, string childName)
        {
            if (!IsAlive(parent))
                return;
            var existing = parent.Find(childName);
            if (!IsAlive(existing))
                return;
            Object.DestroyImmediate(existing.gameObject);
        }

        void EnsureLabel()
        {
            if (!IsAlive(this) || !IsAlive(transform))
                return;

            DestroyNamedChild(transform, LabelChildName);

            var rootGo = new GameObject(LabelChildName);
            m_LabelRoot = rootGo.transform;
            m_LabelRoot.SetParent(transform, false);
            m_LabelRoot.localScale = Vector3.one * Mathf.Max(0.004f, m_LabelRootWorldScale);
            UpdateLabelAnchoredPosition();

            m_LabelTmp = rootGo.AddComponent<TextMeshPro>();
            m_LabelTmp.raycastTarget = false;
            m_LabelTmp.richText = false;
            m_LabelTmp.alignment = TextAlignmentOptions.Center;
            m_LabelTmp.enableWordWrapping = true;
            m_LabelTmp.overflowMode = TextOverflowModes.Overflow;
            m_LabelTmp.outlineWidth = 0.15f;
            m_LabelTmp.outlineColor = new Color32(0, 0, 0, 220);

            float wrap = Mathf.Max(5f, m_WorldLabelWidthMeters * 4f);
            m_LabelTmp.rectTransform.sizeDelta = new Vector2(wrap, 2.4f);

            var f = ResolveTmpFont();
            if (f != null)
            {
                m_LabelTmp.font = f;
                if (f.material != null)
                    m_LabelTmp.fontSharedMaterial = f.material;
            }

            var mr = m_LabelTmp.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 9000;
                DisableSmallMeshCulling(mr);
            }

            int vizLayer = m_AuraLayer >= 0 ? m_AuraLayer : gameObject.layer;
            if (IsAlive(m_LabelRoot))
                SetLayerRecursively(m_LabelRoot, vizLayer);

            RefreshLabelTmp();
        }

        void UpdateLabelAnchoredPosition()
        {
            if (!IsAlive(m_LabelRoot))
            {
                m_LabelRoot = null;
                return;
            }
            Vector3 worldPos;
            if (m_AnchorLabelToHeadBone && m_Animator != null && m_Animator.isHuman)
            {
                var head = m_Animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                    worldPos = head.position + Vector3.up * m_LabelOffsetY;
                else
                    worldPos = transform.position + Vector3.up * m_LabelFallbackWorldHeight;
            }
            else
                worldPos = transform.position + Vector3.up * m_LabelFallbackWorldHeight;

            m_LabelRoot.position = worldPos;
        }

        void LateUpdate()
        {
            if (m_Defeated)
                return;

            VrAudioListenerUtility.EnsureSingleListenerOnRigCamera();
            UpdateLabelAnchoredPosition();

            var cam = ResolveRigCamera();
            if (cam == null)
                return;

            if (m_LabelRoot != null)
            {
                var toCam = cam.transform.position - m_LabelRoot.position;
                if (toCam.sqrMagnitude > 1e-6f)
                    m_LabelRoot.rotation = Quaternion.LookRotation(toCam, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
            }

            UpdateWhisperDynamics(cam);
        }

        void EnsureAudio()
        {
            foreach (var a in GetComponents<AudioSource>())
                Destroy(a);
            var oldLp = GetComponent<AudioLowPassFilter>();
            if (oldLp != null)
                Destroy(oldLp);
            m_LowPass = null;

            var w = gameObject.AddComponent<AudioSource>();
            w.playOnAwake = false;
            w.loop = true;
            w.spatialBlend = 0f;
            w.rolloffMode = AudioRolloffMode.Custom;
            w.minDistance = m_WhisperMinDistance;
            w.maxDistance = m_WhisperMaxDistance;
            w.dopplerLevel = 0f;
            w.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                AnimationCurve.Constant(0f, 1f, 1f));
            w.clip = m_WhisperLoopClip;
            w.volume = 0f;
            m_Whisper = w;

            m_Relief = gameObject.AddComponent<AudioSource>();
            m_Relief.playOnAwake = false;
            m_Relief.loop = false;
            m_Relief.spatialBlend = 1f;
            m_Relief.minDistance = 1f;
            m_Relief.maxDistance = 25f;

            if (m_UseLowPassByDistance)
                m_LowPass = gameObject.AddComponent<AudioLowPassFilter>();
        }

        void OnEnable()
        {
            if (m_Whisper != null && m_WhisperLoopClip != null && !m_Defeated)
            {
                m_Whisper.clip = m_WhisperLoopClip;
                m_Whisper.Play();
            }
        }

        void UpdateWhisperDynamics(Camera listenerCam)
        {
            if (m_Whisper == null || m_WhisperLoopClip == null)
                return;
            if (!m_Whisper.isPlaying)
                m_Whisper.Play();

            Transform ear = ResolveListenerEar(listenerCam);
            if (ear == null)
                return;

            float d = Vector3.Distance(ear.position, transform.position);
            float t = Mathf.InverseLerp(m_WhisperMinDistance, m_WhisperMaxDistance, d);
            t = Mathf.Clamp01(t);
            float volMul = m_VolumeByNormalizedDistance != null && m_VolumeByNormalizedDistance.length > 0
                ? m_VolumeByNormalizedDistance.Evaluate(t)
                : t;
            float vol = m_WhisperBaseVolume * Mathf.Clamp01(volMul);
            if (d <= m_WhisperMaxDistance)
                vol = Mathf.Max(vol, m_WhisperBaseVolume * 0.08f);
            m_Whisper.volume = Mathf.Clamp01(vol);
            m_Whisper.spatialBlend = 0f;

            if (m_LowPass != null && m_UseLowPassByDistance)
            {
                float lpT = m_LowpassCurve != null && m_LowpassCurve.length > 0
                    ? m_LowpassCurve.Evaluate(t)
                    : t;
                float hz = Mathf.Lerp(m_LowpassFarHz, m_LowpassNearHz, lpT);
                m_LowPass.cutoffFrequency = Mathf.Clamp(hz, 10f, 22000f);
            }
        }

        static Transform ResolveListenerEar(Camera rigCam)
        {
            if (rigCam != null)
            {
                var onCam = rigCam.GetComponent<AudioListener>();
                if (onCam != null && onCam.enabled)
                    return rigCam.transform;
            }

            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                var al = listeners[i];
                if (al != null && al.enabled)
                    return al.transform;
            }

            return rigCam != null ? rigCam.transform : null;
        }

        Camera ResolveRigCamera()
        {
            var origin = GetComponentInParent<XROrigin>();
            if (origin == null)
                origin = FindFirstObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null && origin.Camera.isActiveAndEnabled)
                return origin.Camera;
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;
            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c != null && c.isActiveAndEnabled)
                    return c;
            }
            return null;
        }

        IEnumerator CoDefeatAudioAndHide()
        {
            if (m_Whisper != null && m_Whisper.isPlaying)
            {
                float t0 = Time.unscaledTime;
                float v0 = m_Whisper.volume;
                while (Time.unscaledTime - t0 < m_WhisperFadeOutSeconds)
                {
                    float u = (Time.unscaledTime - t0) / m_WhisperFadeOutSeconds;
                    m_Whisper.volume = Mathf.Lerp(v0, 0f, u);
                    yield return null;
                }
                m_Whisper.Stop();
                m_Whisper.volume = 0f;
            }

            if (m_AudioCut != null && m_Relief != null)
                m_Relief.PlayOneShot(m_AudioCut, m_ReliefVolume * 0.65f);

            yield return new WaitForSecondsRealtime(m_ReliefStaggerSeconds);

            if (m_AudioBreath != null && m_Relief != null)
                m_Relief.PlayOneShot(m_AudioBreath, m_ReliefVolume);

            if (m_AudioBell != null && m_Relief != null)
                m_Relief.PlayOneShot(m_AudioBell, m_ReliefVolume * 0.42f);

            if (m_LabelRoot != null)
                m_LabelRoot.gameObject.SetActive(false);
            if (m_AuraTransform != null)
                m_AuraTransform.gameObject.SetActive(false);

            m_FadeRoutine = null;
        }

        void OnDisable()
        {
            if (m_Whisper != null && m_Whisper.isPlaying)
                m_Whisper.Stop();
        }
    }
}
