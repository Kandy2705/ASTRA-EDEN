using Drakkar;
using UnityEngine;

/// <summary>
/// Đảm bảo DrakkarUpdater tồn tại trước khi player animation gọi DrakkarTrail.Begin().
/// Thiếu object này sẽ gây NullReferenceException khi đánh (StartTrail → TryAddLate).
/// </summary>
public static class DrakkarUpdaterBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureUpdaterExists()
    {
        if (DrakkarUpdater.instance != null)
        {
            return;
        }

        var go = new GameObject("DrakkarUpdater");
        go.AddComponent<DrakkarUpdater>();
        Object.DontDestroyOnLoad(go);
    }
}