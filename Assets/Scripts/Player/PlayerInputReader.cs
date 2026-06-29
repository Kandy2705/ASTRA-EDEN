using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
public class PlayerInputReader : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] private Key jumpKey = Key.Space;
    [SerializeField] private Key dashKey = Key.LeftCtrl;
    [SerializeField] private Key runKey = Key.LeftShift;
    [SerializeField] private Key attackKey = Key.J;
    [SerializeField] private Key skill1Key = Key.Q;
    [SerializeField] private Key skill2Key = Key.E;
    [SerializeField] private Key skill3Key = Key.R;
    [SerializeField] private Key interactKey = Key.F;
    [SerializeField] private Key companionCommandKey = Key.T;
    [SerializeField] private Key companionSkillKey = Key.G;
    [SerializeField] private bool allowMouseLeftAttack = true;

    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool RunHeld { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool Skill1Pressed { get; private set; }
    public bool Skill2Pressed { get; private set; }
    public bool Skill3Pressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool CompanionCommandPressed { get; private set; }
    public bool CompanionSkillPressed { get; private set; }
    public int SkillIndexPressed { get; private set; } = -1;

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
        Skill1Pressed = keyboard[skill1Key].wasPressedThisFrame;
        Skill2Pressed = keyboard[skill2Key].wasPressedThisFrame;
        Skill3Pressed = keyboard[skill3Key].wasPressedThisFrame;
        InteractPressed = keyboard[interactKey].wasPressedThisFrame;
        CompanionCommandPressed = keyboard[companionCommandKey].wasPressedThisFrame;
        CompanionSkillPressed = keyboard[companionSkillKey].wasPressedThisFrame;
        SkillIndexPressed = Skill1Pressed ? 1 : Skill2Pressed ? 2 : Skill3Pressed ? 3 : -1;
    }

    private bool IsMouseLeftAttackPressed()
    {
        if (!allowMouseLeftAttack || Mouse.current == null)
        {
            return false;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        // Nếu đang click lên UI Button thì không đánh
        if (IsPointerOverUI())
        {
            return false;
        }

        return true;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return EventSystem.current.IsPointerOverGameObject();
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
