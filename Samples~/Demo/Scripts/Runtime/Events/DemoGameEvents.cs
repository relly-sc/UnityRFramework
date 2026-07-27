/// <summary>
/// Demo 模块全部事件消息定义。
/// 统一使用框架类型化事件（GameEntry.Event.Fire / Subscribe），零 GC、编译期类型安全。
/// 各事件在对应 UI 交互触发时发布，由订阅方刷新界面。
/// </summary>

/// <summary>
/// 任务接取事件。玩家在大厅点击"接取任务"时发布。
/// </summary>
public class QuestAcceptedEvent
{
    /// <summary>
    /// 被接取的任务编号。
    /// </summary>
    public int QuestId;

    /// <summary>
    /// 初始化任务接取事件。
    /// </summary>
    /// <param name="questId">任务编号。</param>
    public QuestAcceptedEvent(int questId)
    {
        QuestId = questId;
    }
}

/// <summary>
/// 任务完成事件。远征结算完成时发布。
/// </summary>
public class QuestCompletedEvent
{
    /// <summary>
    /// 完成的任务编号。
    /// </summary>
    public int QuestId;

    /// <summary>
    /// 是否成功完成。
    /// </summary>
    public bool Success;

    /// <summary>
    /// 初始化任务完成事件。
    /// </summary>
    /// <param name="questId">任务编号。</param>
    /// <param name="success">是否成功。</param>
    public QuestCompletedEvent(int questId, bool success)
    {
        QuestId = questId;
        Success = success;
    }
}

/// <summary>
/// 角色升级事件。玩家点击升级按钮时发布。
/// </summary>
public class CharacterUpgradedEvent
{
    /// <summary>
    /// 升级的角色编号。
    /// </summary>
    public int CharacterId;

    /// <summary>
    /// 升级后的等级。
    /// </summary>
    public int NewLevel;

    /// <summary>
    /// 初始化角色升级事件。
    /// </summary>
    /// <param name="characterId">角色编号。</param>
    /// <param name="newLevel">新等级。</param>
    public CharacterUpgradedEvent(int characterId, int newLevel)
    {
        CharacterId = characterId;
        NewLevel = newLevel;
    }
}

/// <summary>
/// 大厅可变状态发生变化。顶栏和出征面板用它执行轻量刷新。
/// </summary>
public sealed class DemoStateChangedEvent
{
}

/// <summary>
/// 远征开始事件。确认出征时发布。
/// </summary>
public class ExpeditionStartedEvent
{
    /// <summary>
    /// 出征对应的任务编号。
    /// </summary>
    public int QuestId;

    /// <summary>
    /// 初始化远征开始事件。
    /// </summary>
    /// <param name="questId">任务编号。</param>
    public ExpeditionStartedEvent(int questId)
    {
        QuestId = questId;
    }
}

/// <summary>
/// 远征结束事件。远征场景切回大厅时发布。
/// </summary>
public class ExpeditionEndedEvent
{
    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Success;

    /// <summary>
    /// 获得经验。
    /// </summary>
    public int ExpGained;

    /// <summary>
    /// 获得金币。
    /// </summary>
    public int GoldGained;

    /// <summary>
    /// 本次远征对应的任务编号。
    /// </summary>
    public int QuestId;

    /// <summary>
    /// 初始化远征结束事件。
    /// </summary>
    /// <param name="success">是否成功。</param>
    /// <param name="expGained">获得经验。</param>
    /// <param name="goldGained">获得金币。</param>
    public ExpeditionEndedEvent(bool success, int expGained, int goldGained, int questId = -1)
    {
        Success = success;
        ExpGained = expGained;
        GoldGained = goldGained;
        QuestId = questId;
    }
}

/// <summary>
/// 语言切换完成事件。大厅各面板订阅该事件并刷新已创建的本地化文本。
/// </summary>
public class DemoLanguageChangedEvent
{
    /// <summary>
    /// 已切换到的语言代码。
    /// </summary>
    public string Language;

    /// <summary>
    /// 初始化语言切换事件。
    /// </summary>
    /// <param name="language">已切换到的语言代码。</param>
    public DemoLanguageChangedEvent(string language)
    {
        Language = language;
    }
}
