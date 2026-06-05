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
        // Chờ 1 frame để player/controller spawn xong
        yield return null;

        if (GameDataManager.Instance == null)
        {
            Debug.LogWarning("[PlayerPositionRestore] Không có GameDataManager.");
            yield break;
        }

        if (restoreOnlyWhenContinue && !GameDataManager.Instance.ShouldLoadFromContinue())
        {
            if (showDebugLog)
                Debug.Log("[PlayerPositionRestore] Không có cờ Continue/Restore nên không restore.");
            yield break;
        }

        string currentScene = SceneManager.GetActiveScene().name;


        // Lấy vị trí đã lưu riêng cho scene hiện tại
        if (!GameDataManager.Instance.TryGetScenePosition(currentScene, out Vector3 savedPos))
        {
            if (showDebugLog)
                Debug.Log($"[PlayerPositionRestore] Scene '{currentScene}' chưa có vị trí lưu.");

            GameDataManager.Instance.ClearContinueFlag();
            yield break;
        }

        float savedRotY = GameDataManager.Instance.GetLastSavedRotationY();

        CharacterController cc = GetComponent<CharacterController>();
        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (cc != null) cc.enabled = false;
        if (agent != null) agent.enabled = false;

        transform.position = savedPos;
        transform.rotation = Quaternion.Euler(0f, savedRotY, 0f);

        if (agent != null) agent.enabled = true;
        if (cc != null) cc.enabled = true;

        RestorePlayerStatsIfPossible();

        GameDataManager.Instance.ClearContinueFlag();

        if (showDebugLog)
        {
            Debug.Log($"[PlayerPositionRestore] Restored scene={currentScene}, pos={savedPos}, rotY={savedRotY}");
        }
    }

    private void RestorePlayerStatsIfPossible()
    {
        CharacterHealth health = GetComponent<CharacterHealth>();
        if (health == null) return;
        if (health.RuntimeStats == null) return;
        if (!GameDataManager.Instance.HasPlayerData) return;

        var s = health.RuntimeStats;

        s.currentHP = GameDataManager.Instance.PlayerHP;
        s.currentStamina = GameDataManager.Instance.PlayerStamina;
        s.currentEnergy = GameDataManager.Instance.PlayerEnergy;

        if (showDebugLog)
        {
            Debug.Log($"[PlayerPositionRestore] Restored stats HP={s.currentHP}, Stamina={s.currentStamina}, Energy={s.currentEnergy}");
        }
    }
}