using UnityEngine;

/// <summary>
/// Player death: animation (bool IsDead + force state Death), không ragdoll.
/// Enemy đọc <see cref="IsPlayerDead"/>.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)]
public sealed class PlayerDeathController : MonoBehaviour
{
    static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int BlendHash = Animator.StringToHash("Blend");
    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");

    public static PlayerDeathController Instance { get; private set; }

    public static bool IsPlayerDead =>
        Instance != null && Instance.isDead
        || (Instance == null && FindPlayerHealthIfAny()?.IsDead == true);

    [Header("References")]
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAnimatorBridge animatorBridge;

    [Header("Death Animation")]
    [SerializeField] private string isDeadBoolName = "IsDead";
    [SerializeField] private string deathStateName = "Death";
    [Tooltip("CrossFade vào state Death (không chỉ set bool — chặn Move còn chạy).")]
    [SerializeField] private bool forcePlayDeathState = true;
    [SerializeField, Min(0f)] private float deathCrossFade = 0.08f;
    [SerializeField] private bool alsoSetDieTrigger;
    [SerializeField] private bool keepAnimatorEnabled = true;

    [Header("Disable on death")]
    [SerializeField] private bool disablePlayerController = true;
    [SerializeField] private bool disableCombat = true;
    [SerializeField] private bool disableInput = true;
    [SerializeField] private bool disableCharacterController = false;
    [SerializeField] private bool disableRagdollOnDeath = true;

    bool isDead;
    bool deathStateForced;
    int isDeadHash;

    public bool IsDead => isDead;

    public void ReviveForDebug()
    {
        if (!isDead || characterHealth == null || characterHealth.IsDead)
        {
            return;
        }

        isDead = false;
        deathStateForced = false;

        if (animatorBridge != null)
        {
            animatorBridge.enabled = true;
            animatorBridge.SetDead(false);
        }

        if (animator != null)
        {
            if (HasBool(isDeadHash))
            {
                animator.SetBool(isDeadHash, false);
            }
            else if (HasBool(IsDeadHash))
            {
                animator.SetBool(IsDeadHash, false);
            }

            if (animator.HasState(0, Animator.StringToHash("Base Layer.Move")))
            {
                animator.Play("Base Layer.Move", 0, 0f);
                animator.Update(0f);
            }
        }

        if (disableInput)
        {
            PlayerInputReader input = GetComponent<PlayerInputReader>();
            if (input != null) input.enabled = true;
        }

        if (disablePlayerController)
        {
            PlayerController movement = GetComponent<PlayerController>();
            if (movement != null) movement.enabled = true;
        }

        if (disableCombat)
        {
            PlayerCombatController combat = GetComponent<PlayerCombatController>();
            if (combat != null) combat.enabled = true;
        }

        if (disableCharacterController)
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = true;
        }

        PlayerInteractController interact = GetComponent<PlayerInteractController>();
        if (interact != null) interact.enabled = true;

        CompanionSummonController summon = GetComponent<CompanionSummonController>();
        if (summon != null) summon.enabled = true;

        Debug.Log("[PlayerDeath] Debug revive — Player controls restored.", this);
    }

    void Awake()
    {
        Instance = this;
        isDeadHash = Animator.StringToHash(isDeadBoolName);

        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animatorBridge == null)
        {
            animatorBridge = GetComponent<PlayerAnimatorBridge>();
        }

        if (disableRagdollOnDeath)
        {
            RagdollOnDeath ragdoll = GetComponent<RagdollOnDeath>();
            if (ragdoll != null)
            {
                ragdoll.SetControlledExternally(true);
                ragdoll.enabled = false;
            }
        }
    }

    void OnEnable()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
            characterHealth.Died += HandleDied;
            characterHealth.Changed -= HandleHealthChanged;
            characterHealth.Changed += HandleHealthChanged;
        }

        if (characterHealth != null && characterHealth.IsDead)
        {
            ApplyDeath();
        }
    }

    void OnDisable()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
            characterHealth.Changed -= HandleHealthChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void LateUpdate()
    {
        // Khi chết: giữ IsDead + zero locomotion (tránh Move blend tree).
        // Không CrossFade mỗi frame — sẽ restart clip → nhìn như "đơ" frame 0.
        if (!isDead || animator == null)
        {
            return;
        }

        if (HasBool(isDeadHash))
        {
            animator.SetBool(isDeadHash, true);
        }

        ZeroLocomotionParams();

        if (forcePlayDeathState && !string.IsNullOrEmpty(deathStateName) && !deathStateForced)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(deathStateName) || st.IsName("Base Layer." + deathStateName))
            {
                deathStateForced = true;
            }
            else if (!animator.IsInTransition(0))
            {
                // Một lần ép vào Death nếu chưa vào (sau transition).
                animator.CrossFadeInFixedTime(deathStateName, deathCrossFade, 0, 0f);
                deathStateForced = true;
            }
        }
    }

    void HandleHealthChanged(CharacterHealth h)
    {
        if (h != null && h.IsDead && !isDead)
        {
            ApplyDeath();
        }
    }

    void HandleDied(CharacterHealth h) => ApplyDeath();

    void ApplyDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            if (keepAnimatorEnabled)
            {
                animator.enabled = true;
            }

            // Reset triggers có thể kéo ra attack/skill
            SafeResetTrigger(Animator.StringToHash("Attack"));
            SafeResetTrigger(Animator.StringToHash("CastSkill"));
            SafeResetTrigger(Animator.StringToHash("Jump"));
            SafeResetTrigger(Animator.StringToHash("Hit"));

            if (HasBool(isDeadHash))
            {
                animator.SetBool(isDeadHash, true);
            }
            else if (HasBool(IsDeadHash))
            {
                animator.SetBool(IsDeadHash, true);
                isDeadHash = IsDeadHash;
            }
            else
            {
                Debug.LogWarning(
                    $"[PlayerDeath] Thiếu bool '{isDeadBoolName}' trên Animator.",
                    this);
            }

            ZeroLocomotionParams();

            if (alsoSetDieTrigger && HasTrigger(DieHash))
            {
                animator.ResetTrigger(DieHash);
                animator.SetTrigger(DieHash);
            }

            // Ép vào state Death — quan trọng hơn chỉ set bool.
            if (forcePlayDeathState && !string.IsNullOrEmpty(deathStateName))
            {
                deathStateForced = false;
                // Play ngay (không chờ transition) để clip chạy từ đầu.
                animator.Play(deathStateName, 0, 0f);
                animator.Update(0f);
                deathStateForced = true;

                // Log clip có motion không
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                if (clips != null && clips.Length > 0 && clips[0].clip != null)
                {
                    Debug.Log(
                        $"[PlayerDeath] Playing clip '{clips[0].clip.name}' length={clips[0].clip.length:F2}s",
                        this);
                }
                else
                {
                    Debug.LogWarning(
                        "[PlayerDeath] State Death không có AnimationClip (Missing reference trên controller). " +
                        "Kéo lại clip 'Two Handed Sword Death' vào state Death.",
                        this);
                }
            }
        }

        if (animatorBridge != null)
        {
            animatorBridge.SetDead(true);
            animatorBridge.enabled = false;
        }

        if (disableInput)
        {
            var input = GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.enabled = false;
            }
        }

        if (disablePlayerController)
        {
            var move = GetComponent<PlayerController>();
            if (move != null)
            {
                move.enabled = false;
            }
        }

        if (disableCombat)
        {
            var combat = GetComponent<PlayerCombatController>();
            if (combat != null)
            {
                combat.enabled = false;
            }
        }

        if (disableCharacterController)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }
        }

        var interact = GetComponent<PlayerInteractController>();
        if (interact != null)
        {
            interact.enabled = false;
        }

        var summon = GetComponent<CompanionSummonController>();
        if (summon != null)
        {
            summon.enabled = false;
        }

        Debug.Log("[PlayerDeath] Death applied — IsDead + force state Death, Move stopped.", this);
    }

    void ZeroLocomotionParams()
    {
        if (animator == null)
        {
            return;
        }

        if (HasFloat(BlendHash))
        {
            animator.SetFloat(BlendHash, 0f);
        }

        if (HasFloat(HorizontalHash))
        {
            animator.SetFloat(HorizontalHash, 0f);
        }

        if (HasFloat(VerticalHash))
        {
            animator.SetFloat(VerticalHash, 0f);
        }
    }

    void SafeResetTrigger(int hash)
    {
        if (HasTrigger(hash))
        {
            animator.ResetTrigger(hash);
        }
    }

    bool HasBool(int hash) => HasParam(hash, AnimatorControllerParameterType.Bool);
    bool HasTrigger(int hash) => HasParam(hash, AnimatorControllerParameterType.Trigger);
    bool HasFloat(int hash) => HasParam(hash, AnimatorControllerParameterType.Float);

    bool HasParam(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == hash && p.type == type)
            {
                return true;
            }
        }

        return false;
    }

    static CharacterHealth FindPlayerHealthIfAny()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<CharacterHealth>() : null;
    }
}
