using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] private Key jumpKey = Key.Space;
    [SerializeField] private Key dashKey = Key.LeftCtrl;
    [SerializeField] private Key runKey = Key.LeftShift;
    [SerializeField] private Key attackKey = Key.J;
    [SerializeField] private bool allowMouseLeftAttack = true;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool RunHeld { get; private set; }
    public bool AttackPressed { get; private set; }

    private void Update()
    {
        ReadInput();
    }

    public void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            MoveInput = Vector2.zero;
            JumpPressed = false;
            DashPressed = false;
            RunHeld = false;
            AttackPressed = false;
            return;
        }

        MoveInput = ReadMovementInput(keyboard);
        JumpPressed = keyboard[jumpKey].wasPressedThisFrame;
        DashPressed = keyboard[dashKey].wasPressedThisFrame;
        RunHeld = keyboard[runKey].isPressed;
        AttackPressed = keyboard[attackKey].wasPressedThisFrame || IsMouseLeftAttackPressed();
    }

    private bool IsMouseLeftAttackPressed()
    {
        if (!allowMouseLeftAttack || Mouse.current == null)
        {
            return false;
        }

        return Mouse.current.leftButton.wasPressedThisFrame;
    }

    private static Vector2 ReadMovementInput(Keyboard keyboard)
    {
        float h = 0f;
        float v = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            h -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            h += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            v -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            v += 1f;
        }

        return new Vector2(h, v).normalized;
    }
}
