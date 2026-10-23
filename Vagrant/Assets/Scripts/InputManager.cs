using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMotor motor;
    private PlayerLook look;
    private BuildingSystem building;
    private Bow bow;

    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();
        building = GetComponent<BuildingSystem>();
        bow = GetComponent<Bow>();

        // when the Jump button is pressed, tell the motor to jump
        onFoot.Jump.performed += ctx => motor.Jump();

        // Equipping the hammer / bow is handled by the ActionBar (keys 1-8 and the mouse wheel).
        if (building == null)
        {
            Debug.LogError("InputManager: no BuildingSystem component found on this object. Add it to the Player.");
        }
        else
        {
            onFoot.Place.performed += ctx => building.Place();
            onFoot.Rotate.performed += ctx => building.RotatePiece();
            onFoot.Interact.performed += ctx => building.Interact();
        }
    }

    void Update()
    {
        // Sprint and Crouch are Button actions.
        motor.ProcessMove(
            onFoot.Movement.ReadValue<Vector2>(),
            onFoot.Sprint.IsPressed(),
            onFoot.Crouch.IsPressed());

        // Holding the Wheel button (right mouse) opens the build menu while the hammer is out.
        if (building != null) building.UpdateWheel(onFoot.Wheel.IsPressed());

        // Hold left mouse to draw the bow, release to shoot.
        if (bow != null) bow.Tick(onFoot.Place.IsPressed());
    }

    private void LateUpdate()
    {
        Vector2 lookInput = onFoot.Look.ReadValue<Vector2>();

        // While the build wheel is open, mouse movement picks a slice instead of turning the camera.
        if (building != null && building.WheelOpen)
        {
            building.FeedWheelInput(lookInput);
            lookInput = Vector2.zero;
        }

        // FreeLook is a Button action: hold to look around without turning the body.
        look.ProcessLook(lookInput, onFoot.FreeLook.IsPressed());

        // Update the building ghost after the camera has moved this frame.
        if (building != null) building.Refresh();
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}