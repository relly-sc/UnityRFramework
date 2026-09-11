using System;
using System.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine;

namespace UnityRFramework.Sample
{
    /// <summary>
    /// Demo 回合战斗控制器。持有纯运行时战斗数据，并通过同步 FSM 编排回合。
    /// </summary>
    public sealed class DemoBattleController : IDisposable
    {
        private readonly DemoExpeditionWindow view;
        private readonly Demo_CharacterConfig character;
        private readonly Demo_QuestConfig quest;
        private readonly Demo_EnemyConfig enemy;
        private IFsm battleFsm;
        private RFramework.Timer enemyTimer;
        private bool disposed;
        private bool resultApplied;
        private bool defending;
        private int skillCooldown;

        /// <summary>获取玩家最大生命值。</summary>
        public int PlayerMaxHp { get; }

        /// <summary>获取玩家当前生命值。</summary>
        public int PlayerHp { get; private set; }

        /// <summary>获取敌人最大生命值。</summary>
        public int EnemyMaxHp { get; }

        /// <summary>获取敌人当前生命值。</summary>
        public int EnemyHp { get; private set; }

        /// <summary>获取当前战斗回合数。</summary>
        public int Round { get; private set; } = 1;

        /// <summary>获取玩家名称的本地化键。</summary>
        public string PlayerNameKey => character.NameKey;

        /// <summary>获取敌人名称的本地化键。</summary>
        public string EnemyNameKey => enemy.NameKey;

        /// <summary>获取玩家当前等级。</summary>
        public int PlayerLevel => DemoGameState.GetLevel(character.Id);

        /// <summary>获取玩家当前攻击力。</summary>
        public int PlayerAttack => DemoGameState.GetAttack(character);

        /// <summary>获取玩家当前防御力。</summary>
        public int PlayerDefense => DemoGameState.GetDefense(character);

        /// <summary>获取敌人当前攻击力。</summary>
        public int EnemyAttack => enemy.BaseAtk + Round;

        /// <summary>获取敌人当前防御力。</summary>
        public int EnemyDefense => enemy.BaseDef;

        /// <summary>获取当前是否为玩家行动阶段。</summary>
        public bool IsPlayerTurn => battleFsm != null && battleFsm.CurrentStateType == typeof(DemoPlayerTurnState);

        /// <summary>获取玩家技能是否已结束冷却。</summary>
        public bool SkillAvailable => skillCooldown <= 0;

        /// <summary>
        /// 创建回合战斗控制器并读取当前选中的角色、任务和敌人配置。
        /// </summary>
        /// <param name="view">接收战斗状态刷新的远征窗口。</param>
        public DemoBattleController(DemoExpeditionWindow view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            character = GameEntry.Config.GetConfig<Demo_CharacterConfig>(DemoGameState.SelectedCharacterId)
                ?? throw new RFrameworkException("Demo battle character is not selected or configured.");
            quest = GameEntry.Config.GetConfig<Demo_QuestConfig>(DemoGameState.SelectedQuestId)
                ?? throw new RFrameworkException("Demo battle quest is not selected or configured.");
            enemy = GameEntry.Config.GetConfig<Demo_EnemyConfig>(quest.EnemyId)
                ?? throw new RFrameworkException($"Demo battle enemy '{quest.EnemyId}' is not configured.");

            PlayerMaxHp = DemoGameState.GetMaxHp(character);
            PlayerHp = PlayerMaxHp;
            EnemyMaxHp = enemy.BaseHp;
            EnemyHp = EnemyMaxHp;
        }

        /// <summary>
        /// 创建战斗 FSM 并进入玩家回合。
        /// </summary>
        public void Start()
        {
            battleFsm = GameEntry.Fsm.CreateFsm(this,
                new DemoPlayerTurnState(this),
                new DemoEnemyTurnState(this),
                new DemoBattleResultState(this));
            battleFsm.Start<DemoPlayerTurnState>();
        }

        /// <summary>
        /// 在玩家回合执行指定配置的战斗动作。
        /// </summary>
        /// <param name="actionId">战斗动作配置 ID。</param>
        public void UseAction(int actionId)
        {
            if (disposed || !IsPlayerTurn)
            {
                return;
            }

            Demo_ActionConfig action = GameEntry.Config.GetConfig<Demo_ActionConfig>(actionId);
            if (action == null)
            {
                view.AppendLog("Action config missing: " + actionId);
                return;
            }

            defending = false;
            if (string.Equals(action.Type, "defend", StringComparison.OrdinalIgnoreCase))
            {
                defending = true;
                view.AppendLog(GameEntry.Localization.GetString("UI_LogDefend"));
                PlaySfx("Audio/Audio_DEF.mp3");
                battleFsm.ChangeState<DemoEnemyTurnState>();
                return;
            }

            if (string.Equals(action.Type, "skill", StringComparison.OrdinalIgnoreCase))
            {
                if (!SkillAvailable)
                {
                    view.AppendLog(GameEntry.Localization.GetString("UI_SkillCooling"));
                    return;
                }

                skillCooldown = Mathf.Max(1, action.Cooldown + 1);
            }

            int damage = Mathf.Max(1, Mathf.RoundToInt(PlayerAttack * action.Power) - EnemyDefense);
            EnemyHp = Mathf.Max(0, EnemyHp - damage);
            view.AppendLog(string.Format(GameEntry.Localization.GetString("UI_LogPlayerHit"), damage));
            PlaySfx(string.Equals(action.Type, "skill", StringComparison.OrdinalIgnoreCase)
                ? "Audio/Audio_Skill.mp3"
                : "Audio/Audio_ATK.mp3");
            view.RefreshBattle(this);

            if (EnemyHp <= 0)
            {
                battleFsm.ChangeState<DemoBattleResultState>();
            }
            else
            {
                battleFsm.ChangeState<DemoEnemyTurnState>();
            }
        }

        /// <summary>
        /// 进入玩家回合并刷新技能冷却与界面。
        /// </summary>
        internal void BeginPlayerTurn()
        {
            skillCooldown = Mathf.Max(0, skillCooldown - 1);
            view.SetPhase(GameEntry.Localization.GetString("UI_PlayerTurn"));
            view.RefreshBattle(this);
        }

        /// <summary>
        /// 进入敌人回合并启动延迟结算计时器。
        /// </summary>
        internal void BeginEnemyTurn()
        {
            view.SetPhase(GameEntry.Localization.GetString("UI_EnemyTurn"));
            view.RefreshBattle(this);
            CancelEnemyTimer();
            enemyTimer = RFramework.Timer.CreateOnce(0.65f, ResolveEnemyTurn);
            GameEntry.Timer.RegisterTimer(enemyTimer);
        }

        /// <summary>
        /// 离开敌人回合并取消尚未执行的计时器。
        /// </summary>
        internal void EndEnemyTurn()
        {
            CancelEnemyTimer();
        }

        /// <summary>
        /// 结算战斗结果、奖励和业务事件。
        /// </summary>
        internal void CompleteBattle()
        {
            if (resultApplied)
            {
                return;
            }

            resultApplied = true;
            bool success = EnemyHp <= 0 && PlayerHp > 0;
            int exp = success ? quest.RewardExp : 0;
            int gold = success ? quest.RewardGold : 0;
            DemoGameState.ApplyExpeditionResult(success, exp, gold);
            GameEntry.Event.Fire(new QuestCompletedEvent(quest.Id, success));
            GameEntry.Event.Fire(new ExpeditionEndedEvent(success, exp, gold, quest.Id));
            GameEntry.Event.Fire(new DemoStateChangedEvent());
            view.ShowResult(success, exp, gold);
            PlaySfx("Audio/Audio_Skill.mp3");
        }

        private void ResolveEnemyTurn()
        {
            enemyTimer = null;
            if (disposed || battleFsm == null || battleFsm.CurrentStateType != typeof(DemoEnemyTurnState))
            {
                return;
            }

            int defence = PlayerDefense;
            if (defending)
            {
                defence *= 2;
            }

            int damage = Mathf.Max(1, EnemyAttack - defence);
            PlayerHp = Mathf.Max(0, PlayerHp - damage);
            view.AppendLog(string.Format(GameEntry.Localization.GetString("UI_LogEnemyHit"), damage));
            PlaySfx("Audio/Audio_ATK.mp3");
            view.RefreshBattle(this);

            if (PlayerHp <= 0)
            {
                battleFsm.ChangeState<DemoBattleResultState>();
                return;
            }

            Round++;
            battleFsm.ChangeState<DemoPlayerTurnState>();
        }

        private static void PlaySfx(string assetName)
        {
            _ = PlaySfxAsync(assetName);
        }

        private static async Task PlaySfxAsync(string assetName)
        {
            try
            {
                IAudioModule audio = RFrameworkModuleHost.Get<IAudioModule>();
                if (audio != null)
                {
                    await audio.PlaySfxAsync(assetName, 0.65f);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[Demo] Optional audio '{0}' failed: {1}", assetName, ex.Message);
            }
        }

        private void CancelEnemyTimer()
        {
            if (enemyTimer == null)
            {
                return;
            }

            enemyTimer.Cancel();
            enemyTimer = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            CancelEnemyTimer();
            if (battleFsm != null)
            {
                GameEntry.Fsm.DestroyFsm(battleFsm);
                battleFsm = null;
            }
        }
    }

    /// <summary>
    /// Demo 战斗 FSM 的玩家回合状态。
    /// </summary>
    internal sealed class DemoPlayerTurnState : FsmStateBase
    {
        private readonly DemoBattleController owner;

        /// <summary>
        /// 创建玩家回合状态。
        /// </summary>
        /// <param name="owner">战斗控制器。</param>
        public DemoPlayerTurnState(DemoBattleController owner)
        {
            this.owner = owner;
        }

        /// <inheritdoc />
        public override void OnEnter()
        {
            owner.BeginPlayerTurn();
        }
    }

    /// <summary>
    /// Demo 战斗 FSM 的敌人回合状态。
    /// </summary>
    internal sealed class DemoEnemyTurnState : FsmStateBase
    {
        private readonly DemoBattleController owner;

        /// <summary>
        /// 创建敌人回合状态。
        /// </summary>
        /// <param name="owner">战斗控制器。</param>
        public DemoEnemyTurnState(DemoBattleController owner)
        {
            this.owner = owner;
        }

        /// <inheritdoc />
        public override void OnEnter()
        {
            owner.BeginEnemyTurn();
        }

        /// <inheritdoc />
        public override void OnLeave(bool isShutdown)
        {
            owner.EndEnemyTurn();
        }
    }

    /// <summary>
    /// Demo 战斗 FSM 的结果结算状态。
    /// </summary>
    internal sealed class DemoBattleResultState : FsmStateBase
    {
        private readonly DemoBattleController owner;

        /// <summary>
        /// 创建战斗结果状态。
        /// </summary>
        /// <param name="owner">战斗控制器。</param>
        public DemoBattleResultState(DemoBattleController owner)
        {
            this.owner = owner;
        }

        /// <inheritdoc />
        public override void OnEnter()
        {
            owner.CompleteBattle();
        }
    }
}
