using System.Collections.Generic;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 远征场景 HUD。使用 UGUI 和纯色块构建，不持有战斗规则。
/// </summary>
public sealed class DemoExpeditionWindow : UIFormLogic
{
    private const int MaxVisibleLogLines = 12;

    private DemoBattleController battle;
    private readonly Queue<string> battleLogs = new Queue<string>();

    [Header("Header")]
    [SerializeField] private Text battleTitleText;
    [SerializeField] private Text phaseText;
    [SerializeField] private Text roundText;

    [Header("Fighters")]
    [SerializeField] private Text heroNameText;
    [SerializeField] private Text enemyNameText;
    [SerializeField] private Text heroStatsText;
    [SerializeField] private Text enemyStatsText;
    [SerializeField] private Text heroHpText;
    [SerializeField] private Text enemyHpText;
    [SerializeField] private Image heroHpFill;
    [SerializeField] private Image enemyHpFill;

    [Header("Battle Log")]
    [SerializeField] private Text logText;

    [Header("Actions")]
    [SerializeField] private RectTransform actionsPanel;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Text attackButtonText;
    [SerializeField] private Text defendButtonText;
    [SerializeField] private Text skillButtonText;

    [Header("Result")]
    [SerializeField] private RectTransform resultPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private Button returnButton;
    [SerializeField] private Text returnButtonText;

    protected override void OnInit(UIForm owner, object userData)
    {
        base.OnInit(owner, userData);
        ValidateReferences();
        attackButton.onClick.AddListener(OnAttackClicked);
        defendButton.onClick.AddListener(OnDefendClicked);
        skillButton.onClick.AddListener(OnSkillClicked);
        returnButton.onClick.AddListener(ReturnToHall);
    }

    protected override void OnOpen(object userData)
    {
        battleLogs.Clear();
        logText.text = string.Empty;
        actionsPanel.gameObject.SetActive(true);
        resultPanel.gameObject.SetActive(false);
        RefreshStaticTexts();
        battle = new DemoBattleController(this);
        battle.Start();
    }

    protected override void OnClose(object userData)
    {
        DisposeBattle();
    }

    private void OnDestroy()
    {
        attackButton?.onClick.RemoveListener(OnAttackClicked);
        defendButton?.onClick.RemoveListener(OnDefendClicked);
        skillButton?.onClick.RemoveListener(OnSkillClicked);
        returnButton?.onClick.RemoveListener(ReturnToHall);
        DisposeBattle();
    }

    public void SetPhase(string phase)
    {
        phaseText.text = phase;
    }

    public void RefreshBattle(DemoBattleController controller)
    {
        roundText.text = GameEntry.Localization.GetString("UI_Round") + " " + controller.Round;
        heroNameText.text = GameEntry.Localization.GetString(controller.PlayerNameKey) + " Lv." + controller.PlayerLevel;
        enemyNameText.text = GameEntry.Localization.GetString(controller.EnemyNameKey);
        heroStatsText.text = FormatStats(controller.PlayerAttack, controller.PlayerDefense);
        enemyStatsText.text = FormatStats(controller.EnemyAttack, controller.EnemyDefense);
        heroHpText.text = GameEntry.Localization.GetString("UI_HP") + " " + controller.PlayerHp + "/" + controller.PlayerMaxHp;
        enemyHpText.text = GameEntry.Localization.GetString("UI_HP") + " " + controller.EnemyHp + "/" + controller.EnemyMaxHp;
        SetFill(heroHpFill, controller.PlayerHp, controller.PlayerMaxHp);
        SetFill(enemyHpFill, controller.EnemyHp, controller.EnemyMaxHp);
        bool interactable = controller.IsPlayerTurn;
        foreach (Button button in actionsPanel.GetComponentsInChildren<Button>(true))
        {
            button.interactable = interactable;
        }
        if (skillButton != null)
        {
            skillButton.interactable = interactable && controller.SkillAvailable;
        }
    }

    public void AppendLog(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        battleLogs.Enqueue(message);
        while (battleLogs.Count > MaxVisibleLogLines)
        {
            battleLogs.Dequeue();
        }

        logText.text = string.Join("\n", battleLogs);
    }

    public void ShowResult(bool success, int experience, int gold)
    {
        actionsPanel.gameObject.SetActive(false);
        resultPanel.gameObject.SetActive(true);
        string title = GameEntry.Localization.GetString(success ? "UI_Victory" : "UI_Defeat");
        resultText.text = title + "\n" + GameEntry.Localization.GetString("UI_Reward") + ": " +
            experience + " " + GameEntry.Localization.GetString("UI_Exp") + " / " +
            gold + " " + GameEntry.Localization.GetString("UI_Gold");
    }

    private static void SetFill(Image image, int value, int maxValue)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        rect.anchorMax = new Vector2(maxValue > 0 ? Mathf.Clamp01((float)value / maxValue) : 0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string FormatStats(int attack, int defense)
    {
        return GameEntry.Localization.GetString("UI_ATK") + " " + attack + "    " +
            GameEntry.Localization.GetString("UI_DEF") + " " + defense;
    }

    private void RefreshStaticTexts()
    {
        battleTitleText.text = GameEntry.Localization.GetString("UI_BattleTitle");
        attackButtonText.text = GameEntry.Localization.GetString("action_attack");
        defendButtonText.text = GameEntry.Localization.GetString("action_defend");
        skillButtonText.text = GameEntry.Localization.GetString("action_skill");
        returnButtonText.text = GameEntry.Localization.GetString("UI_ReturnHall");
    }

    private void ValidateReferences()
    {
        if (battleTitleText == null || phaseText == null || roundText == null
            || heroNameText == null || enemyNameText == null
            || heroStatsText == null || enemyStatsText == null
            || heroHpText == null || enemyHpText == null
            || heroHpFill == null || enemyHpFill == null || logText == null
            || actionsPanel == null || attackButton == null || defendButton == null
            || skillButton == null || attackButtonText == null
            || defendButtonText == null || skillButtonText == null
            || resultPanel == null || resultText == null || returnButton == null
            || returnButtonText == null)
        {
            throw new RFrameworkException(
                "DemoExpeditionWindow: Scene UI references are incomplete.");
        }
    }

    private void OnAttackClicked() => battle?.UseAction(1);

    private void OnDefendClicked() => battle?.UseAction(2);

    private void OnSkillClicked() => battle?.UseAction(3);

    private void ReturnToHall()
    {
        GameEntry.Procedure.ChangeProcedure<DemoReturnProcedure>();
    }

    private void DisposeBattle()
    {
        battle?.Dispose();
        battle = null;
    }
}
