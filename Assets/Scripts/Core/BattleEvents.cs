using UnityEngine;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 玩法层发给表现层的事件（Core 层）。
    /// 全部是 readonly struct，Publish 时不产生堆分配。
    /// </summary>
    public readonly struct BattleStartedEvent
    {
        public readonly int TotalWaves;
        public readonly float DurationSeconds;

        public BattleStartedEvent(int totalWaves, float durationSeconds)
        {
            TotalWaves = totalWaves;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>切到某一波时发布；HUD 只关心序号和总数。</summary>
    public readonly struct WaveStartedEvent
    {
        public readonly int WaveIndex;
        public readonly int TotalWaves;
        public readonly string EnemyName;
        public readonly int MinimumCount;
        public readonly float SpawnInterval;

        public WaveStartedEvent(
            int waveIndex,
            int totalWaves,
            string enemyName,
            int minimumCount,
            float spawnInterval)
        {
            WaveIndex = waveIndex;
            TotalWaves = totalWaves;
            EnemyName = enemyName;
            MinimumCount = minimumCount;
            SpawnInterval = spawnInterval;
        }
    }

    /// <summary>局内剩余时间；BattleSession 每帧发布，HUD 内部按整秒去重。</summary>
    public readonly struct BattleTimeChangedEvent
    {
        public readonly float RemainingSeconds;

        public BattleTimeChangedEvent(float remainingSeconds)
        {
            RemainingSeconds = remainingSeconds;
        }
    }

    /// <summary>每击杀一只敌人发布一次；TotalKills 是本局累计值。</summary>
    public readonly struct EnemyKilledEvent
    {
        public readonly int TotalKills;
        public readonly Vector2 Position;
        public readonly int Xp;
        public readonly bool IsBoss;

        public EnemyKilledEvent(int totalKills, Vector2 position, int xp = 0, bool isBoss = false)
        {
            TotalKills = totalKills;
            Position = position;
            Xp = xp;
            IsBoss = isBoss;
        }
    }

    /// <summary>经验宝石被拾取；ExperienceSystem 订阅它累加经验。</summary>
    public readonly struct XpGemCollectedEvent
    {
        public readonly int Value;
        public readonly Vector2 Position;

        public XpGemCollectedEvent(int value, Vector2 position)
        {
            Value = value;
            Position = position;
        }
    }

    /// <summary>等级/经验变化；HUD 用它刷新经验条。</summary>
    public readonly struct ExperienceChangedEvent
    {
        public readonly int Level;
        public readonly int Xp;
        public readonly int XpToNext;
        public readonly int PendingLevelUps;

        public ExperienceChangedEvent(int level, int xp, int xpToNext, int pendingLevelUps)
        {
            Level = level;
            Xp = xp;
            XpToNext = xpToNext;
            PendingLevelUps = pendingLevelUps;
        }
    }

    /// <summary>升级三选一的一项展示信息（只读快照，不引用 Gameplay）。</summary>
    public readonly struct UpgradeOptionInfo
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly bool IsWeapon;
        public readonly Sprite Icon;

        public UpgradeOptionInfo(
            string id,
            string displayName,
            string description,
            int level,
            int maxLevel,
            bool isWeapon,
            Sprite icon)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Level = level;
            MaxLevel = maxLevel;
            IsWeapon = isWeapon;
            Icon = icon;
        }
    }

    /// <summary>升级时发布三/四个候选；LevelUpView 渲染后通过 LevelUpChoiceEvent 回传下标。</summary>
    public readonly struct LevelUpOfferedEvent
    {
        public readonly int Level;
        public readonly UpgradeOptionInfo[] Options;

        public LevelUpOfferedEvent(int level, UpgradeOptionInfo[] options)
        {
            Level = level;
            Options = options;
        }
    }

    /// <summary>UI 点击某张升级卡后发布。</summary>
    public readonly struct LevelUpChoiceEvent
    {
        public readonly int ChoiceIndex;

        public LevelUpChoiceEvent(int choiceIndex)
        {
            ChoiceIndex = choiceIndex;
        }
    }

    /// <summary>一次升级选择结束（可能紧接着下一次选择）。</summary>
    public readonly struct LevelUpClosedEvent
    {
    }

    /// <summary>一个武器/被动槽的展示快照。</summary>
    public readonly struct LoadoutSlotInfo
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Sprite Icon;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly bool IsWeapon;

        public LoadoutSlotInfo(
            string id,
            string displayName,
            Sprite icon,
            int level,
            int maxLevel,
            bool isWeapon)
        {
            Id = id;
            DisplayName = displayName;
            Icon = icon;
            Level = level;
            MaxLevel = maxLevel;
            IsWeapon = isWeapon;
        }
    }

    /// <summary>装备变化；HUD 刷新左上角 6+6 槽位。</summary>
    public readonly struct LoadoutChangedEvent
    {
        public readonly LoadoutSlotInfo[] Weapons;
        public readonly LoadoutSlotInfo[] Passives;

        public LoadoutChangedEvent(LoadoutSlotInfo[] weapons, LoadoutSlotInfo[] passives)
        {
            Weapons = weapons;
            Passives = passives;
        }
    }

    /// <summary>宝箱打开；Summary 是给 HUD toast 的一行奖励文本。</summary>
    public readonly struct ChestOpenedEvent
    {
        public readonly int RewardCount;
        public readonly string Summary;

        public ChestOpenedEvent(int rewardCount, string summary)
        {
            RewardCount = rewardCount;
            Summary = summary;
        }
    }

    /// <summary>宝箱蝙蝠出现；HUD 可提示玩家去击杀。</summary>
    public readonly struct BossSpawnedEvent
    {
        public readonly string DisplayName;
        public readonly int ChestRewards;

        public BossSpawnedEvent(string displayName, int chestRewards)
        {
            DisplayName = displayName;
            ChestRewards = chestRewards;
        }
    }

    /// <summary>时间到、清场后发布；HUD 用这两个值填写胜利面板。</summary>
    public readonly struct BattleVictoryEvent
    {
        public readonly float SurvivedSeconds;
        public readonly int Kills;

        public BattleVictoryEvent(float survivedSeconds, int kills)
        {
            SurvivedSeconds = survivedSeconds;
            Kills = kills;
        }
    }
}
