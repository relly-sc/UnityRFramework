using UnityRFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 顶栏。展示公会货币（金币/钻石），提供语言切换、重启和退出按钮。
/// 货币从 DemoGameState 读取，语言切换走 Localization 模块。
/// 自身只负责顶栏展示，不触及其他面板。
/// 固定节点和布局由 DemoHallUI 预制体提供，脚本只负责数据刷新与交互。
/// </summary>
public class DemoTopBar : MonoBehaviour
{
    private UnityEngine.UI.Text titleText;
    private UnityEngine.UI.Text goldText;
    private UnityEngine.UI.Text diamondText;
    private UnityEngine.UI.Text languageText;
    private UnityEngine.UI.Text restartText;
    private UnityEngine.UI.Text quitText;
    private Button languageButton;
    private Button restartButton;
    private Button quitButton;

    private void Awake()
    {
        if (!BindPrefabReferences())
        {
            enabled = false;
            return;
        }

        languageButton.onClick.RemoveAllListeners();
        languageButton.onClick.AddListener(OnSwitchLanguage);
        restartButton.onClick.RemoveAllListeners();
        restartButton.onClick.AddListener(OnRestart);
        quitButton.onClick.RemoveAllListeners();
        quitButton.onClick.AddListener(OnQuit);
        GameEntry.Event.Subscribe<DemoStateChangedEvent>(OnStateChanged);
        RefreshAllText();
    }

    private void OnDestroy()
    {
        if (GameEntry.Event != null)
        {
            GameEntry.Event.Unsubscribe<DemoStateChangedEvent>(OnStateChanged);
        }
    }

    private void OnStateChanged(DemoStateChangedEvent e)
    {
        Refresh();
    }

    private bool BindPrefabReferences()
    {
        titleText = GetChildComponent<UnityEngine.UI.Text>("Title");
        goldText = GetChildComponent<UnityEngine.UI.Text>("Gold");
        diamondText = GetChildComponent<UnityEngine.UI.Text>("Diamond");
        languageText = GetChildComponent<UnityEngine.UI.Text>("Language");
        restartText = GetChildComponent<UnityEngine.UI.Text>("Restart");
        quitText = GetChildComponent<UnityEngine.UI.Text>("Quit");
        languageButton = GetChildComponent<Button>("Language");
        restartButton = GetChildComponent<Button>("Restart");
        quitButton = GetChildComponent<Button>("Quit");
        if (titleText != null && goldText != null && diamondText != null &&
            languageText != null && restartText != null && quitText != null &&
            languageButton != null && restartButton != null && quitButton != null)
        {
            return true;
        }

        Log.Error("[Demo] DemoTopBar prefab references are incomplete.");
        return false;
    }

    private T GetChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponentInChildren<T>(true) : null;
    }

    private void Refresh()
    {
        if (goldText != null)
        {
            goldText.text = GameEntry.Localization.GetString("UI_Gold") + " " + DemoGameState.Gold;
        }
        if (diamondText != null)
        {
            diamondText.text = GameEntry.Localization.GetString("UI_Diamond") + " " + DemoGameState.Diamond;
        }
    }

    private void RefreshAllText()
    {
        titleText.text = GameEntry.Localization.GetString("UI_GuildTitle");
        languageText.text = GameEntry.Localization.GetString("UI_Language");
        restartText.text = GameEntry.Localization.GetString("UI_Restart");
        quitText.text = GameEntry.Localization.GetString("UI_Quit");
        Refresh();
    }

    private async void OnSwitchLanguage()
    {
        try
        {
            string next = GameEntry.Localization.CurrentLanguage == "zh-CN" ? "en" : "zh-CN";
            await GameEntry.Localization.SwitchLanguageAsync(next);
            RefreshAllText();
            GameEntry.Event.Fire(new DemoLanguageChangedEvent(next));
        }
        catch (System.Exception ex)
        {
            Log.Error("[Demo] Switch language failed: {0}", ex);
        }
    }

    /// <summary>
    /// 关闭并重新创建整套框架，不退出当前应用进程。
    /// </summary>
    private void OnRestart()
    {
        restartButton.interactable = false;
        quitButton.interactable = false;
        GameEntry.Restart();
    }

    /// <summary>
    /// 关闭框架并退出当前应用。
    /// </summary>
    private void OnQuit()
    {
        restartButton.interactable = false;
        quitButton.interactable = false;
        GameEntry.Quit();
    }
}
