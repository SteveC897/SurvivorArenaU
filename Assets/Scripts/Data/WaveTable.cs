using System;
using System.Collections.Generic;
using System.Globalization;

namespace SurvivorArena.Data
{
    // =========================================================================
    // WaveTable.cs —— 波次表（CSV）的解析与开局前校验（Data 层）
    //
    // 【这个类是干什么的】
    //   把 Assets/DataDocs/level01_enemy_spawn.csv 读成一组 WaveDefinition，
    //   并在开局前检查数据是否合法。它只属于 Data 层，不负责刷怪，也不负责计时：
    //   刷怪是 Gameplay.EnemySpawner 的事，切波是 Gameplay.WaveScheduler/BattleSession 的事。
    //
    // 【数据流】
    //   Assets/DataDocs/level01_enemy_spawn.csv
    //        │  Unity 把 .csv 当作 TextAsset 导入；BattleConfig 持有它的引用
    //        ▼
    //   WaveTable.Parse(csv.text)          ← Bootstrap.BattleBootstrap 在 Start 里调用
    //        │  逐行解析成 WaveDefinition
    //        ▼
    //   WaveScheduler.Advance(elapsed)     ← BattleSession 每帧驱动
    //        │  到点后触发 WaveTriggered
    //        ▼
    //   EnemySpawner.SetWave(wave)
    //        └─ 只用 wave.MinimumCount / wave.SpawnInterval 刷怪；
    //           wave.Index 通过事件给 HUD 显示“波次 x/N”，wave.EnemyName 只用于日志。
    //
    // 【CSV 约定】（具体数据见 DataDocs/level01_enemy_spawn.csv 顶部的注释）
    //   - 空行、# 开头的行是注释，解析时跳过；
    //   - 第一条非注释行是表头；
    //   - 字段用逗号分隔；本表字段内不含逗号/引号，所以用最简单的 Split(',')；
    //   - 列顺序随意，程序按“列名”找列，不按位置；
    //   - 必需列：wave, time_sec, enemy_id, minimum_count, spawn_interval；
    //   - 可选列：enemy_name（缺省用 enemy_id）、source_minute（缺省空串）；
    //   - time_label 只是给人看的时间文本，程序不使用，也不要求它存在；
    //   - 报错会带 CSV 的原始行号（从 1 开始），方便直接定位。
    //
    // 【校验分两层】
    //   1. Parse()：单行/跨行的静态合法性（数字格式、正数、严格递增、必需列）；
    //   2. ValidateForRun()：和“本局时长”相关的边界（最后一波必须在结束前开始）。
    //   为什么分开：同一张表可能被不同时长的局复用，Parse 不应该知道本局跑多久。
    //
    // 【为什么错误直接抛 FormatException】
    //   Bootstrap.BattleBootstrap 会捕获（catch）并 Debug.LogError，然后不启动战斗。
    //   这样坏数据会立刻在 Console 里带行号暴露，而不是静默变成 0 波、开局空跑。
    //
    // 【建议阅读顺序】
    //   1. Parse()             —— 主流程
    //   2. 文件末尾的五个小工具 —— RequireColumn / SplitCsvLine / ReadField / ParseInt / ParseFloat
    //   3. ValidateForRun()    —— 与本局时长的二次校验
    //   4. 上面的属性          —— Count / this[] / Summary
    // =========================================================================
    /// <summary>
    /// 一份解析完成、且已通过单行静态校验的波次表。
    /// 构造函数私有：外部只能通过 <see cref="Parse"/> 创建实例，
    /// 从源头上保证拿到的 WaveTable 不会是“半解析”的状态。
    /// </summary>
    public sealed class WaveTable
    {
        // ---------------------------------------------------------------------
        // CSV 列名
        // 集中成常量，避免解析代码、报错文案、可选列判断里散落魔法字符串。
        // 这里的值与 CSV 表头一一对应。
        // ---------------------------------------------------------------------
        private const string ColumnWave = "wave";                  // 波次序号（1、2、3……，必须严格递增）
        private const string ColumnTimeSec = "time_sec";           // 本局开波时间（秒，必须严格递增且非负）
        private const string ColumnEnemyId = "enemy_id";           // 敌人标识（本测试场景固定为 bat）
        private const string ColumnEnemyName = "enemy_name";       // 敌人显示名（可选；缺省用 enemy_id）
        private const string ColumnMinimum = "minimum_count";      // 场上数量下限（低于它才补刷）
        private const string ColumnInterval = "spawn_interval";    // 补刷间隔（秒，必须大于 0）
        private const string ColumnSourceMinute = "source_minute"; // 源表行时间（可选；仅用于追溯，不参与逻辑）

        // ---------------------------------------------------------------------
        // 解析结果
        // ---------------------------------------------------------------------
        // 元素按 time_sec 从早到晚排列，且严格递增（Parse 保证）。
        // 用 List 而不是数组：解析前不知道有多少行，逐行 Add 最自然。
        // readonly 只保护字段引用，不保护 List 内容；不过本类没有暴露任何修改方法，
        // 所以离开 Parse 之后，这张表逻辑上就是只读的。
        private readonly List<WaveDefinition> _waves;

        // 私有构造：调用方必须先走完 Parse 的所有校验。
        // 这里直接接管传入的 List，不做防御性拷贝——Parse 返回后不会再持有它。
        private WaveTable(List<WaveDefinition> waves)
        {
            _waves = waves;
        }

        /// <summary>
        /// 波次总数。HUD 里“波次 x/N”的 N 就来自这里，
        /// 也是 WaveScheduler 扫描的实际上限。
        /// </summary>
        public int Count => _waves.Count;

        /// <summary>
        /// 按索引取波：0 是时间最早的一波，Count - 1 是最晚一波。
        /// 越界会由 List 抛 ArgumentOutOfRangeException。
        /// WaveScheduler 实际是按时间顺序扫描，不依赖这个索引器；
        /// 它主要留给调试、测试或以后做“波次预览”时使用。
        /// </summary>
        public WaveDefinition this[int index] => _waves[index];

        /// <summary>
        /// 启动日志用的一行摘要，例如“30 波，0s – 290s”。
        /// Count 为 0 的情况理论上不会出现（Parse 会拒绝），这里只是兜底；
        /// “0.##”表示最多保留两位小数，并去掉末尾多余的 0。
        /// </summary>
        public string Summary =>
            Count == 0
                ? "空波次表"
                : $"{Count} 波，{_waves[0].Time:0.##}s – {_waves[Count - 1].Time:0.##}s";

        /// <summary>
        /// 解析 CSV 文本。空行与 # 开头的行会被忽略。
        /// </summary>
        /// <param name="csv">完整的 CSV 文本，通常来自 TextAsset.text。</param>
        /// <returns>解析并校验通过的波次表。</returns>
        /// <exception cref="FormatException">
        /// CSV 为空、只有注释没有表头、缺必需列、列数不足、数字格式错误、
        /// wave/time_sec 非严格递增、minimum_count &lt; 1、spawn_interval &lt;= 0 等。
        /// </exception>
        public static WaveTable Parse(string csv)
        {
            // 0) 空文本/全空白直接拒绝。
            //    后面找表头会失败，但在这里给出更明确的错误信息。
            if (string.IsNullOrWhiteSpace(csv))
            {
                throw new FormatException("波次表为空。");
            }

            // 1) 统一换行符。
            //    Windows 记事本是 \r\n，老 Mac 是 \r，Unix/Unity 文本资源通常是 \n。
            //    先全部规范成 \n，后面只按 \n 切分即可。
            string[] lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            // 2) 找表头：跳过空行和 # 注释行，第一条真正的内容行就是表头。
            //    headerLineIndex 记录它在 lines 里的下标（0-based），
            //    数据行从 headerLineIndex + 1 开始读。
            int headerLineIndex = -1;
            string[] header = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    // 空行或注释：继续往下找。
                    continue;
                }

                header = SplitCsvLine(line);
                headerLineIndex = i;
                break;
            }

            // header 仍为 null：整个文件只有空行/注释，没有真正的表头。
            if (header == null)
            {
                throw new FormatException("波次表只有注释，没有表头。");
            }

            // 3) 表头 → “列名 → 列下标”的映射。
            //    - OrdinalIgnoreCase：列名大小写不敏感（wave / Wave / WAVE 都认）。
            //    - TrimStart('\uFEFF')：有些编辑器/Excel 会在 UTF-8 文件最前面写 BOM，
            //      BOM 会粘在第一个列名上；不去掉的话，看起来是 "wave"，
            //      实际字符串是 "\uFEFFwave"，列名就对不上了。
            //    - 空列名跳过；同名重复列时后出现的覆盖前面的（本表没有重复列）。
            var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                string column = header[i].Trim().TrimStart('\uFEFF').Trim();
                if (column.Length > 0)
                {
                    columnIndex[column] = i;
                }
            }

            // 4) 必需列必须全部存在。
            //    RequireColumn 找不到时会直接抛 FormatException，
            //    所以下面拿到的每一个下标都一定是有效的。
            int waveColumn = RequireColumn(columnIndex, ColumnWave);
            int timeColumn = RequireColumn(columnIndex, ColumnTimeSec);
            int enemyIdColumn = RequireColumn(columnIndex, ColumnEnemyId);
            int minimumColumn = RequireColumn(columnIndex, ColumnMinimum);
            int intervalColumn = RequireColumn(columnIndex, ColumnInterval);

            // 5) 可选列：用 -1 表示“表头里没有这一列”。
            //    不能直接用 TryGetValue 的 out 默认值 0 来判断“有没有”，
            //    因为 0 是合法列下标（第一列），会把“没有”误判成“第 0 列”。
            int enemyNameColumn = columnIndex.TryGetValue(ColumnEnemyName, out int nameIndex) ? nameIndex : -1;
            int sourceMinuteColumn = columnIndex.TryGetValue(ColumnSourceMinute, out int sourceIndex) ? sourceIndex : -1;

            // 6) 逐行解析数据行。
            //    previousWave / previousTime 保存上一行的值，用来检查严格递增：
            //      - previousWave 从 0 开始，因为 wave 必须 >= 1，第一行天然能通过；
            //      - previousTime 从负无穷开始，这样第一行不需要特判。
            var waves = new List<WaveDefinition>();
            int previousWave = 0;
            float previousTime = float.NegativeInfinity;

            for (int i = headerLineIndex + 1; i < lines.Length; i++)
            {
                // 跳过空行和注释行。
                // lineNumber 用 1-based，和文本编辑器/Excel 里看到的行号一致。
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int lineNumber = i + 1;
                string[] fields = SplitCsvLine(line);

                // 列数少于表头：常见原因是少写了逗号，或中间某列忘了占位。
                // 多出来的列不报错，方便以后在 CSV 末尾追加新列。
                if (fields.Length < header.Length)
                {
                    throw new FormatException(
                        $"波次表第 {lineNumber} 行：列数为 {fields.Length}，少于表头的 {header.Length} 列。");
                }

                // 按表头映射出的下标取值。
                // enemy_name / source_minute 是可选的：表头里没有这一列时走缺省逻辑。
                int wave = ParseInt(ReadField(fields, waveColumn), ColumnWave, lineNumber);
                float time = ParseFloat(ReadField(fields, timeColumn), ColumnTimeSec, lineNumber);
                int minimum = ParseInt(ReadField(fields, minimumColumn), ColumnMinimum, lineNumber);
                float interval = ParseFloat(ReadField(fields, intervalColumn), ColumnInterval, lineNumber);
                string enemyId = ReadField(fields, enemyIdColumn);
                string enemyName = enemyNameColumn >= 0 ? ReadField(fields, enemyNameColumn) : enemyId;
                string sourceMinute = sourceMinuteColumn >= 0 ? ReadField(fields, sourceMinuteColumn) : string.Empty;

                // 7) 单行校验。顺序是：波次 → 时间 → 数量下限 → 补刷间隔 → 敌人标识。
                //    每条错误都带 CSV 行号和列名，方便直接改文件。

                // wave 从 1 开始，0/负数不合法。
                if (wave <= 0)
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnWave} 必须大于 0。");
                }

                // 必须严格递增：允许 1,2,3，也允许 1,3,5；不允许重复或倒序。
                // 这样 WaveScheduler 按时间顺序扫描时，后一波一定在更晚的位置。
                if (wave <= previousWave)
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnWave} 必须严格递增。");
                }

                // time_sec 必须是非负有限数。
                // NaN / Infinity 会让后面的时间比较和 elapsed >= wave.Time 失效。
                if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f)
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnTimeSec} 必须是非负有限数。");
                }

                // 时间也必须严格递增。
                // 两个波次同一时间会导致同一帧连续切两波（后一波会覆盖前一波的刷怪参数）。
                if (time <= previousTime)
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnTimeSec} 必须严格递增。");
                }

                // 数量下限至少为 1；0 会让 EnemySpawner 永远不刷怪。
                if (minimum < 1)
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnMinimum} 必须至少为 1。");
                }

                // 补刷间隔必须大于 0 且有限。
                // 0 会让补刷逻辑出现除零/死循环，负数没有意义。
                if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0f)
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnInterval} 必须大于 0。");
                }

                // 敌人标识不能为空。
                // 本测试场景虽然只用蝙蝠，仍保留这个字段做通用性。
                if (string.IsNullOrEmpty(enemyId))
                {
                    throw new FormatException($"波次表第 {lineNumber} 行：{ColumnEnemyId} 不能为空。");
                }

                // enemy_name 是可选的：没填就退回 enemy_id，
                // 保证日志/调试输出里始终有一个可读的名字。
                if (string.IsNullOrEmpty(enemyName))
                {
                    enemyName = enemyId;
                }

                // 构造 WaveDefinition（定义在同目录的 WaveDefinition.cs）。
                // 参数顺序：序号、开波时间、数量下限、补刷间隔、敌人 id、显示名、源表行。
                waves.Add(new WaveDefinition(
                    wave,
                    time,
                    minimum,
                    interval,
                    enemyId,
                    enemyName,
                    sourceMinute));

                // 记录这一行，供下一行做严格递增检查。
                previousWave = wave;
                previousTime = time;
            }

            // 8) 只有表头、没有任何数据行（或全是注释），也算坏表。
            if (waves.Count == 0)
            {
                throw new FormatException("波次表没有有效数据行。");
            }

            // 解析完成。返回后 _waves 不再被修改（类里没有 Add/Remove 接口）。
            return new WaveTable(waves);
        }

        /// <summary>
        /// 二次校验：把波次表和“本局时长”对一下。
        /// Parse 只保证表本身合法，并不知道这一局要跑多久；
        /// 同一张表可能被不同时长的局复用，所以把这部分放在单独的方法里。
        /// </summary>
        /// <param name="runDurationSeconds">本局时长（秒），测试场景为 300。</param>
        /// <exception cref="FormatException">时长非法，或最后一波不在结束前开始。</exception>
        public void ValidateForRun(float runDurationSeconds)
        {
            // 注意：这里不检查“必须 30 波”或“必须每 10 秒一波”——
            // 那是本测试场景 CSV 的内容约定；WaveTable 只负责解析和边界校验。
            // 波数、节奏由 CSV 数据本身决定，HUD 的总波数用 Count。

            // 时长必须为正，否则后面的比较没有意义。
            if (runDurationSeconds <= 0f)
            {
                throw new FormatException("单局时长必须大于 0。");
            }

            // 空表在 Parse 里已经拦过；这里保留是防御性检查
            // （比如以后允许通过别的途径构造 WaveTable）。
            if (Count == 0)
            {
                throw new FormatException("波次表没有有效数据行。");
            }

            // 最后一波必须严格早于结束时间。
            // last.Time == duration 时，这一波刚开波就到点清场，等于没刷，
            // 属于典型的配置错误（例如把 300s 的局配了 300s 开波）。
            WaveDefinition last = _waves[Count - 1];
            if (last.Time >= runDurationSeconds)
            {
                throw new FormatException(
                    $"最后一波（第 {last.Index} 波）开始于 {last.Time:0.##}s，晚于单局时长 {runDurationSeconds:0.##}s。");
            }

            // 第一波理论上已经被上面的“最后一波”条件覆盖（时间严格递增），
            // 这里保留一个防御性检查，也方便以后调整校验顺序。
            if (_waves[0].Time > runDurationSeconds)
            {
                throw new FormatException(
                    $"第一波开始于 {_waves[0].Time:0.##}s，晚于单局时长 {runDurationSeconds:0.##}s。");
            }
        }

        // =====================================================================
        // 解析小工具
        // =====================================================================

        /// <summary>
        /// 从“列名 → 下标”的表头映射里取出必需列的下标。
        /// 找不到时抛出带列名的 FormatException。
        /// </summary>
        private static int RequireColumn(Dictionary<string, int> columns, string name)
        {
            // 注意：TryGetValue 找不到时会把 index 设为 0，
            // 但 0 也是合法下标，所以必须看返回值，不能靠 index 判断“有没有”。
            if (!columns.TryGetValue(name, out int index))
            {
                throw new FormatException($"波次表缺少必需列“{name}”。");
            }

            return index;
        }

        /// <summary>
        /// 把一行按逗号切成字段，并去掉每个字段两端的空白。
        /// 本表字段不包含逗号/引号，所以不需要完整的 RFC 4180 CSV 解析器；
        /// 如果以后要支持“字段里带逗号”，应换成正规的 CSV 解析。
        /// </summary>
        private static string[] SplitCsvLine(string line)
        {
            // 最简单的逗号切分。字段内部的逗号/引号不支持，这一点在文件顶部有说明。
            string[] fields = line.Split(',');

            // 逐字段 Trim，避免 "1, 0:00, bat" 里的空格影响数字解析和列名匹配。
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = fields[i].Trim();
            }

            return fields;
        }

        /// <summary>
        /// 安全读取字段：下标越界时返回空串。
        /// 必需列在调用前已经通过“列数不少于表头”的检查；
        /// 这个保护主要给可选列/未来行尾缺列的情况用。
        /// </summary>
        private static string ReadField(string[] fields, int index)
        {
            return index >= 0 && index < fields.Length ? fields[index] : string.Empty;
        }

        /// <summary>
        /// 解析整数；失败时抛出带行号和列名的 FormatException。
        /// 使用 InvariantCulture：避免在系统区域设置为“逗号小数点”时
        /// 出现意料之外的解析行为，也保证 CSV 的写法与机器区域设置无关。
        /// </summary>
        private static int ParseInt(string value, string column, int lineNumber)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                throw new FormatException($"波次表第 {lineNumber} 行：{column} 不是整数（\"{value}\"）。");
            }

            return result;
        }

        /// <summary>
        /// 解析浮点数；失败时抛出带行号和列名的 FormatException。
        /// NumberStyles.Float 支持小数、正负号和科学计数法；
        /// InvariantCulture 保证 "0.5" 在任何系统区域设置下都按小数点解析。
        /// 注意：TryParse 可能成功解析出 NaN/Infinity，
        /// 有限性由 Parse 主流程在 time/interval 校验里检查。
        /// </summary>
        private static float ParseFloat(string value, string column, int lineNumber)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                throw new FormatException($"波次表第 {lineNumber} 行：{column} 不是数字（\"{value}\"）。");
            }

            return result;
        }
    }
}
