using UnityEngine;
using System.Collections;

public class NvrBody : MonoBehaviour {
    // Enum for Head Tracking mode
    public enum HeadTrackingMode { None, Position2D, Position3D };
    public HeadTrackingMode headTrackingMode;
    // This should be the parent of Main Camera
    public Transform vrCameraRootTransform;
    // The distance away, on the X & Z axis- This follows on a circle around the followTarget
    public float cameraOffsetXZ = 0.18f;
    public float cameraOffsetY = 0.18f;
    // The distance to adjust the Y axis position
    public float cameraHeightOffset = 2.0f;
    // The angle at which a 'Turn' Animation will be triggered when Idle/Moving
    public float bodyTurnAngleIdle = 75f;
    public float bodyTurnAngleMoving = 10f;
    // If the body will rotate when looking straight down
    public bool rotateWhenLookingDown = true;
    // If the body will rotate when moving
    public bool rotateWhenMoving = true;
    // If the turn animation will play when moving
    public bool turnAnimWhenMoving = false;
    // If this script show show it's Editor Controls
    public bool showEditorControls = true;
    // New position to move camera to
    public Vector3 newCamPos;
    // How fast to play the walk/run animation
    public float walkAnimationSpeed = 1f;
    // If the camera movement should be smoothed
    public bool smoothCameraMovement;
    // Amount to smooth the camera
    public float cameraSmooth = 2f;


    // Animator on this player
    private Animator myAnim;
    // VRHead's (camera) rotation and this body's rotation
    private Vector3 vrRot, myRot;
    // Speed player is currently moving
    private float speed;
    // The previous world position (for calculating speed
    private Vector3 previousPosition;
    // My CharacterController
    private CharacterController myCC;
    // My Rigidbody
    private Rigidbody myRigidbody;
    // Main Camera
    private Transform vrCamera;
    // X, Y, Z values are determined at runtime for their position along a circle for which followOffsetXZ is the radius
    private float x, y, z;
    // Vectors for moving the camera
    private Vector3 cameraUp, cameraFw;
    // Is the camera upside down?
    private bool cameraUpsidedown;
    // Is the body currently turning?
    private bool bodyIsTurning;
    // Is the body allowed to turn? should the Turn Animate?
    private bool shouldBodyTurn, shouldTurnAnimate;
    // Current Velocity Magnitude
    private float myMagnitude;
    // Is this body using a RigidBody or CharacterController?
    private bool usingRB, usingCC, usingAnim;

    // Use this for initialization
    void Start() {
        if (vrCameraRootTransform == null) { Debug.LogErrorFormat("<color=#00ffffff><size=16><b>Please assign a Gameobject to field: 'VR Main Object' on the VRBody script.</b></size></color>"); };
        // Try to find main camera
        if (Camera.main == null) { Debug.LogErrorFormat("<color=#00ffffff><size=16><b>Make sure there is a Main Camera with Tag: 'MainCamera'.</b></size></color>"); }
        else { vrCamera = Camera.main.transform; }
        // Find my Animator
        myAnim = GetComponent<Animator>();
        if (myAnim == null) {
            Debug.LogWarning("Did not find an Animator on " + gameObject.name + ". Cannot animate walking/running.");
            usingAnim = false;
        }
        else {
            usingAnim = true;
            if (myAnim.GetLayerName(0) != "VRBodyBaseLayer") {
                Debug.LogWarningFormat("<color=#00ffffff><size=16><b>Animator's controller is not 'VRBodyAnimController'.</b></size> Assign the controller located at /VRBody/Animation/Controllers/ to the Animator.</color>");
                usingAnim = false;
            }
        }
        // Find CharacterController or Rigidbod
        myCC = GetComponent<CharacterController>();
        if (myCC == null) {
            myRigidbody = GetComponent<Rigidbody>();
            if (myRigidbody == null) {
                Debug.LogWarning("Did not find a CharacterController or Rigidbody on " + gameObject.name + ". Cannot animate walking/running.");
                usingCC = false; usingRB = false;
            } else { // Using Rigidbody
                usingCC = false; usingRB = true;
            }
        } else { // Using CharacterController
            usingCC = true; usingRB = false;
        }
        if (!showEditorControls) {showEditorControls = false; }//Prevents var assigned but never used warning
    }

    // Update is called once per frame
    void Update() {
        cameraUp = vrCamera.transform.up;
        cameraFw = vrCamera.transform.forward;
        vrRot = Camera.main.transform.rotation.eulerAngles;
        myRot = transform.rotation.eulerAngles;
        Quaternion rotateTo = Quaternion.identity;
        float bodyTurnAngle = bodyTurnAngleIdle;
        shouldBodyTurn = true;
        shouldTurnAnimate = true;
        // Upsidedown check
        if (cameraUp.y <= 0f && cameraFw.y < 0.5f) {
            cameraUpsidedown = true;
            if (rotateWhenLookingDown == false) { shouldBodyTurn = false; }
        }
        else { cameraUpsidedown = false; }
        // Moving check
        if (usingCC) { myMagnitude = myCC.velocity.magnitude; }
        else if (usingRB) { myMagnitude = myRigidbody.linearVelocity.magnitude; }
        else { myMagnitude = 0f; }
        if (myMagnitude > 0.1f) {
            bodyTurnAngle = bodyTurnAngleMoving;
            if (rotateWhenMoving == false) { shouldBodyTurn = false; }
            if (turnAnimWhenMoving == false) { shouldTurnAnimate = false; }
        }
        if (usingAnim == false) shouldTurnAnimate = false;
        // Rotate the body
        if (shouldBodyTurn == true) {
            if (cameraUpsidedown) {
                if (Mathf.DeltaAngle(vrRot.y - 180f, myRot.y) > bodyTurnAngle) {
                    if (shouldTurnAnimate == true) { if (myAnim.GetBool("bTurnLeft") == false) { myAnim.SetBool("bTurnLeft", true); } }
                    rotateTo = Quaternion.Euler(new Vector3(myRot.x, vrRot.y - 180f, myRot.z));
                    bodyIsTurning = true;
                }
                else if (Mathf.DeltaAngle(vrRot.y - 180f, myRot.y) < -bodyTurnAngle) {
                    if (shouldTurnAnimate == true) { if (myAnim.GetBool("bTurnRight") == false) { myAnim.SetBool("bTurnRight", true); } }
                    rotateTo = Quaternion.Euler(new Vector3(myRot.x, vrRot.y - 180f, myRot.z));
                    bodyIsTurning = true;
                }
                else if (bodyIsTurning == true) {
                    rotateTo = Quaternion.Euler(new Vector3(myRot.x, vrRot.y - 180f, myRot.z));
                }
            // Camera right-side up
            } else {
                if (Mathf.DeltaAngle(vrRot.y, myRot.y) > bodyTurnAngle) {
                    if (shouldTurnAnimate == true) { if (myAnim.GetBool("bTurnLeft") == false) { myAnim.SetBool("bTurnLeft", true); } }
                    rotateTo = Quaternion.Euler(new Vector3(myRot.x, vrRot.y, myRot.z));
                    bodyIsTurning = true;
                }
                else if (Mathf.DeltaAngle(vrRot.y, myRot.y) < -bodyTurnAngle) {
                    if (shouldTurnAnimate == true) { if (myAnim.GetBool("bTurnRight") == false) { myAnim.SetBool("bTurnRight", true); } }
                    rotateTo = Quaternion.Euler(new Vector3(myRot.x, vrRot.y, myRot.z));
                    bodyIsTurning = true;
                }
                else if (bodyIsTurning == true) {
                    rotateTo = Quaternion.Euler(new Vector3(myRot.x, vrRot.y, myRot.z));
                }
            }
        }
        if (bodyIsTurning == true) {
            // Get a value between 0 and 1 based on how big the angle is between body and camera
            float a = Mathf.DeltaAngle(vrRot.y, myRot.y);
            if (cameraUpsidedown) a = Mathf.DeltaAngle(vrRot.y-180f, myRot.y);
            if (a < 0) { // Right turn
                a += 180f;
                a /= 180f;
            } else { // Left turn
                a -= 180f;
                a *= -1f;
                a /= 180f;
            }
            // Set rotation with lerp
            transform.rotation = Quaternion.Lerp(transform.rotation, rotateTo, a * 0.1f);
            if (a > 0.95f) {
                bodyIsTurning = false;
            }
        } else {
            if (usingAnim == true) {
                if (myAnim.GetBool("bTurnLeft") == true) { myAnim.SetBool("bTurnLeft", false); }
                if (myAnim.GetBool("bTurnRight") == true) { myAnim.SetBool("bTurnRight", false); }
            }
        }
        // Set the speed variable for the animator, this could be a CharacterController or Rigidbody
        if (usingAnim == true) { myAnim.SetFloat("Speed", myMagnitude * walkAnimationSpeed); }
    }

    // Late Update for Camera Follow
    void LateUpdate() {
        if (headTrackingMode == HeadTrackingMode.Position3D) {
            cameraUp = vrCamera.transform.up;
            cameraFw = vrCamera.transform.forward;
            if (cameraUp.y < 0.1f && cameraFw.y < 0.5f) {
                x = cameraOffsetXZ * Mathf.Sin(transform.rotation.eulerAngles.y * Mathf.Deg2Rad);
                y = cameraHeightOffset - cameraOffsetY * 2;
                y += cameraOffsetY * Mathf.Sin(vrCamera.rotation.eulerAngles.x * Mathf.Deg2Rad);
                z = cameraOffsetXZ * Mathf.Cos(transform.rotation.eulerAngles.y * Mathf.Deg2Rad);
                newCamPos = transform.position + new Vector3(x, y, z);
                if (smoothCameraMovement == true) {
                    vrCameraRootTransform.position = Vector3.Lerp(vrCameraRootTransform.position, newCamPos, Vector3.Distance(vrCameraRootTransform.position, newCamPos) * cameraSmooth/2);
                } else {
                    vrCameraRootTransform.position = newCamPos;
                }
            }
            else {
                x = cameraOffsetXZ * Mathf.Sin(vrCamera.rotation.eulerAngles.y * Mathf.Deg2Rad);
                y = cameraOffsetY * Mathf.Sin(vrCamera.rotation.eulerAngles.x * Mathf.Deg2Rad);
                y = cameraHeightOffset - y;
                z = cameraOffsetXZ * Mathf.Cos(vrCamera.rotation.eulerAngles.y * Mathf.Deg2Rad);
                newCamPos = transform.position + new Vector3(x, y, z);
                if (smoothCameraMovement == true) {
                    vrCameraRootTransform.position = Vector3.Lerp(vrCameraRootTransform.position, newCamPos, Vector3.Distance(vrCameraRootTransform.position, newCamPos) * cameraSmooth);
                }
                else {
                    vrCameraRootTransform.position = newCamPos;
                }
                
            }
        }
        else if (headTrackingMode == HeadTrackingMode.Position2D) {
            cameraUp = vrCamera.transform.up;
            cameraFw = vrCamera.transform.forward;
            if (cameraUp.y < 0.1f && cameraFw.y < 0.5f) {
                x = cameraOffsetXZ * Mathf.Sin(transform.rotation.eulerAngles.y * Mathf.Deg2Rad);
                y = cameraHeightOffset;
                z = cameraOffsetXZ * Mathf.Cos(transform.rotation.eulerAngles.y * Mathf.Deg2Rad);
                newCamPos = transform.position + new Vector3(x, y, z);
                if (smoothCameraMovement == true) {
                    vrCameraRootTransform.position = Vector3.Lerp(vrCameraRootTransform.position, newCamPos, Vector3.Distance(vrCameraRootTransform.position, newCamPos) * cameraSmooth / 2);
                }
                else {
                    vrCameraRootTransform.position = newCamPos;
                }
            }
            else {
                x = cameraOffsetXZ * Mathf.Sin(vrCamera.rotation.eulerAngles.y * Mathf.Deg2Rad);
                y = cameraHeightOffset;
                z = cameraOffsetXZ * Mathf.Cos(vrCamera.rotation.eulerAngles.y * Mathf.Deg2Rad);
                newCamPos = transform.position + new Vector3(x, y, z);
                if (smoothCameraMovement == true) {
                    vrCameraRootTransform.position = Vector3.Lerp(vrCameraRootTransform.position, newCamPos, Vector3.Distance(vrCameraRootTransform.position, newCamPos) * cameraSmooth);
                }
                else {
                    vrCameraRootTransform.position = newCamPos;
                }
            }
        }
        else if (headTrackingMode == HeadTrackingMode.None) {
            x = 0f; z = 0f;
            y = cameraHeightOffset;
            newCamPos = transform.position + new Vector3(x, y, z);
            if (smoothCameraMovement == true) {
                vrCameraRootTransform.position = Vector3.Lerp(vrCameraRootTransform.position, newCamPos, Vector3.Distance(vrCameraRootTransform.position, newCamPos) * cameraSmooth);
            }
            else {
                vrCameraRootTransform.position = newCamPos;
            }
        }
        
    }
    void OnDrawGizmosSelected() {
        if (showEditorControls && Application.isPlaying) {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(new Vector3(transform.position.x, transform.position.y + cameraHeightOffset, transform.position.z), newCamPos);
        }
    }
}
