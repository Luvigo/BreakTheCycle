using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class DemoAutowalk : MonoBehaviour {
    // Move speed
    public float moveSpeed = 3f;
    // If the player is allowed to move (don't allow movement when looking at UI)
    public bool allowMovement = true;
    // My character controller
    private CharacterController myCC;
    // Should I move forward or not
    private bool moveForward;
    
    // Use this for initialization
    void Start () {
        // Get CharacterController
        myCC = GetComponent<CharacterController>();
    }
	
	// Update is called once per frame
	void Update () {
        // If there is an interactive object at gaze and it's within distance
        if (allowMovement == false) {
            // do not allow movement
            if (moveForward == true) { moveForward = false; }
        }
        // Otherwise if the Google VR button, or the Gear VR touchpad is pressed
        else if (Input.GetButtonDown("Fire1")) {
            // Change the state of moveForward
            moveForward = !moveForward;
            if (moveForward == false) { myCC.SimpleMove(Vector3.zero); }
        }
        // Check to see if I should move
        if (moveForward) {
            // Find the forward direction
            Vector3 forward = Camera.main.transform.TransformDirection(Vector3.forward);
            // Tell CharacterController to move forward
            myCC.SimpleMove(forward * moveSpeed);
        }
    }

    public void AllowMovement(bool status) {
        allowMovement = status;
        if (moveForward == true) {
            moveForward = false;
        }
    }
}
