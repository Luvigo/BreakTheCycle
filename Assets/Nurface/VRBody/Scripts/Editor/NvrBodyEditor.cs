using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(NvrBody))]
public class NvrBodyEditor : Editor {

    private SerializedProperty vrCameraRootTransform;
    private SerializedProperty cameraOffsetXZ;
    private SerializedProperty cameraOffsetY;
    private SerializedProperty cameraHeightOffset;
    private SerializedProperty rotateWhenLookingDown;
    private SerializedProperty rotateWhenMoving;    
    private SerializedProperty bodyTurnAngleIdle;
    private SerializedProperty bodyTurnAngleMoving;
    private SerializedProperty walkAnimationSpeed;
    private SerializedProperty turnAnimWhenMoving;
    private SerializedProperty smoothCameraMovement;
    private SerializedProperty cameraSmooth;

    private static GUIStyle ToggleButtonStyleNormal = null;
    private static GUIStyle ToggleButtonStyleToggled = null;
    private static bool toggleA, toggleB, toggleC;
    private static NvrBody t;

    private string[] tooltips = new string[17]{
        "This script has 3 operating modes: None, 2D, and 3D.",
        "Default VR head rotation. The camera stays in a fixed position (0,0,0) at the head’s location.",
        "Camera moves on the X and Z axis only, in a perfect circle around the head. This method moves the camera away from 0,0,0 at a defined offset value on the X and Z axis. However the Y position remains constant.",
        "Camera moves on the X,Y,Z axis to try to simulate the most realistic head and eye movements. When looking up and to the left, the camera will move into a position also up and to the left, depending on XZ and Y offset values.",
        "Show the in-scene controls for manipulating the Camera Offset XZ, Camera Offset Y, and Camera Height Offset values.",
        "The ‘VRMain’ GameObject. This is the parent of the Main Camera.",
        "Smooth the camera's movements via Lerp.",
        "How fast the camera moves during a smooth movement.",
        "Should the body rotate when walking / moving? If you are using a waypoint movement system, you may not want to the body to rotate when it’s moving between waypoints.",
        "Should the ‘turn left’ and ‘turn right’ animations play when the player is moving? It does not look good to play to the turn animations while the walking animation is playing.",
        "Should the body rotate when looking down? If the camera is looking straight down (or upside down), do not rotate the player’s body. This is useful is you want to show a menu when looking straight down.",
        "This is the offset on the XZ axis between ‘center of the head’ (0,0,0) and the camera position. The position is drawn in purple in the editor and you can adjust it by clicking the purple box.",
        "This is the offset on the Y axis between ‘center of head’ and camera position. This adjusts how far the camera will move up and down when the head rotates up/down. This value is drawn in yellow and can be adjusted by clicking the yellow box.",
        "This is how far from the Character’s Feet to move the camera UP, so that it is in the correct position. This should be the height of the eyeballs and it is shown in the editor as a White Arrow. You can adjust the height with the white arrow.",
        "When the character is idle and not moving, at which angle should the body turn to match the camera’s position? Normally when we stand and look around in VR, our head turns some distance to the left and right before we pick up our feet and turn. This is probably about 90 degrees, because after a 90 degree turn of the head, we must really stretch to look behind ourselves.",
        "When the character is moving, at which angle should the body turn to match the camera’s position? When we are moving we will tend to walk looking straight ahead of us, so this angle should be reduced a lot from the idle value.",
        "How fast to play the walk animation. You can adjust this value so the feet will not look like they are ‘sliding’ on the ground when walking."
    };

    void OnEnable() {
        t = target as NvrBody;
        vrCameraRootTransform = serializedObject.FindProperty("vrCameraRootTransform");
        cameraOffsetXZ = serializedObject.FindProperty("cameraOffsetXZ");
        cameraOffsetY = serializedObject.FindProperty("cameraOffsetY");
        cameraHeightOffset = serializedObject.FindProperty("cameraHeightOffset");
        rotateWhenLookingDown = serializedObject.FindProperty("rotateWhenLookingDown");
        rotateWhenMoving = serializedObject.FindProperty("rotateWhenMoving");
        bodyTurnAngleIdle = serializedObject.FindProperty("bodyTurnAngleIdle");
        bodyTurnAngleMoving = serializedObject.FindProperty("bodyTurnAngleMoving");
        walkAnimationSpeed = serializedObject.FindProperty("walkAnimationSpeed");
        turnAnimWhenMoving = serializedObject.FindProperty("turnAnimWhenMoving");
        smoothCameraMovement = serializedObject.FindProperty("smoothCameraMovement");
        cameraSmooth = serializedObject.FindProperty("cameraSmooth");
    }

    public override void OnInspectorGUI() {
        if (ToggleButtonStyleNormal == null) { ToggleButtonStyleNormal = "Button"; }
        if (ToggleButtonStyleToggled == null) {
            ToggleButtonStyleToggled = new GUIStyle(ToggleButtonStyleNormal);
            ToggleButtonStyleToggled.normal.background = ToggleButtonStyleToggled.active.background;
        }
        RenderSectionHeader("Head Movement:", tooltips[0]);
        GUILayout.BeginHorizontal();
        if (t.headTrackingMode == NvrBody.HeadTrackingMode.None)       { toggleA = true; toggleB = false; toggleC = false; }
        if (t.headTrackingMode == NvrBody.HeadTrackingMode.Position2D) { toggleA = false; toggleB = true; toggleC = false; }
        if (t.headTrackingMode == NvrBody.HeadTrackingMode.Position3D) { toggleA = false; toggleB = false; toggleC = true; }
        if (GUILayout.Button(new GUIContent("None", tooltips[1]), toggleA ? ToggleButtonStyleToggled : ToggleButtonStyleNormal)) {
            t.headTrackingMode = NvrBody.HeadTrackingMode.None;
            toggleA = true; toggleB = false; toggleC = false;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button(new GUIContent("2D(X,Z)", tooltips[2]), toggleB ? ToggleButtonStyleToggled : ToggleButtonStyleNormal)) {
            t.headTrackingMode = NvrBody.HeadTrackingMode.Position2D;
            toggleA = false; toggleB = true; toggleC = false;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button(new GUIContent("3D(XYZ)", tooltips[3]), toggleC ? ToggleButtonStyleToggled : ToggleButtonStyleNormal)) {
            t.headTrackingMode = NvrBody.HeadTrackingMode.Position3D;
            toggleA = false; toggleB = false; toggleC = true;
            SceneView.RepaintAll();
        }
        GUILayout.EndHorizontal();
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        t.showEditorControls = EditorGUILayout.Toggle(new GUIContent("Show Editor Controls", tooltips[4]), t.showEditorControls);
        if (EditorGUI.EndChangeCheck()) { SceneView.RepaintAll(); }
        RenderSeparator();        
        EditorGUILayout.PropertyField(vrCameraRootTransform, new GUIContent("VR Camera Root:", tooltips[5]));
        EditorGUILayout.PropertyField(smoothCameraMovement, new GUIContent("Smooth Camera Movement:", tooltips[6]));
        if (smoothCameraMovement.boolValue) {
            EditorGUILayout.PropertyField(cameraSmooth, new GUIContent("Camera Smooth Speed:", tooltips[7]));
        }
        if (t.headTrackingMode == NvrBody.HeadTrackingMode.Position2D || t.headTrackingMode == NvrBody.HeadTrackingMode.Position3D) {
            EditorGUILayout.PropertyField(rotateWhenMoving, new GUIContent("Rotate When Moving:", tooltips[6]));
            if (rotateWhenMoving.boolValue == true) {
                EditorGUILayout.PropertyField(turnAnimWhenMoving, new GUIContent("Turn Animation When Moving:", tooltips[7]));
            }
            EditorGUILayout.PropertyField(rotateWhenLookingDown, new GUIContent("Rotate When Looking Down:", tooltips[8]));
            EditorGUILayout.PropertyField(cameraOffsetXZ, new GUIContent("Camera Offset XZ:", tooltips[9]));
        }

        if (t.headTrackingMode == NvrBody.HeadTrackingMode.Position3D) {
            EditorGUILayout.PropertyField(cameraOffsetY, new GUIContent("Camera Offset Y:", tooltips[10]));
        }
        
        EditorGUILayout.PropertyField(cameraHeightOffset, new GUIContent("Camera Height Offset:", tooltips[11]));
        EditorGUILayout.PropertyField(bodyTurnAngleIdle, new GUIContent("Body Turn Angle Idle:", tooltips[12]));
        EditorGUILayout.PropertyField(bodyTurnAngleMoving, new GUIContent("Body Turn Angle Moving:", tooltips[13]));
        EditorGUILayout.PropertyField(walkAnimationSpeed, new GUIContent("Walk Animation Speed:", tooltips[14]));
        serializedObject.ApplyModifiedProperties();
    }

    void OnSceneGUI() {
        if (t.showEditorControls) {
            Vector3 handlePosition = new Vector3(t.transform.position.x, t.transform.position.y + t.cameraHeightOffset, t.transform.position.z);
            EditorGUI.BeginChangeCheck();
            float heightOffset = Handles.ScaleValueHandle(t.cameraHeightOffset, handlePosition, Quaternion.Euler(90, 0, 0), 1.0f, Handles.ArrowHandleCap, 0.0f);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(t, "VRBody.cs cameraHeightOffset");
                t.cameraHeightOffset = heightOffset;
            }
            if (t.headTrackingMode == NvrBody.HeadTrackingMode.Position2D || t.headTrackingMode == NvrBody.HeadTrackingMode.Position3D) {
                // XZ Offset Disc
                Handles.color = new Color(0.541f, 0.168f, 0.886f, 1f);
                Handles.DrawWireDisc(handlePosition, t.transform.up, t.cameraOffsetXZ);
                EditorGUI.BeginChangeCheck();
                float offsetXZ = Handles.ScaleValueHandle(t.cameraOffsetXZ, handlePosition + (t.transform.forward * t.cameraOffsetXZ), t.transform.rotation, t.cameraOffsetXZ, Handles.CubeHandleCap, 0f);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(t, "VRBody.cs cameraOffsetXZ");
                    t.cameraOffsetXZ = offsetXZ;
                }
            }
            if (t.headTrackingMode == NvrBody.HeadTrackingMode.Position3D) {
                // Y Offset Disc
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(handlePosition, t.transform.forward, t.cameraOffsetY);
                EditorGUI.BeginChangeCheck();
                float offsetY = Handles.ScaleValueHandle(t.cameraOffsetY, handlePosition + (t.transform.up * t.cameraOffsetY), t.transform.rotation, t.cameraOffsetY, Handles.CubeHandleCap, 0f);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(t, "VRBody.cs cameraOffsetY");
                    t.cameraOffsetY = offsetY;
                }
            }
        }
    }

    void RenderSectionHeader(string header, string tooltip = "") {
        EditorGUILayout.LabelField(new GUIContent(header, tooltip), EditorStyles.largeLabel, GUILayout.Height(20f));
    }
    void RenderSeparator() {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }
}