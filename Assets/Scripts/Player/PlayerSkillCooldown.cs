using UnityEngine;

public class PlayerSkillCooldown : MonoBehaviour
{
    [System.Serializable]
    public class CooldownSlot
    {
        public string slotName = "Skill";
        public float baseCooldown = 3f;
        [HideInInspector] public float remaining;
        [HideInInspector] public float total;
    }

    [SerializeField] private CooldownSlot[] slots = new CooldownSlot[5]
    {
        new CooldownSlot{ slotName="Skill1 (Q)", baseCooldown=4f },
        new CooldownSlot{ slotName="Skill2 (E)", baseCooldown=5f },
        new CooldownSlot{ slotName="Ultimate (R)", baseCooldown=12f },
        new CooldownSlot{ slotName="Companion Command", baseCooldown=8f },
        new CooldownSlot{ slotName="Companion Ultimate", baseCooldown=20f },
    };

    [Tooltip("Map combat skill index (0-3) -> cooldown slot index. -1 = no cooldown")]
    [SerializeField] private int[] combatToCooldownMap = new int[] { -1, 0, 1, 2 };

    private void Update()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].remaining > 0f)
            {
                slots[i].remaining -= Time.deltaTime;
                if (slots[i].remaining < 0f) slots[i].remaining = 0f;
            }
        }
    }

    public void StartCooldownForCombatIndex(int combatSkillIndex)
    {
        int slotIndex = GetCooldownSlot(combatSkillIndex);
        if (slotIndex < 0) return;
        StartCooldown(slotIndex);
    }

    public void StartCooldownForCombatIndex(int combatSkillIndex, float duration)
    {
        int slotIndex = GetCooldownSlot(combatSkillIndex);
        if (slotIndex < 0) return;
        StartCooldown(slotIndex, duration);
    }

    public void StartCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        slots[slotIndex].remaining = slots[slotIndex].baseCooldown;
        slots[slotIndex].total = slots[slotIndex].baseCooldown;
    }

    /// <summary>Override cooldown duration (vd: lấy từ SkillData.cooldown). duration <= 0 = dùng baseCooldown trong inspector.</summary>
    public void StartCooldown(int slotIndex, float duration)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        float final = duration > 0f ? duration : slots[slotIndex].baseCooldown;
        slots[slotIndex].remaining = final;
        slots[slotIndex].total = final;
    }

    public string GetSlotName(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return string.Empty;
        return slots[slotIndex].slotName;
    }

    public bool IsOnCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;
        return slots[slotIndex].remaining > 0.01f;
    }

    public bool CanUseCombatSkill(int combatSkillIndex)
    {
        int slotIndex = GetCooldownSlot(combatSkillIndex);
        if (slotIndex < 0) return true;
        return !IsOnCooldown(slotIndex);
    }

    public float GetRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return 0f;
        return slots[slotIndex].remaining;
    }

    public float GetTotal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return 1f;
        return slots[slotIndex].total;
    }

    public int SlotCount => slots.Length;

    private int GetCooldownSlot(int combatSkillIndex)
    {
        if (combatSkillIndex < 0 || combatSkillIndex >= combatToCooldownMap.Length) return -1;
        return combatToCooldownMap[combatSkillIndex];
    }
}
