using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMotor motor;
    private PlayerLook look;

    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();

        // when the Jump button is pressed, tell the motor to jump
        onFoot.Jump.performed += ctx => motor.Jump();
    }

    void Update()
    {
        // Sprint and Crouch are Button actions (see setup notes).
        motor.ProcessMove(
            onFoot.Movement.ReadValue<Vector2>(),
            onFoot.Sprint.IsPressed(),
            onFoot.Crouch.IsPressed());
    }

    private void LateUpdate()
    {
        // FreeLook is a Button action: hold to look around without turning the body.
        look.ProcessLook(onFoot.Look.ReadValue<Vector2>(), onFoot.FreeLook.IsPressed());
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
