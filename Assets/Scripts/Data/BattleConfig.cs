using UnityEngine;

namespace SurvivorArena.Data
{
    /// <summary>
    /// 测试战斗场景的全部可调数值（Data 层）。
    /// 运行时代码只读这里的值，不在 MonoBehaviour 里散落魔法数字；
    /// 改数值只需要改这个资产，不需要改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Survivor Arena/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Header("波次")]
        [Tooltip("波次表 CSV（Assets/DataDocs/level01_enemy_spawn.csv）。")]
        [SerializeField] private TextAsset waveTableCsv;

        [Tooltip("单局时长（秒）。")]
        [SerializeField, Min(1f)] private float runDurationSeconds = 300f;

        [Header("主角")]
        [SerializeField, Min(0f)] private float playerMoveSpeed = 4.2f;

        [Header("测试攻击：击杀光环")]
        [SerializeField, Min(0.1f)] private float auraRadius = 2f;

        [Header("蝙蝠生成")]
        [SerializeField, Min(0f)] private float enemySpawnRadiusMin = 12f;
        [SerializeField, Min(0f)] private float enemySpawnRadiusMax = 14f;
        [SerializeField, Min(0f)] private float enemySpeedMin = 2.4f;
        [SerializeField, Min(0f)] private float enemySpeedMax = 2.9f;
        [SerializeField, Min(0.01f)] private float enemyRadius = 0.4f;
        [SerializeField, Min(0)] private int enemyPrewarmCount = 48;
        [SerializeField, Min(1)] private int enemyMaxSpawnsPerFrame = 8;

        [Header("离屏回收")]
        [SerializeField, Min(1f)] private float recycleDistance = 16f;
        [SerializeField, Min(0f)] private float recycleMargin = 1f;

        public TextAsset WaveTableCsv => waveTableCsv;

        public float RunDurationSeconds => runDurationSeconds;

        public float PlayerMoveSpeed => playerMoveSpeed;

        public float AuraRadius => auraRadius;

        public float EnemySpawnRadiusMin => enemySpawnRadiusMin;

        public float EnemySpawnRadiusMax => enemySpawnRadiusMax;

        public float EnemySpeedMin => enemySpeedMin;

        public float EnemySpeedMax => enemySpeedMax;

        public float EnemyRadius => enemyRadius;

        public int EnemyPrewarmCount => enemyPrewarmCount;

        public int EnemyMaxSpawnsPerFrame => enemyMaxSpawnsPerFrame;

        public float RecycleDistance => recycleDistance;

        public float RecycleMargin => recycleMargin;

        private void OnValidate()
        {
            runDurationSeconds = Mathf.Max(1f, runDurationSeconds);
            playerMoveSpeed = Mathf.Max(0f, playerMoveSpeed);
            auraRadius = Mathf.Max(0.1f, auraRadius);
            enemySpawnRadiusMin = Mathf.Max(0f, enemySpawnRadiusMin);
            enemySpawnRadiusMax = Mathf.Max(enemySpawnRadiusMin, enemySpawnRadiusMax);
            enemySpeedMin = Mathf.Max(0f, enemySpeedMin);
            enemySpeedMax = Mathf.Max(enemySpeedMin, enemySpeedMax);
            enemyRadius = Mathf.Max(0.01f, enemyRadius);
            enemyPrewarmCount = Mathf.Max(0, enemyPrewarmCount);
            enemyMaxSpawnsPerFrame = Mathf.Max(1, enemyMaxSpawnsPerFrame);
            recycleDistance = Mathf.Max(1f, recycleDistance);
            recycleMargin = Mathf.Max(0f, recycleMargin);
        }
    }
}
