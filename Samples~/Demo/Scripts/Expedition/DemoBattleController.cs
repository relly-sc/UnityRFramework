using System;
using System.Threading.Tasks;
using Game.Config;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine;

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

    public int PlayerMaxHp { get; }
    public int PlayerHp { get; private set; }
    public int EnemyMaxHp { get; }
    public int EnemyHp { get; private set; }
    public int Round { get; private set; } = 1;
    public string PlayerNameKey => character.NameKey;
    public string EnemyNameKey => enemy.NameKey;
    public int PlayerLevel => DemoGameState.GetLevel(character.Id);
    public int PlayerAttack => DemoGameState.GetAttack(character);
    public int PlayerDefense => DemoGameState.GetDefense(character);
    public int EnemyAttack => enemy.BaseAtk + Round;
    public int EnemyDefense => enemy.BaseDef;
    public bool IsPlayerTurn => battleFsm != null && battleFsm.CurrentStateType == typeof(DemoPlayerTurnState);
    public bool SkillAvailable => skillCooldown <= 0;

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

    public void Start()
    {
        battleFsm = GameEntry.Fsm.CreateFsm(this,
            new DemoPlayerTurnState(this),
            new DemoEnemyTurnState(this),
            new DemoBattleResultState(this));
        battleFsm.Start<DemoPlayerTurnState>();
    }

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
            PlaySfx("Audio/sound_weapon_player.wav");
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
        PlaySfx("Audio/sound_weapon_player.wav");
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

    internal void BeginPlayerTurn()
    {
        skillCooldown = Mathf.Max(0, skillCooldown - 1);
        view.SetPhase(GameEntry.Localization.GetString("UI_PlayerTurn"));
        view.RefreshBattle(this);
    }

    internal void BeginEnemyTurn()
    {
        view.SetPhase(GameEntry.Localization.GetString("UI_EnemyTurn"));
        view.RefreshBattle(this);
        CancelEnemyTimer();
        enemyTimer = RFramework.Timer.CreateOnce(0.65f, ResolveEnemyTurn);
        GameEntry.Timer.RegisterTimer(enemyTimer);
    }

    internal void EndEnemyTurn()
    {
        CancelEnemyTimer();
    }

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
        PlaySfx(success
            ? "Audio/sound_explosion_enemy.wav"
            : "Audio/sound_explosion_player.wav");
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
        PlaySfx("Audio/sound_weapon_enemy.wav");
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
            IAudioModule audio = RFrameworkModuleEntry.GetModule<IAudioModule>();
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

internal sealed class DemoPlayerTurnState : FsmStateBase
{
    private readonly DemoBattleController owner;

    public DemoPlayerTurnState(DemoBattleController owner)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        owner.BeginPlayerTurn();
    }
}

internal sealed class DemoEnemyTurnState : FsmStateBase
{
    private readonly DemoBattleController owner;

    public DemoEnemyTurnState(DemoBattleController owner)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        owner.BeginEnemyTurn();
    }

    public override void OnLeave(bool isShutdown)
    {
        owner.EndEnemyTurn();
    }
}

internal sealed class DemoBattleResultState : FsmStateBase
{
    private readonly DemoBattleController owner;

    public DemoBattleResultState(DemoBattleController owner)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        owner.CompleteBattle();
    }
}
