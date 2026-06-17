using UnityEngine;

[System.Serializable]
public class EnemyBaseStats
{
    [Min(1f)] public float maxHP = 100f;
    [Min(0f)] public float attack = 10f;
    [Min(0f)] public float defense = 0f;
    [Min(0f)] public float poise = 0f;

    [Min(0f)] public float moveSpeed = 3f;
    [Tooltip("Tốc độ xoay (độ/giây) — dùng cho NavMeshAgent.angularSpeed.")]
    [Min(0f)] public float turnSpeed = 600f;
}
