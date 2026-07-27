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
        JumpPressed = keyboard[GetKey(GameControlAction.Jump, jumpKey)].wasPressedThisFrame;
        DashPressed = keyboard[GetKey(GameControlAction.Dash, dashKey)].wasPressedThisFrame;
        RunHeld = keyboard[GetKey(GameControlAction.Run, runKey)].isPressed;
        AttackPressed =
            keyboard[GetKey(GameControlAction.Attack, attackKey)].wasPressedThisFrame ||
            IsMouseLeftAttackPressed();
        Skill1Pressed = keyboard[GetKey(GameControlAction.Skill1, skill1Key)].wasPressedThisFrame;
        Skill2Pressed = keyboard[GetKey(GameControlAction.Skill2, skill2Key)].wasPressedThisFrame;
        Skill3Pressed = keyboard[GetKey(GameControlAction.Skill3, skill3Key)].wasPressedThisFrame;
        InteractPressed = keyboard[GetKey(GameControlAction.Interact, interactKey)].wasPressedThisFrame;
        CompanionCommandPressed =
            keyboard[GetKey(GameControlAction.CompanionCommand, companionCommandKey)].wasPressedThisFrame;
        CompanionSkillPressed =
            keyboard[GetKey(GameControlAction.CompanionSkill, companionSkillKey)].wasPressedThisFrame;
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

        if (keyboard[GetKey(GameControlAction.MoveLeft, Key.A)].isPressed)
        {
            h -= 1f;
        }

        if (keyboard[GetKey(GameControlAction.MoveRight, Key.D)].isPressed)
        {
            h += 1f;
        }

        if (keyboard[GetKey(GameControlAction.MoveBackward, Key.S)].isPressed)
        {
            v -= 1f;
        }

        if (keyboard[GetKey(GameControlAction.MoveForward, Key.W)].isPressed)
        {
            v += 1f;
        }

        return new Vector2(h, v).normalized;
    }

    private static Key GetKey(GameControlAction action, Key fallback)
    {
        Key configured = GameSettingsManager.GetBinding(action);
        return configured != Key.None ? configured : fallback;
    }
}
