using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    private const string HasSaveKey = "ASTRA_HAS_SAVE";
    private const string LastSceneKey = "ASTRA_LAST_SCENE";

    private const string LastPosXKey = "ASTRA_LAST_POS_X";
    private const string LastPosYKey = "ASTRA_LAST_POS_Y";
    private const string LastPosZKey = "ASTRA_LAST_POS_Z";

    private const string LastRotYKey = "ASTRA_LAST_ROT_Y";
    private const string ContinueFlagKey = "ASTRA_LOAD_FROM_CONTINUE";

    [Header("Tiền tệ")]
    [SerializeField] private int currency;

    [Header("Dữ liệu Player")]
    [SerializeField] private float playerHP = -1f;
    [SerializeField] private float playerStamina = -1f;
    [SerializeField] private float playerEnergy = -1f;

    [Header("Vị trí Player theo scene")]
    [SerializeField] private List<string> posSceneNames = new List<string>();
    [SerializeField] private List<Vector3> posValues = new List<Vector3>();

    private Dictionary<string, Vector3> scenePositions = new Dictionary<string, Vector3>();

    public int Currency
    {
        get => currency;
        set => currency = Mathf.Max(0, value);
    }

    public float PlayerHP
    {
        get => playerHP;
        set => playerHP = value;
    }

    public float PlayerStamina
    {
        get => playerStamina;
        set => playerStamina = value;
    }

    public float PlayerEnergy
    {
        get => playerEnergy;
        set => playerEnergy = value;
    }

    public bool HasPlayerData => playerHP >= 0f;

    public bool HasSave => PlayerPrefs.GetInt(HasSaveKey, 0) == 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadRuntimePositionLists();
        LoadPersistentData();
    }

    private void LoadRuntimePositionLists()
    {
        scenePositions.Clear();

        for (int i = 0; i < Mathf.Min(posSceneNames.Count, posValues.Count); i++)
        {
            scenePositions[posSceneNames[i]] = posValues[i];
        }
    }

    public void AddCurrency(int delta)
    {
        Currency = currency + delta;
        SavePersistentData();
    }

    public void SavePlayerStats(float hp, float stamina, float energy)
    {
        playerHP = hp;
        playerStamina = stamina;
        playerEnergy = energy;

        SavePersistentData();
    }

    public void ClearPlayerStats()
    {
        playerHP = -1f;
        playerStamina = -1f;
        playerEnergy = -1f;

        SavePersistentData();
    }

    public void SaveScenePosition(string sceneName, Vector3 position)
    {
        scenePositions[sceneName] = position;
        SyncPosToLists();

        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetString(LastSceneKey, sceneName);

        PlayerPrefs.SetFloat(LastPosXKey, position.x);
        PlayerPrefs.SetFloat(LastPosYKey, position.y);
        PlayerPrefs.SetFloat(LastPosZKey, position.z);

        PlayerPrefs.Save();

        Debug.Log($"[GameDataManager] Save position scene={sceneName}, pos={position}");
    }

    public void SaveLastPlayerTransform(string sceneName, Transform playerTransform)
    {
        if (playerTransform == null) return;

        Vector3 pos = playerTransform.position;
        float rotY = playerTransform.eulerAngles.y;

        SaveScenePosition(sceneName, pos);

        PlayerPrefs.SetFloat(LastRotYKey, rotY);
        PlayerPrefs.Save();

        Debug.Log($"[GameDataManager] Save transform scene={sceneName}, pos={pos}, rotY={rotY}");
    }

    public bool TryGetScenePosition(string sceneName, out Vector3 position)
    {
        return scenePositions.TryGetValue(sceneName, out position);
    }

    public string GetLastSceneName(string fallbackScene)
    {
        return PlayerPrefs.GetString(LastSceneKey, fallbackScene);
    }

    public Vector3 GetLastSavedPosition()
    {
        float x = PlayerPrefs.GetFloat(LastPosXKey, 0f);
        float y = PlayerPrefs.GetFloat(LastPosYKey, 1f);
        float z = PlayerPrefs.GetFloat(LastPosZKey, 0f);

        return new Vector3(x, y, z);
    }

    public float GetLastSavedRotationY()
    {
        return PlayerPrefs.GetFloat(LastRotYKey, 0f);
    }

    public void MarkLoadFromContinue()
    {
        PlayerPrefs.SetInt(ContinueFlagKey, 1);
        PlayerPrefs.Save();
    }

    public bool ShouldLoadFromContinue()
    {
        return PlayerPrefs.GetInt(ContinueFlagKey, 0) == 1;
    }

    public void ClearContinueFlag()
    {
        PlayerPrefs.DeleteKey(ContinueFlagKey);
        PlayerPrefs.Save();
    }

    public void DeleteSaveData()
    {
        currency = 0;
        ClearPlayerStats();

        scenePositions.Clear();
        SyncPosToLists();

        PlayerPrefs.DeleteKey(HasSaveKey);
        PlayerPrefs.DeleteKey(LastSceneKey);

        PlayerPrefs.DeleteKey(LastPosXKey);
        PlayerPrefs.DeleteKey(LastPosYKey);
        PlayerPrefs.DeleteKey(LastPosZKey);

        PlayerPrefs.DeleteKey(LastRotYKey);
        PlayerPrefs.DeleteKey(ContinueFlagKey);

        PlayerPrefs.DeleteKey("ASTRA_CURRENCY");
        PlayerPrefs.DeleteKey("ASTRA_PLAYER_HP");
        PlayerPrefs.DeleteKey("ASTRA_PLAYER_STAMINA");
        PlayerPrefs.DeleteKey("ASTRA_PLAYER_ENERGY");

        PlayerPrefs.Save();

        Debug.Log("[GameDataManager] Delete save data.");
    }

    private void SavePersistentData()
    {
        PlayerPrefs.SetInt(HasSaveKey, 1);

        PlayerPrefs.SetInt("ASTRA_CURRENCY", currency);
        PlayerPrefs.SetFloat("ASTRA_PLAYER_HP", playerHP);
        PlayerPrefs.SetFloat("ASTRA_PLAYER_STAMINA", playerStamina);
        PlayerPrefs.SetFloat("ASTRA_PLAYER_ENERGY", playerEnergy);

        PlayerPrefs.Save();
    }

    private void LoadPersistentData()
    {
        currency = PlayerPrefs.GetInt("ASTRA_CURRENCY", currency);
        playerHP = PlayerPrefs.GetFloat("ASTRA_PLAYER_HP", playerHP);
        playerStamina = PlayerPrefs.GetFloat("ASTRA_PLAYER_STAMINA", playerStamina);
        playerEnergy = PlayerPrefs.GetFloat("ASTRA_PLAYER_ENERGY", playerEnergy);

        string lastScene = PlayerPrefs.GetString(LastSceneKey, "");
        if (!string.IsNullOrEmpty(lastScene))
        {
            Vector3 lastPos = GetLastSavedPosition();
            scenePositions[lastScene] = lastPos;
            SyncPosToLists();
        }
    }

    private void SyncPosToLists()
    {
        posSceneNames.Clear();
        posValues.Clear();

        foreach (var kvp in scenePositions)
        {
            posSceneNames.Add(kvp.Key);
            posValues.Add(kvp.Value);
        }
    }
}