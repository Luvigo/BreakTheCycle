using Unity.XR.CoreUtils;
using UnityEngine;

namespace MundoMental.VR
{
    /// <summary>Un solo AudioListener en la cámara del XR Origin (evita audio mudo o en posición fija).</summary>
    public static class VrAudioListenerUtility
    {
        static Camera s_ConfiguredCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_ConfiguredCamera = null;

        public static void EnsureSingleListenerOnRigCamera()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;

            Camera rigCam = origin.Camera;
            if (s_ConfiguredCamera == rigCam)
                return;

            if (rigCam.GetComponent<AudioListener>() == null)
                rigCam.gameObject.AddComponent<AudioListener>();

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                var al = listeners[i];
                if (al == null)
                    continue;
                bool isRig = al.gameObject == rigCam.gameObject ||
                             al.transform.IsChildOf(rigCam.transform);
                al.enabled = isRig;
            }

            s_ConfiguredCamera = rigCam;
        }
    }
}
