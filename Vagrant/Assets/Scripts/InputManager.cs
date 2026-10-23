using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMotor motor;
    private PlayerLook look;
    private BuildingSystem building;

    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();
        building = GetComponent<BuildingSystem>();

                // when the Jump button is pressed, tell the motor to jump (ignored while a UI screen is open)
        onFoot.Jump.performed += ctx => { if (!StorageUI.IsOpen) motor.Jump(); };

        if (building == null)
        {
            Debug.LogError("InputManager: no BuildingSystem component found on this object. Add it to the Player.");
        }
        else
        {
                        // building actions are ignored while the storage screen is open
            onFoot.Hammer.performed += ctx => { if (!StorageUI.IsOpen) building.ToggleHammer(); };
            onFoot.Place.performed += ctx => { if (!StorageUI.IsOpen) building.Place(); };
            onFoot.Rotate.performed += ctx => { if (!StorageUI.IsOpen) building.RotatePiece(); };
            onFoot.Interact.performed += ctx => { if (!StorageUI.IsOpen) building.Interact(); };
        }
    }

        void Update()
    {
        // While the storage screen is open the mouse drives the UI: freeze the player.
        if (StorageUI.IsOpen)
        {
            motor.ProcessMove(Vector2.zero, false, false);
            return;
        }

        // Sprint and Crouch are Button actions.
        motor.ProcessMove(
            onFoot.Movement.ReadValue<Vector2>(),
            onFoot.Sprint.IsPressed(),
            onFoot.Crouch.IsPressed());

        // Holding the Wheel button (right mouse) opens the build menu while the hammer is out.
        if (building != null) building.UpdateWheel(onFoot.Wheel.IsPressed());
    }

    private void LateUpdate()
    {
                Vector2 lookInput = onFoot.Look.ReadValue<Vector2>();

        // While the storage screen is open the mouse drives the UI, not the camera.
        if (StorageUI.IsOpen) lookInput = Vector2.zero;

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

