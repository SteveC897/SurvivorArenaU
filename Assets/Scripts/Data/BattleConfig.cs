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

        [Header("蝙蝠生成")]
        [SerializeField, Min(0f)] private float enemySpawnRadiusMin = 8f;
        [SerializeField, Min(0f)] private float enemySpawnRadiusMax = 10f;
        [SerializeField, Min(0f)] private float enemySpeedMin = 2.4f;
        [SerializeField, Min(0f)] private float enemySpeedMax = 2.9f;
        [SerializeField, Min(0.01f)] private float enemyRadius = 0.4f;
        [SerializeField, Min(0)] private int enemyPrewarmCount = 48;
        [SerializeField, Min(1)] private int enemyMaxSpawnsPerFrame = 8;

        [Header("离屏回收")]
        [SerializeField, Min(1f)] private float recycleDistance = 16f;
        [SerializeField, Min(0f)] private float recycleMargin = 1f;

        [Header("局内内容目录")]
        [SerializeField] private ContentCatalog contentCatalog;

        [Header("经验与升级")]
        [SerializeField] private int randomSeed = 20261031;
        [SerializeField, Min(1)] private int xpGemMaxCount = 256;
        [SerializeField, Min(0.1f)] private float xpGemMagnetRadius = 2f;
        [SerializeField, Min(0.05f)] private float xpGemPickupRadius = 0.6f;
        [SerializeField, Min(1f)] private float xpGemMagnetSpeed = 12f;
        [SerializeField, Min(1)] private int maxWeaponSlots = 6;
        [SerializeField, Min(1)] private int maxPassiveSlots = 6;
        [SerializeField, Min(0.1f)] private float baseProjectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 2.5f;
        [SerializeField, Min(0.1f)] private float garlicBaseRadius = 2f;

        [Header("敌人战斗")]
        [SerializeField, Min(1f)] private float enemyHealth = 5f;
        [SerializeField, Min(0)] private int enemyXp = 1;

        [Header("Boss 与宝箱")]
        [SerializeField] private float[] bossSpawnTimes = { 60f, 150f, 270f };
        [SerializeField] private int[] bossChestRewards = { 1, 3, 5 };
        [SerializeField, Min(1f)] private float bossSpawnRadius = 7f;
        [SerializeField, Min(1f)] private float bossHealthBase = 120f;
        [SerializeField, Min(0f)] private float bossHealthPerSpawn = 120f;
        [SerializeField, Min(0.1f)] private float bossSpeed = 1.2f;
        [SerializeField, Min(0.01f)] private float bossRadius = 0.8f;
        [SerializeField, Min(1f)] private float bossVisualScale = 1.8f;
        [SerializeField, Min(0)] private int bossXp = 30;
        [SerializeField, Min(0.1f)] private float chestPickupRadius = 1.2f;

        public TextAsset WaveTableCsv => waveTableCsv;

        public float RunDurationSeconds => runDurationSeconds;

        public float PlayerMoveSpeed => playerMoveSpeed;

        public float EnemySpawnRadiusMin => enemySpawnRadiusMin;

        public float EnemySpawnRadiusMax => enemySpawnRadiusMax;

        public float EnemySpeedMin => enemySpeedMin;

        public float EnemySpeedMax => enemySpeedMax;

        public float EnemyRadius => enemyRadius;

        public int EnemyPrewarmCount => enemyPrewarmCount;

        public int EnemyMaxSpawnsPerFrame => enemyMaxSpawnsPerFrame;

        public float RecycleDistance => recycleDistance;

        public float RecycleMargin => recycleMargin;

        public ContentCatalog ContentCatalog => contentCatalog;

        public int RandomSeed => randomSeed;

        public int XpGemMaxCount => xpGemMaxCount;

        public float XpGemMagnetRadius => xpGemMagnetRadius;

        public float XpGemPickupRadius => xpGemPickupRadius;

        public float XpGemMagnetSpeed => xpGemMagnetSpeed;

        public int MaxWeaponSlots => maxWeaponSlots;

        public int MaxPassiveSlots => maxPassiveSlots;

        public float BaseProjectileSpeed => baseProjectileSpeed;

        public float ProjectileLifetime => projectileLifetime;

        public float GarlicBaseRadius => garlicBaseRadius;

        public float EnemyHealth => enemyHealth;

        public int EnemyXp => enemyXp;

        public float[] BossSpawnTimes => bossSpawnTimes;

        public int[] BossChestRewards => bossChestRewards;

        public float BossSpawnRadius => bossSpawnRadius;

        public float BossHealthBase => bossHealthBase;

        public float BossHealthPerSpawn => bossHealthPerSpawn;

        public float BossSpeed => bossSpeed;

        public float BossRadius => bossRadius;

        public float BossVisualScale => bossVisualScale;

        public int BossXp => bossXp;

        public float ChestPickupRadius => chestPickupRadius;

        private void OnValidate()
        {
            runDurationSeconds = Mathf.Max(1f, runDurationSeconds);
            playerMoveSpeed = Mathf.Max(0f, playerMoveSpeed);
            enemySpawnRadiusMin = Mathf.Max(0f, enemySpawnRadiusMin);
            enemySpawnRadiusMax = Mathf.Max(enemySpawnRadiusMin, enemySpawnRadiusMax);
            enemySpeedMin = Mathf.Max(0f, enemySpeedMin);
            enemySpeedMax = Mathf.Max(enemySpeedMin, enemySpeedMax);
            enemyRadius = Mathf.Max(0.01f, enemyRadius);
            enemyPrewarmCount = Mathf.Max(0, enemyPrewarmCount);
            enemyMaxSpawnsPerFrame = Mathf.Max(1, enemyMaxSpawnsPerFrame);
            recycleDistance = Mathf.Max(1f, recycleDistance);
            recycleMargin = Mathf.Max(0f, recycleMargin);
            xpGemMaxCount = Mathf.Max(1, xpGemMaxCount);
            xpGemMagnetRadius = Mathf.Max(0.1f, xpGemMagnetRadius);
            xpGemPickupRadius = Mathf.Max(0.05f, xpGemPickupRadius);
            xpGemMagnetSpeed = Mathf.Max(1f, xpGemMagnetSpeed);
            maxWeaponSlots = Mathf.Max(1, maxWeaponSlots);
            maxPassiveSlots = Mathf.Max(1, maxPassiveSlots);
            baseProjectileSpeed = Mathf.Max(0.1f, baseProjectileSpeed);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            garlicBaseRadius = Mathf.Max(0.1f, garlicBaseRadius);
            enemyHealth = Mathf.Max(1f, enemyHealth);
            enemyXp = Mathf.Max(0, enemyXp);
            bossSpawnRadius = Mathf.Max(1f, bossSpawnRadius);
            bossHealthBase = Mathf.Max(1f, bossHealthBase);
            bossHealthPerSpawn = Mathf.Max(0f, bossHealthPerSpawn);
            bossSpeed = Mathf.Max(0.1f, bossSpeed);
            bossRadius = Mathf.Max(0.01f, bossRadius);
            bossVisualScale = Mathf.Max(1f, bossVisualScale);
            bossXp = Mathf.Max(0, bossXp);
            chestPickupRadius = Mathf.Max(0.1f, chestPickupRadius);

            if (bossSpawnTimes == null || bossSpawnTimes.Length == 0)
            {
                bossSpawnTimes = new[] { 60f, 150f, 270f };
            }

            if (bossChestRewards == null || bossChestRewards.Length == 0)
            {
                bossChestRewards = new[] { 1, 3, 5 };
            }
        }
    }
}
