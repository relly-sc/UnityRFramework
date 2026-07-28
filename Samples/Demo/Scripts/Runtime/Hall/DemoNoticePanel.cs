using System;
using UnityRFramework.Runtime;
using UnityEngine;

/// <summary>
/// 公告栏。通过 WebRequest 从 StreamingAssets 拉取公告 JSON 并展示。
/// 演示框架 WebRequest 模块的真实数据链路（本地走 file:// 协议）。
/// 固定节点和布局由 DemoHallUI 预制体提供，脚本只负责加载与刷新公告。
/// </summary>
public class DemoNoticePanel : MonoBehaviour
{
    private UnityEngine.UI.Text titleText;
    private UnityEngine.UI.Text bodyText;
    private DemoNotice notice;
    private bool isDestroyed;

    private void Awake()
    {
        titleText = GetChildText("NoticeTitle");
        bodyText = GetChildText("NoticeBody");
        if (titleText == null || bodyText == null)
        {
            Log.Error("[Demo] DemoNoticePanel prefab references are incomplete.");
            enabled = false;
            return;
        }

        titleText.text = GameEntry.Localization.GetString("UI_NoticeTitle");
        bodyText.text = GameEntry.Localization.GetString("UI_Loading");

        GameEntry.Event.Subscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
    }

    private void OnDestroy()
    {
        isDestroyed = true;
        if (GameEntry.Event != null)
        {
            GameEntry.Event.Unsubscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
        }
    }

    private UnityEngine.UI.Text GetChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<UnityEngine.UI.Text>() : null;
    }

    private async void Start()
    {
        try
        {
            notice = await DemoNoticeLoader.LoadNoticeAsync();
            if (isDestroyed)
            {
                return;
            }

            if (notice == null)
            {
                bodyText.text = GameEntry.Localization.GetString("UI_NoticeFail");
                return;
            }
            RefreshNoticeText();
        }
        catch (System.Exception ex)
        {
            Log.Error("[Demo] Notice panel load failed: {0}", ex);
            if (bodyText != null)
            {
                bodyText.text = GameEntry.Localization.GetString("UI_NoticeFail");
            }
        }
    }

    private void OnLanguageChanged(DemoLanguageChangedEvent e)
    {
        if (notice != null)
        {
            RefreshNoticeText();
        }
        else if (titleText != null)
        {
            titleText.text = GameEntry.Localization.GetString("UI_NoticeTitle");
        }
    }

    private void RefreshNoticeText()
    {
        titleText.text = GameEntry.Localization.GetString(notice.TitleKey);
        string[] lineKeys = notice.LineKeys ?? Array.Empty<string>();
        string[] lines = new string[lineKeys.Length];
        for (int i = 0; i < lineKeys.Length; i++)
        {
            lines[i] = GameEntry.Localization.GetString(lineKeys[i]);
        }

        bodyText.text = string.Join("\n", lines);
    }
}
