using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < Mathf.Min(posSceneNames.Count, posValues.Count); i++)
        {
            scenePositions[posSceneNames[i]] = posValues[i];
        }
    }

    public void AddCurrency(int delta)
    {
        Currency = currency + delta;
    }

    public void SavePlayerStats(float hp, float stamina, float energy)
    {
        playerHP = hp;
        playerStamina = stamina;
        playerEnergy = energy;
    }

    public void ClearPlayerStats()
    {
        playerHP = -1f;
        playerStamina = -1f;
        playerEnergy = -1f;
    }

    public void SaveScenePosition(string sceneName, Vector3 position)
    {
        scenePositions[sceneName] = position;
        SyncPosToLists();
    }

    public bool TryGetScenePosition(string sceneName, out Vector3 position)
    {
        return scenePositions.TryGetValue(sceneName, out position);
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
