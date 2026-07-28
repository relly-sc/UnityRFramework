using System.Collections.Generic;
using Game.Config;

/// <summary>
/// Demo 运行时游戏状态中枢（内存态，非持久化）。
/// 集中管理大厅所需的可变状态：货币、角色等级、选中项、已接任务。
/// UI 组件从此处读取状态，事件修改此处后由订阅方刷新界面。
/// 注意：Demo 不含存档系统，状态仅在本次运行会话内有效。
/// </summary>
public static class DemoGameState
{
    /// <summary>金币。</summary>
    public static int Gold = 1200;

    /// <summary>钻石。</summary>
    public static int Diamond = 30;

    /// <summary>已接取任务编号列表。</summary>
    public static readonly List<int> AcceptedQuests = new List<int>();

    /// <summary>当前选中角色编号（-1 表示未选）。</summary>
    public static int SelectedCharacterId = -1;

    /// <summary>当前选中任务编号（-1 表示未选）。</summary>
    public static int SelectedQuestId = -1;

    private static readonly Dictionary<int, int> levels = new Dictionary<int, int>();

    private static readonly Dictionary<int, int> experience = new Dictionary<int, int>();

    /// <summary>
    /// 初始化角色等级（默认 5 级，与大厅展示一致）。
    /// </summary>
    public static void ResetSession(IEnumerable<Demo_CharacterConfig> characters)
    {
        Gold = 1200;
        Diamond = 30;
        AcceptedQuests.Clear();
        SelectedCharacterId = -1;
        SelectedQuestId = -1;
        levels.Clear();
        experience.Clear();
        foreach (Demo_CharacterConfig c in characters)
        {
            levels[c.Id] = 5;
            experience[c.Id] = 0;
        }
    }

    /// <summary>
    /// 获取角色等级（未记录时返回 1）。
    /// </summary>
    public static int GetLevel(int characterId)
    {
        return levels.TryGetValue(characterId, out int lv) ? lv : 1;
    }

    /// <summary>
    /// 设置角色等级。
    /// </summary>
    public static void SetLevel(int characterId, int level)
    {
        levels[characterId] = level;
    }

    /// <summary>
    /// 获取角色累计经验。
    /// </summary>
    public static int GetExperience(int characterId)
    {
        return experience.TryGetValue(characterId, out int value) ? value : 0;
    }

    /// <summary>
    /// 获取角色当前等级对应的攻击力。
    /// </summary>
    public static int GetAttack(Demo_CharacterConfig character)
    {
        return character != null ? character.BaseAtk + GetLevel(character.Id) * 2 : 0;
    }

    /// <summary>
    /// 获取角色当前等级对应的防御力。
    /// </summary>
    public static int GetDefense(Demo_CharacterConfig character)
    {
        return character != null ? character.BaseDef + GetLevel(character.Id) : 0;
    }

    /// <summary>
    /// 获取角色当前等级对应的最大生命值。
    /// </summary>
    public static int GetMaxHp(Demo_CharacterConfig character)
    {
        return character != null ? character.BaseHp + GetLevel(character.Id) * 5 : 0;
    }

    /// <summary>
    /// 选择出征角色。
    /// </summary>
    public static void SelectCharacter(int characterId)
    {
        SelectedCharacterId = characterId;
    }

    /// <summary>
    /// 接取并选择任务。
    /// </summary>
    public static bool AcceptQuest(int questId)
    {
        bool added = !AcceptedQuests.Contains(questId);
        if (added)
        {
            AcceptedQuests.Add(questId);
        }

        SelectedQuestId = questId;
        return added;
    }

    /// <summary>
    /// 选择一个已经接取的任务。
    /// </summary>
    public static bool SelectQuest(int questId)
    {
        if (!AcceptedQuests.Contains(questId))
        {
            return false;
        }

        SelectedQuestId = questId;
        return true;
    }

    /// <summary>
    /// 尝试升级角色。升级费用按当前等级乘以配置基础费用计算。
    /// </summary>
    public static bool TryUpgradeCharacter(Demo_CharacterConfig character, out int newLevel)
    {
        newLevel = character != null ? GetLevel(character.Id) : 0;
        if (character == null)
        {
            return false;
        }

        int cost = character.UpgradeCost * newLevel;
        if (Gold < cost)
        {
            return false;
        }

        Gold -= cost;
        newLevel++;
        levels[character.Id] = newLevel;
        return true;
    }

    /// <summary>
    /// 应用远征结算。胜利时发放奖励并完成当前任务。
    /// </summary>
    public static void ApplyExpeditionResult(bool success, int experienceGained, int goldGained)
    {
        if (!success)
        {
            return;
        }

        Gold += goldGained;
        if (SelectedCharacterId >= 0)
        {
            experience[SelectedCharacterId] = GetExperience(SelectedCharacterId) + experienceGained;
        }

        AcceptedQuests.Remove(SelectedQuestId);
        SelectedQuestId = -1;
    }
}
