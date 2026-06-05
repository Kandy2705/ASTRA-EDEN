using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerPositionRestore : MonoBehaviour
{
    private void Start()
    {
        if (GameDataManager.Instance == null) return;

        Vector3 savedPos;
        if (GameDataManager.Instance.TryGetScenePosition(SceneManager.GetActiveScene().name, out savedPos))
        {
            transform.position = savedPos;
        }
    }
}
