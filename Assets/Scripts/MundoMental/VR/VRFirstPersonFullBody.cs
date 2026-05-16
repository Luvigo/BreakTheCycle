using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MundoMental.VR
{
    /// <summary>
    /// Cuerpo completo en primera persona bajo el XR Origin: sigue el yaw de la cámara, pies en el suelo del rig,
    /// opcionalmente alinea el hueso <see cref="HumanBodyBones.Head"/> al HMD para que al mirar abajo no se atraviese el torso,
    /// oculta mallas de cara/dientes y alimenta un Animator con la velocidad del CharacterController.
    /// </summary>
    [DefaultExecutionOrder(3000)]
    [AddComponentMenu("Mundo Mental VR/VR First Person Full Body")]
    public sealed class VRFirstPersonFullBody : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Cámara principal / ojo del HMD (yaw del torso + opcionalmente rotación del hueso Head al pitch del HMD).")]
        Transform m_HeadCamera;

        [SerializeField]
        [Tooltip("Si está vacío, se usa este transform como padre del avatar (normalmente el XR Origin).")]
        Transform m_BodyParent;

        [SerializeField]
        [Tooltip("Modelo humanoide (ej. DefaultAvatar de Nurface). Arrastra el GameObject raíz bajo el .fbx en el Project (no un sub-mesh). Si hay hijo ya colocado, puede quedar vacío.")]
        Object m_AvatarPrefab;

        [SerializeField]
        [Tooltip("Instanciar el prefab al iniciar si no hay hijo con Animator.")]
        bool m_InstantiateOnStart = true;

        [SerializeField]
        [Tooltip("Si hay prefab asignado, instanciarlo siempre primero (evita que otro Animator humanoide bajo el XR Origin impida crear el DefaultAvatar).")]
        bool m_PrioritizeAvatarPrefab = true;

        [SerializeField]
        [Tooltip("Si el cuerpo mide muy poco en mundo tras 2 frames (import a escala rara), escala hasta esta altura aproximada (m).")]
        bool m_AutoCorrectTinyBodyScale = true;

        [SerializeField]
        [Tooltip("Altura objetivo del personaje en metros cuando se aplica la corrección automática.")]
        float m_TargetBodyWorldHeight = 1.65f;

        [SerializeField]
        [Tooltip("Posición local del cuerpo respecto al XR Origin (ajusta para alinear pies con el suelo).")]
        Vector3 m_LocalBodyOffset;

        [SerializeField]
        [Tooltip("Escala uniforme del cuerpo.")]
        float m_BodyUniformScale = 1f;

        [SerializeField]
        [Tooltip("Giro extra en Y (grados) si el mesh no mira al frente del rig.")]
        float m_YawOffsetDegrees;

        [SerializeField]
        [Range(0.02f, 0.5f)]
        [Tooltip("Suavizado del yaw del cuerpo (0.1 ≈ rápido).")]
        float m_YawSmoothTime = 0.12f;

        [SerializeField]
        [Tooltip("Desactiva renderers cuyo nombre sugiera cara, ojos, dientes, etc. (no quita la piel del cuerpo único si viene en una sola malla).")]
        bool m_HideFaceMeshes = true;

        [SerializeField]
        [Tooltip("Si hay CharacterController en este mismo GameObject, envía magnitud de velocidad al Animator como 'Speed' (compatible con VR Body de Nurface).")]
        bool m_DriveAnimatorSpeedFromCharacterController = true;

        [SerializeField]
        [Tooltip("Opcional: asigna el RuntimeAnimatorController del VR Body (si no, se usa el que ya tenga el modelo).")]
        RuntimeAnimatorController m_OverrideAnimatorController;

        [Header("Brazos hibridos (IK)")]
        [SerializeField]
        [Tooltip("Activa el modo hibrido C: los brazos del avatar siguen a las manos VR mediante Two-Bone IK; la mano del avatar se colapsa para dejar visible solo la mano VR.")]
        bool m_HybridArmsMode = true;

        [SerializeField]
        [Tooltip("Busca automaticamente bajo BodyParent (XR Origin) los SimpleHandsXRInputDriver y los usa como targets izquierdo/derecho. Solo aplica si los campos manuales estan vacios.")]
        bool m_AutoFindHandTargets = true;

        [SerializeField]
        [Tooltip("Override manual: transform de la muneca VR izquierda (donde debe llegar el brazo izquierdo del avatar).")]
        Transform m_LeftHandTarget;

        [SerializeField]
        [Tooltip("Override manual: transform de la muneca VR derecha.")]
        Transform m_RightHandTarget;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Mezcla IK vs animacion idle del avatar (1 = IK pleno, 0 = ignorar IK).")]
        float m_ArmIkWeight = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Cuanto se alinea la rotacion del hueso de la muneca del avatar con la rotacion de la mano VR.")]
        float m_WristMatchWeight = 1f;

        [SerializeField]
        [Tooltip("Offset desde el hombro izquierdo (en espacio local del cuerpo) hacia donde apunta el codo. Evita codos invertidos.")]
        Vector3 m_LeftElbowPoleLocal = new Vector3(-0.25f, -0.4f, -0.15f);

        [SerializeField]
        [Tooltip("Offset desde el hombro derecho (en espacio local del cuerpo) hacia donde apunta el codo. Evita codos invertidos.")]
        Vector3 m_RightElbowPoleLocal = new Vector3(0.25f, -0.4f, -0.15f);

        [SerializeField]
        [Range(1f, 1.06f)]
        [Tooltip("Tope de alcance del IK como fraccion de (upperArm+lowerArm). 1 = sin estirar: al maximo el brazo se queda en su longitud real (evita muneca puntiaguda). 1.03-1.06 = un poco extra solo si lo quieres.")]
        float m_ArmReachStretchClamp = 1f;

        [SerializeField]
        [Range(0f, 0.35f)]
        [Tooltip("Snap posicional tras IK solo si el gap es pequeño. Con 'glue' seguro no hace falta subir mucho.")]
        float m_WristSnapMaxGap = 0.1f;

        [SerializeField]
        [Tooltip("Tras el IK, ajusta fino la posicion del hueso Hand hacia el tracker solo si el antebrazo puede alcanzarlo sin estirarse como aguja (se limita por longitud del lowerArm). Desactivalo si prefieres cero truco.")]
        bool m_ApplyWristPositionGlue = true;

        [SerializeField]
        [Range(0.02f, 0.12f)]
        [Tooltip("Como mucho mueve la muneca del avatar hacia el tracker (m) cuando el glue pasa el chequeo de antebrazo. Valores grandes provocaban punta al estirar.")]
        float m_WristPositionGlueMaxMeters = 0.05f;

        [SerializeField]
        [Tooltip("Sin mover el root del XR (agarres/trigger siguen en el tracking real): cada frame resetea la base de HandRig y lo desplaza solo lo necesario para que la muneca IK coincida con el limite del brazo. Requiere hijo 'HandRig' bajo el prefab (SimpleHands).")]
        bool m_ClampHandVisualToArmReach = true;

        [SerializeField]
        [Tooltip("Hijos del root de la mano (no el root) que deben recibir el mismo pull que HandRig, ej. vacios de agarre. Si vacío: se incluyen solos hijos directos del root que no sean HandRig ni malla con SkinnedMeshRenderer (p. ej. StylizedHand).")]
        Transform[] m_LeftHandReachPullExtras;

        [SerializeField]
        [Tooltip("Igual que izquierda, mano derecha.")]
        Transform[] m_RightHandReachPullExtras;

        [SerializeField]
        [Tooltip("Transforms fuera del prefab de mano pero que deben recibir el mismo pull (ej. Near-Far Interactor hermano del WhiteHand). Si vacío y 'Auto Pull Interactor Siblings' está activo, se detectan solos.")]
        Transform[] m_LeftHandReachPullExternals;

        [SerializeField]
        Transform[] m_RightHandReachPullExternals;

        [SerializeField]
        [Tooltip("Sube desde el root de la mano y en cada padre registra hermanos con XRBaseInteractor (NearFar, Direct, etc.). Necesario cuando la mano no es hija directa del Controller: el interactor cuelga del mismo padre que el wrapper de la mano.")]
        bool m_AutoPullReachInteractorSiblings = true;

        [SerializeField]
        [Range(1, 8)]
        [Tooltip("Cuántos padres subir al buscar interactores hermanos. 2 = hasta Left/Right Controller (recomendado). Valores mayores llegan al Camera Offset y pueden enlazar el mando de la OTRA mano (ej. Near-Far mal parentado) y romper el rig.")]
        int m_ReachPullInteractorAncestorDepth = 2;

        [SerializeField]
        [Tooltip("Offset del target IK en espacio LOCAL del punto de pegado (ver 'Use Hand Rig Wrist Anchor'). Con el ancla en Wrist del WhiteHand suele bastar (0,0,0). Ajusta solo si el rig de tu mano es distinto.")]
        Vector3 m_HandTargetLocalOffset;

        [SerializeField]
        [Range(0f, 0.20f)]
        [Tooltip("Retroceso EXTRA en mundo hacia el codo del avatar (mano->lowerArm). Con ancla HandRig/Wrist suele quedar en 0.")]
        float m_HandIkPullBackTowardElbow;

        [SerializeField]
        [Tooltip("Si el Hand Target apunta al root del prefab WhiteHand (donde esta SimpleHandsXRInputDriver), el hueso de muneca real esta en HandRig/Wrist. Activado = usar esa transformacion para posicion y rotacion del IK.")]
        bool m_UseHandRigWristAnchor = true;

        [SerializeField]
        [Tooltip("Bias fino (local muneca/pose): empuja el target unos mm hacia la palma para que la manga tape el borde inferior al rotar. Default leve -Y; prueba -Z si tu rig va al reves.")]
        Vector3 m_WristCoverLocalBias = new Vector3(0f, -0.012f, 0f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Propaga roll/pronacion al antebrazo. 1 = maximo seguimiento en giros extremos (evita que se vea el hueco bajo la mano naranja).")]
        float m_ForearmTwistFollow = 1f;

        [SerializeField]
        [Range(-0.05f, 0.05f)]
        [Tooltip("Correccion lateral (metros sobre handPose.right). Con ancla Wrist suele ser 0.")]
        float m_LeftHandIkLateralAdjust;

        [SerializeField]
        [Range(-0.05f, 0.05f)]
        [Tooltip("Correccion lateral derecha (metros sobre handPose.right).")]
        float m_RightHandIkLateralAdjust;

        [SerializeField]
        [Tooltip("Colapsa los huesos LeftHand/RightHand del avatar a escala 0 cada frame para que solo se vea la mano VR.")]
        bool m_CollapseAvatarHandsAtWrist = true;

        [SerializeField]
        [Tooltip("Encoge el hueso Head del avatar a escala 0 para que la malla de la cabeza no tape la camara en primera persona. NO afecta al torso. Combinado con 'Spawn Head Shadow Proxy' la sombra conserva la cabeza.")]
        bool m_HideAvatarHeadByBoneScale = true;

        [SerializeField]
        [Tooltip("Crea automaticamente un GameObject hijo del Head con un mesh esferico configurado como 'Shadows Only', para que la sombra del avatar conserve una cabeza aunque el hueso este encogido.")]
        bool m_SpawnHeadShadowProxy = true;

        [SerializeField]
        [Range(0.05f, 0.20f)]
        [Tooltip("Radio (m) del proxy de sombra de la cabeza. Aproxima el tamano del craneo del avatar.")]
        float m_HeadShadowProxyRadius = 0.12f;

        [SerializeField]
        [Tooltip("(Opcional) Tambien aumenta el nearClipPlane de la camara HMD para descartar geometria muy cercana. Suele NO necesitarse si 'Hide Avatar Head By Bone Scale' esta activo.")]
        bool m_HideAvatarHeadByNearClip;

        [SerializeField]
        [Range(0.05f, 0.40f)]
        [Tooltip("Distancia de clipping cercano para la camara HMD cuando 'Hide Avatar Head By Near Clip' esta activo. CUIDADO: valores altos recortan el torso al mirar hacia abajo.")]
        float m_HeadHideNearClip = 0.15f;

        [Header("Cabeza vs HMD")]
        [SerializeField]
        [Tooltip("Rota el hueso humanoide Head hacia la cámara del casco. Evita que al mirar abajo el punto de vista quede dentro del pecho/cuello.")]
        bool m_SyncHeadBoneToHmd = true;

        [SerializeField]
        [Tooltip("Rotación local aplicada después del HMD (ajusta si el mesh de cabeza va desplazado respecto al ojo).")]
        Vector3 m_HeadBoneOffsetEuler;

        [SerializeField]
        [Range(4f, 80f)]
        [Tooltip("Qué tan rápido el hueso Head alcanza la orientación del HMD (más alto = menos ‘lag’).")]
        float m_HeadBoneAlignSpeed = 24f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Tope de mezcla hacia el HMD (1 = igual que la cámara). Por debajo de 1 deja algo de la animación idle en la cabeza.")]
        float m_HeadBoneAlignMaxWeight = 1f;

        [SerializeField]
        [Tooltip("Mueve el cuerpo verticalmente (Y) para que la cabeza del avatar coincida con la altura del HMD. Al agacharte el cuerpo baja con la camara.")]
        bool m_BodyFollowsHeadVertically = true;

        [SerializeField]
        [Tooltip("Mueve el cuerpo horizontalmente (XZ) para que la cabeza del avatar siempre quede donde esta el HMD. Evita que la camara se separe del cuerpo cuando te mueves fisicamente o el HMD se desplaza al rotar el cuello.")]
        bool m_BodyFollowsHeadHorizontally = true;

        [SerializeField]
        [Tooltip("Si esta activo, el cuerpo sigue al HMD frame a frame sin smoothing (1:1). Recomendado para evitar lag al caminar. Desactivalo solo si quieres suavizado.")]
        bool m_BodyAnchorInstantFollow = true;

        [SerializeField]
        [Range(0f, 60f)]
        [Tooltip("Rapidez con la que el cuerpo alcanza la altura objetivo de la camara (mas alto = menos lag al agacharse). Solo aplica cuando 'Instant Follow' esta apagado.")]
        float m_HeadAnchorSmoothSpeed = 30f;

        [SerializeField]
        [Range(0f, 1.5f)]
        [Tooltip("Limite de cuanto puede mover el body por frame en metros (evita 'brinco' feo al iniciar Play). Solo aplica cuando 'Instant Follow' esta apagado.")]
        float m_HeadAnchorMaxStepPerFrame = 0.5f;

        [SerializeField]
        [Tooltip("Offset fino vertical entre la camara y el hueso Head (positivo = avatar mas abajo respecto al HMD).")]
        float m_HeadAnchorVerticalBias;

        [SerializeField]
        [Tooltip("Donde debe quedar el hueso Head respecto a la camara en espacio del cuerpo: x = derecha del avatar, y = adelante del avatar. Y NEGATIVO (~-0.06 a -0.12) situa el head bone DETRAS del HMD para que la camara quede como los ojos y no 'dentro' del cuello. Y positivo suele dejar la camara detras del craneo.")]
        Vector2 m_HeadAnchorHorizontalBias = new Vector2(0f, -0.09f);

        [Header("Visibilidad VR")]
        [SerializeField]
        [Tooltip("Evita que el Animator deje de actualizar huesos por culling (típico si el cuerpo queda ‘invisible’ en 1ª persona).")]
        bool m_ForceAnimatorAlwaysAnimate = true;

        [SerializeField]
        [Tooltip("Fuerza actualización de mallas con skin aunque el frustum no las toque (mirada hacia abajo en VR).")]
        bool m_SkinMeshesUpdateWhenOffscreen = true;

        [SerializeField]
        [Tooltip("Asigna al avatar la misma Layer que la cámara de cabeza (el XR Rig suele estar en capas que otras cámaras no dibujan).")]
        bool m_MatchBodyLayerToHeadCamera = true;

        [SerializeField]
        [Tooltip("Desactiva la oclusión dinámica en todas las mallas del avatar (en VR 1ª persona el torso/piernas a veces se ‘pierden’).")]
        bool m_DisableDynamicOcclusionOnBody = true;

        [SerializeField]
        [Tooltip("Amplía el AABB local de cada SkinnedMesh (metros). Con animación idle, bounds estrechos pueden hacer que Unity no dibuje piernas/torso al mirar abajo.")]
        float m_SkinnedMeshBoundsExpansion = 0.45f;

        [SerializeField]
        [Tooltip("Desplaza el avatar en el espacio local del XR Origin (Z+ = hacia delante del rig). Ayuda a separar el cuerpo del plano cercano de la cámara.")]
        Vector3 m_LocalBodyVisualBias;

        [SerializeField]
        [Tooltip("Escribe en la consola al crear o reutilizar el cuerpo (útil si no ves DefaultAvatar_FP en jerarquía).")]
        bool m_LogBodySetup;

        [SerializeField]
        [Range(1, 120)]
        [Tooltip("Cuántos frames espera a que el XR Origin esté activo y reintenta instanciar el avatar.")]
        int m_MaxSpawnWaitFrames = 45;

        Transform m_BodyRoot;
        Animator m_Animator;
        CharacterController m_CharacterController;
        float m_SmoothYawVelocity;
        bool m_HasSpeedParameter;
        bool m_BodySetupCompleted;
        bool m_ConfigurationApplied;
        bool m_HaveWarnedMissingAvatarPrefab;

        Transform m_LeftUpperArm;
        Transform m_LeftLowerArm;
        Transform m_LeftHandBone;
        Transform m_RightUpperArm;
        Transform m_RightLowerArm;
        Transform m_RightHandBone;
        float m_LeftUpperArmLength;
        float m_LeftLowerArmLength;
        float m_RightUpperArmLength;
        float m_RightLowerArmLength;
        bool m_ArmIkReady;
        bool m_HaveWarnedMissingHandTargets;
        bool m_HeadAnchorInitialSnapDone;
        bool m_LeftHandRigReachBaseStored;
        Vector3 m_LeftHandRigReachBaseLocalPos;
        Quaternion m_LeftHandRigReachBaseLocalRot;
        bool m_RightHandRigReachBaseStored;
        Vector3 m_RightHandRigReachBaseLocalPos;
        Quaternion m_RightHandRigReachBaseLocalRot;

        struct ReachPullBinding
        {
            public Transform Target;
            public Vector3 BaseLocalPos;
            public Quaternion BaseLocalRot;
        }

        Transform m_LeftReachPullBindingsRoot;
        readonly List<ReachPullBinding> m_LeftReachPullBindings = new List<ReachPullBinding>(4);
        Transform m_RightReachPullBindingsRoot;
        readonly List<ReachPullBinding> m_RightReachPullBindings = new List<ReachPullBinding>(4);

        static readonly string[] kFaceNameTokens =
        {
            "Eye", "Teeth", "Tongue", "Tounge", "Jaw", "Cheek", "Lip", "Brow", "Nostril", "Eyelid",
            "Ri_Eye", "Le_Eye",
        };

        void Awake()
        {
            if (m_BodyParent == null)
                m_BodyParent = transform;
            if (m_HeadCamera == null && Camera.main != null)
                m_HeadCamera = Camera.main.transform;
            m_CharacterController = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            InvalidateReachPullBindingCache();
        }

        void InvalidateReachPullBindingCache()
        {
            m_LeftReachPullBindingsRoot = null;
            m_RightReachPullBindingsRoot = null;
            m_LeftReachPullBindings.Clear();
            m_RightReachPullBindings.Clear();
        }

        void OnDisable()
        {
            RestoreHandRigReachVisualToBase(m_LeftHandTarget, m_LeftHandRigReachBaseStored,
                m_LeftHandRigReachBaseLocalPos, m_LeftHandRigReachBaseLocalRot);
            RestoreHandRigReachVisualToBase(m_RightHandTarget, m_RightHandRigReachBaseStored,
                m_RightHandRigReachBaseLocalPos, m_RightHandRigReachBaseLocalRot);
            RestoreReachPullBindingsList(m_LeftReachPullBindings);
            RestoreReachPullBindingsList(m_RightReachPullBindings);
        }

        void Start()
        {
            if (!Application.isPlaying)
                return;
            StopAllCoroutines();
            StartCoroutine(CoSpawnAndConfigureBody());
        }

        IEnumerator CoSpawnAndConfigureBody()
        {
            for (var frame = 0; frame < m_MaxSpawnWaitFrames; frame++)
            {
                if (m_BodySetupCompleted)
                    yield break;

                if (TryResolveBodyTransform())
                {
                    ApplyConfigurationIfNeeded();
                    if (m_BodySetupCompleted)
                        yield break;
                }

                yield return null;
            }

            if (!m_BodySetupCompleted)
            {
                Debug.LogError(
                    $"VRFirstPersonFullBody en '{name}': tras {m_MaxSpawnWaitFrames} frames sigue sin cuerpo. " +
                    $"Prefab={(m_AvatarPrefab != null ? m_AvatarPrefab.name + " (" + m_AvatarPrefab.GetType().Name + ")" : "NULL")}, " +
                    $"padreActivo={m_BodyParent.gameObject.activeInHierarchy}, " +
                    $"instantiateOnStart={m_InstantiateOnStart}, prioritizePrefab={m_PrioritizeAvatarPrefab}. " +
                    "¿Estás en Play? ¿El FBX DefaultAvatar sigue asignado en el inspector?",
                    this);
            }
        }

        GameObject ResolveAvatarPrefabRoot()
        {
            if (m_AvatarPrefab == null)
                return null;
            if (m_AvatarPrefab is GameObject go)
                return go;
            if (m_AvatarPrefab is Transform tr)
                return tr.gameObject;
            if (m_AvatarPrefab is Component c)
                return c.gameObject;
            return null;
        }

        bool TryResolveBodyTransform()
        {
            if (!m_BodyParent.gameObject.activeInHierarchy)
                return false;

            if (m_BodyRoot != null)
                return true;

            var avatarRoot = ResolveAvatarPrefabRoot();

            if (m_InstantiateOnStart && avatarRoot == null)
            {
                if (!m_HaveWarnedMissingAvatarPrefab)
                {
                    if (m_AvatarPrefab == null)
                    {
                        Debug.LogWarning(
                            $"VRFirstPersonFullBody en '{name}': Avatar Prefab vacío. Asigna el GameObject raíz de DefaultAvatar (despliega el .fbx en el Project).",
                            this);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"VRFirstPersonFullBody en '{name}': Avatar Prefab apunta a '{m_AvatarPrefab.name}' ({m_AvatarPrefab.GetType().Name}), no a un GameObject. " +
                            "En el Project, despliega DefaultAvatar.fbx y arrastra el **GameObject raíz** (icono de cubo) al campo.",
                            this);
                    }

                    m_HaveWarnedMissingAvatarPrefab = true;
                }

                return false;
            }

            if (m_PrioritizeAvatarPrefab && m_InstantiateOnStart && avatarRoot != null)
            {
                var fpName = avatarRoot.name + "_FP";
                var existing = FindNamedChildTransform(m_BodyParent, fpName);
                if (existing != null)
                {
                    m_BodyRoot = existing;
                    if (m_LogBodySetup)
                        Debug.Log($"[VRFirstPersonFullBody] Hijo existente '{fpName}'.", this);
                }
                else
                {
                    GameObject go;
                    try
                    {
                        go = Instantiate(avatarRoot, m_BodyParent, false);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError(
                            $"VRFirstPersonFullBody: Instantiate falló ({e.Message}). " +
                            "En el Project, despliega el .fbx y arrastra el **GameObject raíz** (icono de cubo) al campo Avatar Prefab, no el archivo .fbx entero.",
                            this);
                        m_BodySetupCompleted = true;
                        return true;
                    }

                    go.name = fpName;
                    go.SetActive(true);
                    m_BodyRoot = go.transform;
                    ApplyBodyPlacement(m_BodyRoot);
                    if (m_LogBodySetup)
                        Debug.Log($"[VRFirstPersonFullBody] Instanciado '{fpName}' bajo '{m_BodyParent.name}'.", this);
                }
            }

            if (m_BodyRoot == null)
                m_BodyRoot = FindBodyRoot();

            if (m_BodyRoot == null && !m_PrioritizeAvatarPrefab && m_InstantiateOnStart && avatarRoot != null)
            {
                var go = Instantiate(avatarRoot, m_BodyParent, false);
                go.name = avatarRoot.name + "_FP";
                go.SetActive(true);
                m_BodyRoot = go.transform;
                ApplyBodyPlacement(m_BodyRoot);
                if (m_LogBodySetup)
                    Debug.Log($"[VRFirstPersonFullBody] Instanciado '{go.name}' (sin priorizar prefab).", this);
            }

            if (m_BodyRoot == null)
                return false;

            return true;
        }

        void ApplyConfigurationIfNeeded()
        {
            if (m_BodyRoot == null || m_ConfigurationApplied)
                return;

            m_Animator = m_BodyRoot.GetComponentInChildren<Animator>();
            if (m_OverrideAnimatorController != null && m_Animator != null)
                m_Animator.runtimeAnimatorController = m_OverrideAnimatorController;
            ApplyBodyPlacement(m_BodyRoot);
            if (m_HideFaceMeshes)
                HideFaceRenderers(m_BodyRoot);
            ApplyVrVisibilityFixes(m_BodyRoot);
            CacheSpeedParameter();
            CacheArmIkBones();
            ResolveHandTargets();
            ApplyHeadCameraNearClipIfNeeded();
            SpawnHeadShadowProxyIfNeeded();
            if (m_HeadCamera != null)
            {
                var e = m_BodyRoot.eulerAngles;
                m_BodyRoot.eulerAngles = new Vector3(e.x, m_HeadCamera.eulerAngles.y + m_YawOffsetDegrees, e.z);
            }

            if (m_AutoCorrectTinyBodyScale)
                StartCoroutine(CoEnsureReasonableBodyHeight());

            if (m_Animator == null)
                Debug.LogWarning(
                    $"VRFirstPersonFullBody en '{name}': '{m_BodyRoot.name}' no tiene Animator en jerarquía.",
                    this);

            m_ConfigurationApplied = true;
            m_BodySetupCompleted = true;
        }

        static Transform FindNamedChildTransform(Transform root, string exactName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName)
                    return t;
            }

            return null;
        }

        IEnumerator CoEnsureReasonableBodyHeight()
        {
            yield return null;
            yield return null;
            if (m_BodyRoot == null)
                yield break;
            var smrs = m_BodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs.Length == 0)
                yield break;
            var b = smrs[0].bounds;
            for (var i = 1; i < smrs.Length; i++)
                b.Encapsulate(smrs[i].bounds);
            var h = b.size.y;
            if (h >= 0.35f || h <= 0.0001f)
                yield break;
            var factor = m_TargetBodyWorldHeight / h;
            m_BodyRoot.localScale *= factor;
            RecomputeArmLengths();
        }

        void CacheSpeedParameter()
        {
            m_HasSpeedParameter = false;
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null)
                return;
            foreach (var p in m_Animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float && p.name == "Speed")
                {
                    m_HasSpeedParameter = true;
                    break;
                }
            }
        }

        Transform FindBodyRoot()
        {
            var anims = m_BodyParent.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < anims.Length; i++)
            {
                var a = anims[i];
                if (a.transform == m_BodyParent || a.avatar == null || !a.avatar.isHuman)
                    continue;
                if (a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                    continue;
                return a.transform;
            }

            return null;
        }

        void ApplyBodyPlacement(Transform body)
        {
            body.localPosition = m_LocalBodyOffset + m_LocalBodyVisualBias;
            body.localScale = Vector3.one * Mathf.Max(0.01f, m_BodyUniformScale);
        }

        void ApplyVrVisibilityFixes(Transform bodyRoot)
        {
            if (m_ForceAnimatorAlwaysAnimate && m_Animator != null)
                m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (m_DisableDynamicOcclusionOnBody)
            {
                var renderers = bodyRoot.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                    renderers[i].allowOcclusionWhenDynamic = false;
            }

            if (m_SkinMeshesUpdateWhenOffscreen || m_SkinnedMeshBoundsExpansion > 0f)
            {
                var skinned = bodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (var i = 0; i < skinned.Length; i++)
                {
                    if (m_SkinMeshesUpdateWhenOffscreen)
                        skinned[i].updateWhenOffscreen = true;
                    if (m_SkinnedMeshBoundsExpansion > 0f)
                    {
                        var b = skinned[i].localBounds;
                        b.Expand(m_SkinnedMeshBoundsExpansion);
                        skinned[i].localBounds = b;
                    }
                }
            }

            if (m_MatchBodyLayerToHeadCamera && m_HeadCamera != null)
                SetLayerRecursively(bodyRoot, m_HeadCamera.gameObject.layer);
        }

        static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer);
        }

        static void HideFaceRenderers(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var n = renderers[i].gameObject.name;
                if (n.Contains("Unity_Body_Mesh"))
                    continue;
                for (var t = 0; t < kFaceNameTokens.Length; t++)
                {
                    if (!n.Contains(kFaceNameTokens[t]))
                        continue;
                    renderers[i].enabled = false;
                    break;
                }
            }
        }

        void CacheArmIkBones()
        {
            m_ArmIkReady = false;
            m_LeftUpperArm = m_LeftLowerArm = m_LeftHandBone = null;
            m_RightUpperArm = m_RightLowerArm = m_RightHandBone = null;

            if (m_Animator == null || !m_Animator.isHuman)
                return;

            m_LeftUpperArm = m_Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            m_LeftLowerArm = m_Animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            m_LeftHandBone = m_Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            m_RightUpperArm = m_Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            m_RightLowerArm = m_Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            m_RightHandBone = m_Animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (m_LeftUpperArm == null || m_LeftLowerArm == null || m_LeftHandBone == null ||
                m_RightUpperArm == null || m_RightLowerArm == null || m_RightHandBone == null)
            {
                Debug.LogWarning(
                    $"VRFirstPersonFullBody en '{name}': el Animator no expuso todos los huesos de brazo (puede deberse a 'Optimize Game Objects' o a un rig no humanoide). IK de brazos desactivado.",
                    this);
                return;
            }

            RecomputeArmLengths();
            m_ArmIkReady = true;
        }

        void RecomputeArmLengths()
        {
            if (m_LeftUpperArm != null && m_LeftLowerArm != null && m_LeftHandBone != null)
            {
                m_LeftUpperArmLength = Vector3.Distance(m_LeftUpperArm.position, m_LeftLowerArm.position);
                m_LeftLowerArmLength = Vector3.Distance(m_LeftLowerArm.position, m_LeftHandBone.position);
            }

            if (m_RightUpperArm != null && m_RightLowerArm != null && m_RightHandBone != null)
            {
                m_RightUpperArmLength = Vector3.Distance(m_RightUpperArm.position, m_RightLowerArm.position);
                m_RightLowerArmLength = Vector3.Distance(m_RightLowerArm.position, m_RightHandBone.position);
            }
        }

        void ResolveHandTargets()
        {
            if (!m_AutoFindHandTargets || m_BodyParent == null)
                return;

            var drivers = m_BodyParent.GetComponentsInChildren<SimpleHandsXRInputDriver>(true);
            for (var i = 0; i < drivers.Length; i++)
            {
                var d = drivers[i];
                if (d == null)
                    continue;
                if (d.IsRightHand)
                {
                    if (m_RightHandTarget == null)
                        m_RightHandTarget = d.transform;
                }
                else
                {
                    if (m_LeftHandTarget == null)
                        m_LeftHandTarget = d.transform;
                }
            }

            if (m_HybridArmsMode && !m_HaveWarnedMissingHandTargets &&
                (m_LeftHandTarget == null || m_RightHandTarget == null))
            {
                m_HaveWarnedMissingHandTargets = true;
                Debug.LogWarning(
                    $"VRFirstPersonFullBody en '{name}': no encontre SimpleHandsXRInputDriver bajo '{m_BodyParent.name}' para alguna mano " +
                    $"(left={(m_LeftHandTarget == null ? "NULL" : m_LeftHandTarget.name)}, right={(m_RightHandTarget == null ? "NULL" : m_RightHandTarget.name)}). " +
                    "Arrastra los GameObjects de las manos VR a los campos Left/Right Hand Target.",
                    this);
            }
        }

        void SolveArmIkIfNeeded()
        {
            if (!m_HybridArmsMode || !m_ArmIkReady || m_BodyRoot == null)
                return;

            var bodyUp = m_BodyRoot.up;
            var bodyForward = m_BodyRoot.forward;

            if (m_LeftHandTarget != null)
            {
                PrepareHandVisualReachClamp(m_LeftHandTarget, false, m_LeftUpperArm.position,
                    m_LeftLowerArm, m_LeftHandIkLateralAdjust,
                    m_LeftUpperArmLength + m_LeftLowerArmLength);
                var leftPose = GetHandIkPoseTransform(m_LeftHandTarget);
                var poleWorld = m_LeftUpperArm.position + m_BodyRoot.TransformDirection(m_LeftElbowPoleLocal);
                var leftTargetPos = ComputeIkTargetPosition(leftPose, m_LeftLowerArm, m_LeftHandIkLateralAdjust);
                leftTargetPos = ClampHandTargetToReach(m_LeftUpperArm.position, leftTargetPos,
                    m_LeftUpperArmLength + m_LeftLowerArmLength);
                SolveTwoBoneIK(m_LeftUpperArm, m_LeftLowerArm, m_LeftHandBone,
                    m_LeftUpperArmLength, m_LeftLowerArmLength,
                    leftTargetPos, leftPose.rotation,
                    poleWorld, m_ArmIkWeight, 0f, 1f,
                    bodyUp, bodyForward, m_WristSnapMaxGap);
                ApplyForearmTwist(m_LeftLowerArm, m_LeftHandBone, leftPose.rotation, m_ForearmTwistFollow);
                if (m_WristMatchWeight > 0f && m_LeftHandBone != null)
                    m_LeftHandBone.rotation = Quaternion.Slerp(m_LeftHandBone.rotation, leftPose.rotation, m_WristMatchWeight);
                ApplyWristPositionGlue(m_LeftHandBone, m_LeftLowerArm, m_LeftLowerArmLength, leftTargetPos, leftPose.rotation);
            }

            if (m_RightHandTarget != null)
            {
                PrepareHandVisualReachClamp(m_RightHandTarget, true, m_RightUpperArm.position,
                    m_RightLowerArm, m_RightHandIkLateralAdjust,
                    m_RightUpperArmLength + m_RightLowerArmLength);
                var rightPose = GetHandIkPoseTransform(m_RightHandTarget);
                var poleWorld = m_RightUpperArm.position + m_BodyRoot.TransformDirection(m_RightElbowPoleLocal);
                var rightTargetPos = ComputeIkTargetPosition(rightPose, m_RightLowerArm, m_RightHandIkLateralAdjust);
                rightTargetPos = ClampHandTargetToReach(m_RightUpperArm.position, rightTargetPos,
                    m_RightUpperArmLength + m_RightLowerArmLength);
                SolveTwoBoneIK(m_RightUpperArm, m_RightLowerArm, m_RightHandBone,
                    m_RightUpperArmLength, m_RightLowerArmLength,
                    rightTargetPos, rightPose.rotation,
                    poleWorld, m_ArmIkWeight, 0f, 1f,
                    bodyUp, bodyForward, m_WristSnapMaxGap);
                ApplyForearmTwist(m_RightLowerArm, m_RightHandBone, rightPose.rotation, m_ForearmTwistFollow);
                if (m_WristMatchWeight > 0f && m_RightHandBone != null)
                    m_RightHandBone.rotation = Quaternion.Slerp(m_RightHandBone.rotation, rightPose.rotation, m_WristMatchWeight);
                ApplyWristPositionGlue(m_RightHandBone, m_RightLowerArm, m_RightLowerArmLength, rightTargetPos, rightPose.rotation);
            }
        }

        Transform GetHandVisualShiftTransform(Transform handRoot)
        {
            if (handRoot == null)
                return null;
            var t = handRoot.Find("HandRig");
            return t;
        }

        void RestoreHandRigReachVisualToBase(Transform handRoot, bool stored, Vector3 baseLocalPos,
            Quaternion baseLocalRot)
        {
            if (!stored || handRoot == null)
                return;
            var shift = GetHandVisualShiftTransform(handRoot);
            if (shift == null)
                return;
            shift.localPosition = baseLocalPos;
            shift.localRotation = baseLocalRot;
        }

        void RestoreReachPullBindingsList(List<ReachPullBinding> list)
        {
            if (list == null)
                return;
            for (var i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.Target == null)
                    continue;
                b.Target.localPosition = b.BaseLocalPos;
                b.Target.localRotation = b.BaseLocalRot;
            }
        }

        static bool ShouldSkipAutoReachPullChild(Transform child, Transform handRig)
        {
            if (child == null)
                return true;
            if (handRig != null && child == handRig)
                return true;
            var n = child.name ?? "";
            if (n.IndexOf("stylized", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (child.GetComponent<SkinnedMeshRenderer>() != null)
                return true;
            return false;
        }

        void EnsureReachPullBindings(Transform handRoot, bool isRightHand)
        {
            if (isRightHand)
                EnsureReachPullBindingsForSide(ref m_RightReachPullBindingsRoot, m_RightReachPullBindings,
                    m_RightHandReachPullExtras, m_RightHandReachPullExternals, handRoot, true);
            else
                EnsureReachPullBindingsForSide(ref m_LeftReachPullBindingsRoot, m_LeftReachPullBindings,
                    m_LeftHandReachPullExtras, m_LeftHandReachPullExternals, handRoot, false);
        }

        void EnsureReachPullBindingsForSide(ref Transform cachedRoot, List<ReachPullBinding> list,
            Transform[] manualUnderHand, Transform[] manualExternal, Transform handRoot, bool isRightHand)
        {
            if (handRoot == null)
            {
                list.Clear();
                cachedRoot = null;
                return;
            }

            if (handRoot == cachedRoot && list.Count > 0)
                return;

            cachedRoot = handRoot;
            list.Clear();

            var rig = GetHandVisualShiftTransform(handRoot);

            void AddBinding(Transform t)
            {
                if (t == null || t == handRoot)
                    return;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Target == t)
                        return;
                }

                list.Add(new ReachPullBinding
                {
                    Target = t,
                    BaseLocalPos = t.localPosition,
                    BaseLocalRot = t.localRotation,
                });
            }

            void AddUnderHandIfOk(Transform t)
            {
                if (t == null || t == handRoot)
                    return;
                if (!t.IsChildOf(handRoot))
                    return;
                if (rig != null && t == rig)
                    return;
                AddBinding(t);
            }

            if (manualExternal != null && manualExternal.Length > 0)
            {
                for (var i = 0; i < manualExternal.Length; i++)
                    AddBinding(manualExternal[i]);
            }

            if (manualUnderHand != null && manualUnderHand.Length > 0)
            {
                for (var i = 0; i < manualUnderHand.Length; i++)
                    AddUnderHandIfOk(manualUnderHand[i]);
            }
            else
            {
                for (var c = 0; c < handRoot.childCount; c++)
                {
                    var ch = handRoot.GetChild(c);
                    if (ShouldSkipAutoReachPullChild(ch, rig))
                        continue;
                    AddUnderHandIfOk(ch);
                }
            }

            if (m_AutoPullReachInteractorSiblings && handRoot != null)
            {
                var walk = handRoot;
                for (var depth = 0; depth < m_ReachPullInteractorAncestorDepth && walk != null; depth++)
                {
                    var p = walk.parent;
                    if (p == null)
                        break;
                    for (var i = 0; i < p.childCount; i++)
                    {
                        var ch = p.GetChild(i);
                        if (ch == walk)
                            continue;
                        if (!ch.gameObject.activeInHierarchy)
                            continue;
                        if (handRoot.IsChildOf(ch))
                            continue;
                        if (isRightHand && m_LeftHandTarget != null && m_LeftHandTarget.IsChildOf(ch))
                            continue;
                        if (!isRightHand && m_RightHandTarget != null && m_RightHandTarget.IsChildOf(ch))
                            continue;
                        if (ch.GetComponentInChildren<XRBaseInteractor>(true) == null)
                            continue;
                        AddBinding(ch);
                    }

                    walk = p;
                }
            }
        }

        void ResetReachPullBindingsToBase(List<ReachPullBinding> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.Target == null)
                    continue;
                b.Target.localPosition = b.BaseLocalPos;
                b.Target.localRotation = b.BaseLocalRot;
            }
        }

        void PrepareHandVisualReachClamp(Transform handRoot, bool isRightHand, Vector3 shoulderWorld,
            Transform lowerArm, float lateralAlongHandRight, float armChainLength)
        {
            if (handRoot == null || !m_HybridArmsMode)
                return;

            EnsureReachPullBindings(handRoot, isRightHand);
            var extras = isRightHand ? m_RightReachPullBindings : m_LeftReachPullBindings;

            var shift = GetHandVisualShiftTransform(handRoot);

            if (shift != null)
            {
                if (isRightHand)
                {
                    if (!m_RightHandRigReachBaseStored)
                    {
                        m_RightHandRigReachBaseLocalPos = shift.localPosition;
                        m_RightHandRigReachBaseLocalRot = shift.localRotation;
                        m_RightHandRigReachBaseStored = true;
                    }

                    shift.localPosition = m_RightHandRigReachBaseLocalPos;
                    shift.localRotation = m_RightHandRigReachBaseLocalRot;
                }
                else
                {
                    if (!m_LeftHandRigReachBaseStored)
                    {
                        m_LeftHandRigReachBaseLocalPos = shift.localPosition;
                        m_LeftHandRigReachBaseLocalRot = shift.localRotation;
                        m_LeftHandRigReachBaseStored = true;
                    }

                    shift.localPosition = m_LeftHandRigReachBaseLocalPos;
                    shift.localRotation = m_LeftHandRigReachBaseLocalRot;
                }
            }

            ResetReachPullBindingsToBase(extras);

            if (!m_ClampHandVisualToArmReach)
                return;

            var pose = GetHandIkPoseTransform(handRoot);
            if (pose == null)
                return;

            var rawT = ComputeIkTargetPosition(pose, lowerArm, lateralAlongHandRight);
            var clampedT = ClampHandTargetToReach(shoulderWorld, rawT, armChainLength);
            var d = clampedT - rawT;
            if (d.sqrMagnitude <= 1e-10f)
                return;

            if (shift != null)
                shift.position += d;

            for (var i = 0; i < extras.Count; i++)
            {
                var t = extras[i].Target;
                if (t != null)
                    t.position += d;
            }
        }

        Vector3 ClampHandTargetToReach(Vector3 shoulderPos, Vector3 desiredWorld, float chainLength)
        {
            var v = desiredWorld - shoulderPos;
            var m = v.magnitude;
            var maxR = chainLength * Mathf.Clamp(m_ArmReachStretchClamp, 1f, 1.06f);
            if (m > maxR && m > 1e-5f)
                return shoulderPos + v * (maxR / m);
            return desiredWorld;
        }

        void ApplyWristPositionGlue(Transform handBone, Transform lowerBone, float lowerNominalLength,
            Vector3 targetWorldPos, Quaternion targetWorldRot)
        {
            if (!m_ApplyWristPositionGlue || handBone == null || lowerBone == null)
                return;
            var gap = Vector3.Distance(handBone.position, targetWorldPos);
            if (gap > m_WristPositionGlueMaxMeters)
                return;
            if (lowerNominalLength > 1e-5f &&
                Vector3.Distance(lowerBone.position, targetWorldPos) > lowerNominalLength * 1.06f)
                return;
            if (gap > 1e-5f)
                handBone.position = targetWorldPos;
            if (m_WristMatchWeight >= 0.999f)
                handBone.rotation = targetWorldRot;
        }

        Transform GetHandIkPoseTransform(Transform assignedHandRoot)
        {
            if (assignedHandRoot == null)
                return null;
            if (!m_UseHandRigWristAnchor)
                return assignedHandRoot;

            var anchor = assignedHandRoot.Find("HandRig/Wrist");
            if (anchor != null)
                return anchor;
            anchor = assignedHandRoot.Find("Wrist");
            return anchor != null ? anchor : assignedHandRoot;
        }

        Vector3 ComputeIkTargetPosition(Transform handTarget, Transform lowerArm, float lateralAlongHandRight)
        {
            if (handTarget == null)
                return Vector3.zero;

            var basePos = handTarget.position;
            if (Mathf.Abs(lateralAlongHandRight) > 1e-6f)
                basePos += handTarget.right * lateralAlongHandRight;

            if (m_HandTargetLocalOffset.sqrMagnitude > 0f)
                basePos += handTarget.TransformVector(m_HandTargetLocalOffset);

            if (m_WristCoverLocalBias.sqrMagnitude > 0f)
                basePos += handTarget.TransformVector(m_WristCoverLocalBias);

            if (m_HandIkPullBackTowardElbow > 0f && lowerArm != null)
            {
                var toElbow = lowerArm.position - handTarget.position;
                if (toElbow.sqrMagnitude > 1e-6f)
                    basePos += toElbow.normalized * m_HandIkPullBackTowardElbow;
            }

            return basePos;
        }

        static void ApplyForearmTwist(Transform lowerArm, Transform handBone, Quaternion targetHandRot, float twistFollow)
        {
            if (lowerArm == null || handBone == null || twistFollow <= 0f)
                return;

            var axis = (handBone.position - lowerArm.position);
            if (axis.sqrMagnitude < 1e-6f)
                return;
            axis.Normalize();

            var currentHandFwd = handBone.rotation * Vector3.forward;
            var targetHandFwd = targetHandRot * Vector3.forward;
            var curProj = Vector3.ProjectOnPlane(currentHandFwd, axis);
            var tgtProj = Vector3.ProjectOnPlane(targetHandFwd, axis);
            if (curProj.sqrMagnitude < 1e-6f || tgtProj.sqrMagnitude < 1e-6f)
                return;

            var twist = Quaternion.FromToRotation(curProj.normalized, tgtProj.normalized);
            twist.ToAngleAxis(out var angle, out var twAxis);
            if (angle > 180f) angle -= 360f;
            var sign = Vector3.Dot(twAxis, axis) >= 0f ? 1f : -1f;
            var signedAngle = angle * sign;
            var apply = Quaternion.AngleAxis(signedAngle * Mathf.Clamp01(twistFollow), axis);
            lowerArm.rotation = apply * lowerArm.rotation;
        }

        static void SolveTwoBoneIK(Transform upper, Transform lower, Transform end,
            float upperLen, float lowerLen,
            Vector3 targetPos, Quaternion targetRot, Vector3 poleWorld,
            float weight, float wristWeight, float stretchClamp,
            Vector3 bodyUp, Vector3 bodyForward,
            float wristSnapMaxGap)
        {
            if (upper == null || lower == null || end == null || weight <= 0f)
                return;

            var preUpperRot = upper.rotation;
            var preLowerRot = lower.rotation;

            var shoulder = upper.position;
            var aToT = targetPos - shoulder;
            var distRaw = aToT.magnitude;
            if (distRaw < 1e-4f)
                return;

            var maxLen = upperLen + lowerLen;
            var maxStretched = maxLen * Mathf.Max(1f, stretchClamp);
            if (distRaw > maxStretched)
            {
                var scaled = maxStretched / distRaw;
                aToT *= scaled;
                targetPos = shoulder + aToT;
                distRaw = aToT.magnitude;
            }

            var dist = Mathf.Min(distRaw, maxLen * 0.9995f);

            var u2 = upperLen * upperLen;
            var l2 = lowerLen * lowerLen;
            var d2 = dist * dist;
            var denomShoulder = 2f * upperLen * dist;
            if (denomShoulder < 1e-6f)
                return;
            var cosShoulder = Mathf.Clamp((u2 + d2 - l2) / denomShoulder, -1f, 1f);
            var sinShoulder = Mathf.Sqrt(Mathf.Max(0f, 1f - cosShoulder * cosShoulder));

            var targetDir = aToT.normalized;

            var shoulderToPole = poleWorld - shoulder;
            var bendDir = shoulderToPole - Vector3.Dot(shoulderToPole, targetDir) * targetDir;
            if (bendDir.sqrMagnitude < 1e-6f)
                bendDir = Vector3.Cross(targetDir, bodyUp);
            if (bendDir.sqrMagnitude < 1e-6f)
                bendDir = Vector3.Cross(targetDir, bodyForward);
            if (bendDir.sqrMagnitude < 1e-6f)
                return;
            bendDir.Normalize();

            var elbowPos = shoulder + targetDir * (upperLen * cosShoulder) + bendDir * (upperLen * sinShoulder);

            var currentUpperDir = lower.position - shoulder;
            var desiredUpperDir = elbowPos - shoulder;
            if (currentUpperDir.sqrMagnitude > 1e-8f && desiredUpperDir.sqrMagnitude > 1e-8f)
                upper.rotation = Quaternion.FromToRotation(currentUpperDir, desiredUpperDir) * upper.rotation;

            var currentLowerDir = end.position - lower.position;
            var desiredLowerDir = targetPos - lower.position;
            if (currentLowerDir.sqrMagnitude > 1e-8f && desiredLowerDir.sqrMagnitude > 1e-8f)
                lower.rotation = Quaternion.FromToRotation(currentLowerDir, desiredLowerDir) * lower.rotation;

            if (weight < 1f)
            {
                upper.rotation = Quaternion.Slerp(preUpperRot, upper.rotation, weight);
                lower.rotation = Quaternion.Slerp(preLowerRot, lower.rotation, weight);
            }

            if (wristSnapMaxGap > 0f)
            {
                var gap = Vector3.Distance(end.position, targetPos);
                if (gap > 0f && gap <= wristSnapMaxGap)
                    end.position = targetPos;
            }

            var w = Mathf.Clamp01(wristWeight);
            if (w > 0f)
                end.rotation = Quaternion.Slerp(end.rotation, targetRot, w);
        }

        void CollapseAvatarHandsIfNeeded()
        {
            if (m_CollapseAvatarHandsAtWrist && m_HybridArmsMode)
            {
                if (m_LeftHandBone != null)
                    m_LeftHandBone.localScale = Vector3.zero;
                if (m_RightHandBone != null)
                    m_RightHandBone.localScale = Vector3.zero;
            }

            if (m_HideAvatarHeadByBoneScale && m_Animator != null && m_Animator.isHuman)
            {
                var headBone = m_Animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                    headBone.localScale = Vector3.zero;
            }
        }

        void ApplyHeadCameraNearClipIfNeeded()
        {
            if (!m_HideAvatarHeadByNearClip || m_HeadCamera == null)
                return;
            var cam = m_HeadCamera.GetComponent<Camera>();
            if (cam == null)
                return;
            if (cam.nearClipPlane < m_HeadHideNearClip)
                cam.nearClipPlane = m_HeadHideNearClip;
        }

        Transform m_HeadShadowProxy;

        void SpawnHeadShadowProxyIfNeeded()
        {
            if (!m_SpawnHeadShadowProxy || m_Animator == null || !m_Animator.isHuman)
                return;
            if (m_HeadShadowProxy != null)
                return;
            var headBone = m_Animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                return;

            var neck = m_Animator.GetBoneTransform(HumanBodyBones.Neck);
            var parent = neck != null ? neck : headBone.parent;
            if (parent == null)
                parent = m_BodyRoot;

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxy.name = "HeadShadowProxy";
            var col = proxy.GetComponent<Collider>();
            if (col != null) Destroy(col);

            proxy.transform.SetParent(parent, true);
            m_HeadShadowProxy = proxy.transform;

            var mr = proxy.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.allowOcclusionWhenDynamic = false;
            }
        }

        void UpdateHeadShadowProxy()
        {
            if (m_HeadShadowProxy == null || m_Animator == null || !m_Animator.isHuman)
                return;
            var headBone = m_Animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                return;

            var headWorldUp = headBone.parent != null ? headBone.parent.up : Vector3.up;
            m_HeadShadowProxy.position = headBone.position + headWorldUp * (m_HeadShadowProxyRadius * 0.5f);
            m_HeadShadowProxy.rotation = headBone.rotation;
            var diameter = m_HeadShadowProxyRadius * 2f;
            m_HeadShadowProxy.localScale = new Vector3(diameter, diameter, diameter);
        }

        void LateUpdate()
        {
            if (m_HeadCamera == null || m_BodyRoot == null)
                return;

            var targetYaw = m_HeadCamera.eulerAngles.y + m_YawOffsetDegrees;
            float y;
            if (m_YawSmoothTime > 0.001f)
                y = Mathf.SmoothDampAngle(m_BodyRoot.eulerAngles.y, targetYaw, ref m_SmoothYawVelocity, m_YawSmoothTime);
            else
                y = targetYaw;

            var e = m_BodyRoot.eulerAngles;
            m_BodyRoot.eulerAngles = new Vector3(e.x, y, e.z);

            var anchorActive = m_BodyFollowsHeadVertically || m_BodyFollowsHeadHorizontally;
            if (!anchorActive)
                m_BodyRoot.localPosition = m_LocalBodyOffset + m_LocalBodyVisualBias;

            if (m_DriveAnimatorSpeedFromCharacterController && m_HasSpeedParameter && m_Animator != null && m_CharacterController != null)
                m_Animator.SetFloat("Speed", m_CharacterController.velocity.magnitude);

            AnchorBodyToHeadCamera();
            AlignHeadBoneToHmd();
            SolveArmIkIfNeeded();
            CollapseAvatarHandsIfNeeded();
            UpdateHeadShadowProxy();
        }

        void AlignHeadBoneToHmd()
        {
            if (!m_SyncHeadBoneToHmd || m_Animator == null || !m_Animator.isHuman || m_HeadCamera == null)
                return;

            var headBone = m_Animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                return;

            var targetWorld = m_HeadCamera.rotation * Quaternion.Euler(m_HeadBoneOffsetEuler);
            var k = 1f - Mathf.Exp(-m_HeadBoneAlignSpeed * Time.deltaTime);
            k = Mathf.Clamp01(k) * m_HeadBoneAlignMaxWeight;
            headBone.rotation = Quaternion.Slerp(headBone.rotation, targetWorld, k);
        }

        void AnchorBodyToHeadCamera()
        {
            if ((!m_BodyFollowsHeadVertically && !m_BodyFollowsHeadHorizontally) ||
                m_HeadCamera == null || m_BodyRoot == null ||
                m_Animator == null || !m_Animator.isHuman)
                return;

            var head = m_Animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
                return;

            var horizontalBiasWorld = m_BodyRoot.right * m_HeadAnchorHorizontalBias.x +
                                      m_BodyRoot.forward * m_HeadAnchorHorizontalBias.y;
            var desired = m_HeadCamera.position + new Vector3(0f, m_HeadAnchorVerticalBias, 0f) + horizontalBiasWorld;
            var delta = desired - head.position;
            if (!m_BodyFollowsHeadHorizontally)
            {
                delta.x = 0f;
                delta.z = 0f;
            }
            if (!m_BodyFollowsHeadVertically)
                delta.y = 0f;

            if (!m_HeadAnchorInitialSnapDone || m_BodyAnchorInstantFollow)
            {
                m_BodyRoot.position += delta;
                m_HeadAnchorInitialSnapDone = true;
                return;
            }

            var k = 1f - Mathf.Exp(-m_HeadAnchorSmoothSpeed * Time.deltaTime);
            var step = delta * k;
            var maxStep = m_HeadAnchorMaxStepPerFrame;
            if (step.magnitude > maxStep)
                step = step.normalized * maxStep;
            m_BodyRoot.position += step;
        }

        void OnValidate()
        {
            if (m_BodyUniformScale < 0.01f)
                m_BodyUniformScale = 0.01f;
            if (m_SkinnedMeshBoundsExpansion < 0f)
                m_SkinnedMeshBoundsExpansion = 0f;
            if (m_TargetBodyWorldHeight < 0.5f)
                m_TargetBodyWorldHeight = 0.5f;
            if (m_MaxSpawnWaitFrames < 1)
                m_MaxSpawnWaitFrames = 1;
            if (m_HeadBoneAlignSpeed < 4f)
                m_HeadBoneAlignSpeed = 4f;
            if (m_HeadBoneAlignMaxWeight < 0f)
                m_HeadBoneAlignMaxWeight = 0f;
            else if (m_HeadBoneAlignMaxWeight > 1f)
                m_HeadBoneAlignMaxWeight = 1f;
            m_ArmIkWeight = Mathf.Clamp01(m_ArmIkWeight);
            m_WristMatchWeight = Mathf.Clamp01(m_WristMatchWeight);
            if (m_ArmReachStretchClamp < 1f)
                m_ArmReachStretchClamp = 1f;
            else if (m_ArmReachStretchClamp > 1.06f)
                m_ArmReachStretchClamp = 1.06f;
            if (m_WristSnapMaxGap < 0f)
                m_WristSnapMaxGap = 0f;
            else if (m_WristSnapMaxGap > 0.35f)
                m_WristSnapMaxGap = 0.35f;
            if (m_WristPositionGlueMaxMeters < 0.02f)
                m_WristPositionGlueMaxMeters = 0.02f;
            else if (m_WristPositionGlueMaxMeters > 0.12f)
                m_WristPositionGlueMaxMeters = 0.12f;
            if (m_HeadAnchorSmoothSpeed < 0f)
                m_HeadAnchorSmoothSpeed = 0f;
            if (m_HeadAnchorMaxStepPerFrame < 0f)
                m_HeadAnchorMaxStepPerFrame = 0f;
            m_ForearmTwistFollow = Mathf.Clamp01(m_ForearmTwistFollow);
            if (m_HandIkPullBackTowardElbow < 0f)
                m_HandIkPullBackTowardElbow = 0f;
            else if (m_HandIkPullBackTowardElbow > 0.20f)
                m_HandIkPullBackTowardElbow = 0.20f;
            if (m_HeadHideNearClip < 0.05f)
                m_HeadHideNearClip = 0.05f;
            else if (m_HeadHideNearClip > 0.40f)
                m_HeadHideNearClip = 0.40f;
            if (m_HeadShadowProxyRadius < 0.05f)
                m_HeadShadowProxyRadius = 0.05f;
            else if (m_HeadShadowProxyRadius > 0.20f)
                m_HeadShadowProxyRadius = 0.20f;
        }
    }
}
