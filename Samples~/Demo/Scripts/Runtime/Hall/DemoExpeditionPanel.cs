using Game.Config;
using UnityRFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 远征面板。展示当前选中的英雄与任务，点击「出发远征」发布 ExpeditionStartedEvent
/// 并切换到远征流程。自身只负责右下角的远征状态展示与出发交互。
/// 固定节点和布局由 DemoHallUI 预制体提供，脚本只负责状态刷新与交互。
/// </summary>
public class DemoExpeditionPanel : MonoBehaviour
{
    private UnityEngine.UI.Text statusText;
    private UnityEngine.UI.Text titleText;
    private UnityEngine.UI.Text departButtonText;
    private Button departButton;

    private void Awake()
    {
        titleText = GetChildComponent<UnityEngine.UI.Text>("ExpeditionTitle");
        statusText = GetChildComponent<UnityEngine.UI.Text>("ExpeditionStatus");
        departButton = GetChildComponent<Button>("DepartButton");
        departButtonText = GetChildComponent<UnityEngine.UI.Text>("DepartButton");
        if (titleText == null || statusText == null || departButton == null || departButtonText == null)
        {
            Log.Error("[Demo] DemoExpeditionPanel prefab references are incomplete.");
            enabled = false;
            return;
        }

        departButton.onClick.RemoveAllListeners();
        departButton.onClick.AddListener(OnStartExpedition);
        RefreshLocalizedText();

        GameEntry.Event.Subscribe<ExpeditionEndedEvent>(OnExpeditionEnded);
        GameEntry.Event.Subscribe<DemoStateChangedEvent>(OnStateChanged);
        GameEntry.Event.Subscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
        Refresh();
    }

    private T GetChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponentInChildren<T>(true) : null;
    }

    private void OnDestroy()
    {
        if (GameEntry.Event != null)
        {
            GameEntry.Event.Unsubscribe<ExpeditionEndedEvent>(OnExpeditionEnded);
            GameEntry.Event.Unsubscribe<DemoStateChangedEvent>(OnStateChanged);
            GameEntry.Event.Unsubscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
        }
    }

    private void Refresh()
    {
        string none = GameEntry.Localization.GetString("UI_NoneSelected");
        string standby = GameEntry.Localization.GetString("UI_Standby");
        string noOngoing = GameEntry.Localization.GetString("UI_NoOngoing");
        string hero = DemoGameState.SelectedCharacterId >= 0
            ? GameEntry.Localization.GetString(GameEntry.Config.GetConfig<Demo_CharacterConfig>(DemoGameState.SelectedCharacterId)?.NameKey ?? string.Empty)
            : none;
        string quest = DemoGameState.SelectedQuestId >= 0
            ? GameEntry.Localization.GetString(GameEntry.Config.GetConfig<Demo_QuestConfig>(DemoGameState.SelectedQuestId)?.NameKey ?? string.Empty)
            : none;
        statusText.text = GameEntry.Localization.GetString("UI_CurrentState") + ": " + (DemoGameState.SelectedQuestId >= 0 ? standby : noOngoing) +
            "\n" + GameEntry.Localization.GetString("UI_SelectedHero") + ": " + hero +
            "\n" + GameEntry.Localization.GetString("UI_TargetQuest") + ": " + quest;
        if (departButton != null)
        {
            departButton.interactable = DemoGameState.SelectedCharacterId >= 0 && DemoGameState.SelectedQuestId >= 0;
        }
    }

    private void OnStartExpedition()
    {
        if (DemoGameState.SelectedCharacterId < 0 || DemoGameState.SelectedQuestId < 0)
        {
            Log.Warning("[Demo] Expedition: hero and quest must both be selected.");
            return;
        }
        GameEntry.Event.Fire(new ExpeditionStartedEvent(DemoGameState.SelectedQuestId));
        // 同步切换：按钮回调直接触发，ChangeProcedure 同步执行离开/进入，不挂起调用方。
        GameEntry.Procedure.ChangeProcedure<DemoExpeditionProcedure>();
    }

    private void OnExpeditionEnded(ExpeditionEndedEvent e)
    {
        Refresh();
    }

    private void OnStateChanged(DemoStateChangedEvent e)
    {
        Refresh();
    }

    private void OnLanguageChanged(DemoLanguageChangedEvent e)
    {
        RefreshLocalizedText();
        Refresh();
    }

    private void RefreshLocalizedText()
    {
        titleText.text = GameEntry.Localization.GetString("UI_ExpeditionTitle");
        departButtonText.text = GameEntry.Localization.GetString("UI_Depart");
    }
}
