using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class PlayerPositionRestore : MonoBehaviour
{
    [Header("Restore Settings")]
    public bool restoreOnlyWhenContinue = true;

    [Header("Debug")]
    public bool showDebugLog = true;

    private IEnumerator Start()
    {
        yield return null;

        if (GameDataManager.Instance == null)
        {
            yield break;
        }

        string currentScene = SceneManager.GetActiveScene().name;

        bool shouldRestore = !restoreOnlyWhenContinue || GameDataManager.Instance.ShouldLoadFromContinue();

        if (shouldRestore)
        {
            if (GameDataManager.Instance.TryGetScenePosition(currentScene, out Vector3 savedPos))
            {
                float savedRotY = GameDataManager.Instance.GetLastSavedRotationY();

                CharacterController cc = GetComponent<CharacterController>();
                NavMeshAgent agent = GetComponent<NavMeshAgent>();

                if (cc != null) cc.enabled = false;
                if (agent != null) agent.enabled = false;

                transform.position = savedPos;
                transform.rotation = Quaternion.Euler(0f, savedRotY, 0f);

                if (agent != null) agent.enabled = true;
                if (cc != null) cc.enabled = true;
            }

            GameDataManager.Instance.ClearContinueFlag();
        }

        // Stats luôn restore nếu đã có save (Awake CharacterHealth cũng restore — gọi lại để chắc sau 1 frame).
        RestorePlayerStatsIfPossible();

        // QUAN TRỌNG:
        // Sau khi vào scene hiện tại, lưu scene này thành scene mới nhất để Continue biết.
        GameDataManager.Instance.SaveLastPlayerTransform(currentScene, transform);
    }

    private void RestorePlayerStatsIfPossible()
    {
        CharacterHealth health = GetComponent<CharacterHealth>();
        if (health == null) return;
        if (health.RuntimeStats == null) return;
        if (GameDataManager.Instance == null || !GameDataManager.Instance.HasPlayerData) return;

        health.ApplySavedVitals(
            GameDataManager.Instance.PlayerHP,
            GameDataManager.Instance.PlayerStamina,
            GameDataManager.Instance.PlayerEnergy);

        if (showDebugLog && health.RuntimeStats != null)
        {
            var s = health.RuntimeStats;
            Debug.Log($"[PlayerPositionRestore] Restored stats HP={s.currentHP}/{s.maxHP}, Stamina={s.currentStamina}/{s.staminaMax}, Energy={s.currentEnergy}/{s.energyMax}");
        }
    }
}