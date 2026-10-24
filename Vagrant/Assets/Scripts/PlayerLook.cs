using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;
    private float xRotation = 0f;   // pitch (up/down)
    private float freeLookYaw = 0f; // head turn relative to the body while free looking

    public float xSensitivity = 0.15f;
    public float ySensitivity = 0.15f;

    [Header("Free Look")]
    public float maxFreeLookYaw = 120f;   // how far you can turn your head left/right
    public float freeLookReturnSpeed = 12f; // how fast the view snaps back on release
    [Header("Recoil")]
    public float recoilRecoverSpeed = 6f;  // how fast the gunfire kick settles back

    float recoilPitch = 0f;                // temporary upward kick added by gunfire
    float recoilYaw = 0f;                  // temporary sideways kick added by gunfire

    void Start()
    {
        // find the camera on the player automatically
        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        // hide and lock the mouse cursor (press Escape to get it back)
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ProcessLook(Vector2 input, bool freeLook)
    {
        float mouseX = input.x;
        float mouseY = input.y;

        // up / down always moves the camera
        xRotation -= mouseY * ySensitivity;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);

        if (freeLook)
        {
            // head turns, body (and therefore movement direction) stays put
            freeLookYaw += mouseX * xSensitivity;
            freeLookYaw = Mathf.Clamp(freeLookYaw, -maxFreeLookYaw, maxFreeLookYaw);
        }
        else
        {
            // ease the head back to forward, then steer the body as normal
            freeLookYaw = Mathf.Lerp(freeLookYaw, 0f, freeLookReturnSpeed * Time.deltaTime);
            if (Mathf.Abs(freeLookYaw) < 0.05f) freeLookYaw = 0f;

            transform.Rotate(Vector3.up * mouseX * xSensitivity);
        }

                // settle the gunfire kick back toward zero, then aim with it applied
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, recoilRecoverSpeed * Time.deltaTime);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, recoilRecoverSpeed * Time.deltaTime);

        cam.transform.localRotation = Quaternion.Euler(xRotation + recoilPitch, freeLookYaw + recoilYaw, 0f);
    }

    /// Gunfire kicks the camera up (and slightly sideways); it settles back on its own.
    public void AddRecoil(float pitch, float yaw)
    {
        recoilPitch = Mathf.Min(recoilPitch + pitch, 22f);
        recoilYaw = Mathf.Clamp(recoilYaw + yaw, -12f, 12f);
    }
}
