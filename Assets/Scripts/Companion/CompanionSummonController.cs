using UnityEngine;

[DisallowMultipleComponent]
public class CompanionSummonController : MonoBehaviour
{
    [Header("Companion")]
    [SerializeField] private GameObject companionPrefab;
    [SerializeField] private Vector3 spawnOffset = new Vector3(-1.5f, 0f, -1.5f);
    [SerializeField] private bool summonOnStart = true;

    [Header("Input")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerSkillCooldown skillCooldown;

    [Header("Cooldown Slots")]
    [SerializeField] private int companionCommandSlotIndex = 3;
    [SerializeField] private int companionSkillSlotIndex = 4;

    CompanionController companion;

    public CompanionController Companion => companion;

    void Awake()
    {
        if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
        if (skillCooldown == null) skillCooldown = GetComponent<PlayerSkillCooldown>();
    }

    void Start()
    {
        if (summonOnStart)
        {
            SummonCompanion();
        }
    }

    void Update()
    {
        if (companion == null || inputReader == null)
        {
            return;
        }

        if (inputReader.CompanionCommandPressed)
        {
            if (skillCooldown == null || !skillCooldown.IsOnCooldown(companionCommandSlotIndex))
            {
                if (companion.TryCommandAttack())
                {
                    skillCooldown?.StartCooldown(companionCommandSlotIndex);
                }
            }
        }

        if (inputReader.CompanionSkillPressed)
        {
            if (skillCooldown == null || !skillCooldown.IsOnCooldown(companionSkillSlotIndex))
            {
                if (companion.TryUseSkill())
                {
                    skillCooldown?.StartCooldown(companionSkillSlotIndex);
                }
            }
        }
    }

    [ContextMenu("Summon Companion")]
    public void SummonCompanion()
    {
        if (companion != null || companionPrefab == null)
        {
            return;
        }

        Vector3 pos = transform.position + transform.TransformVector(spawnOffset);
        GameObject instance = Instantiate(companionPrefab, pos, transform.rotation);
        companion = instance.GetComponent<CompanionController>();
        if (companion == null)
        {
            companion = instance.AddComponent<CompanionController>();
        }

        companion.Initialize(transform);
    }
}