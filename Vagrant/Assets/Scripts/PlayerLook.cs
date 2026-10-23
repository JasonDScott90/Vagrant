using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;
    private float xRotation = 0f;

    public float xSensitivity = 0.15f;
    public float ySensitivity = 0.15f;

    // Start is called before the first frame update
    void Start()
    {
        // find the camera on the player automatically
        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        // hide and lock the mouse cursor (press Escape to get it back)
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ProcessLook(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;

        // calculate camera rotation for looking up and down
        xRotation -= mouseY * ySensitivity;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        // apply this to our camera transform
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        // rotate the player to look left and right
        transform.Rotate(Vector3.up * mouseX * xSensitivity);
    }
}