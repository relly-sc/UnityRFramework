using System.Collections;
using System.Collections.Generic;
using Game.Config;
using UnityRFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务板。展示所有可接取任务（从 Quest 配置读取），点击「接取」发布 QuestAcceptedEvent
/// 并将任务标记为已接（移入下方已接取区）。自身只负责中栏任务展示与接取交互。
/// 固定容器和布局由 DemoHallUI 预制体提供，任务条目按配置动态生成。
/// </summary>
public class DemoQuestBoard : MonoBehaviour
{
    private Transform listContainer;
    private Transform acceptedContainer;
    private UnityEngine.UI.Text titleText;
    private Coroutine rebuildRoutine;

    private void Awake()
    {
        Transform title = transform.Find("QuestTitle");
        titleText = title != null ? title.GetComponent<UnityEngine.UI.Text>() : null;
        listContainer = transform.Find("AvailableQuests");
        acceptedContainer = transform.Find("ActiveQuests");
        if (titleText == null || listContainer == null || acceptedContainer == null)
        {
            Log.Error("[Demo] DemoQuestBoard prefab references are incomplete.");
            enabled = false;
            return;
        }

        RefreshTitle();
        BuildList();
        GameEntry.Event.Subscribe<QuestAcceptedEvent>(OnQuestAccepted);
        GameEntry.Event.Subscribe<DemoStateChangedEvent>(OnStateChanged);
        GameEntry.Event.Subscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
    }

    private void OnDestroy()
    {
        if (GameEntry.Event != null)
        {
            GameEntry.Event.Unsubscribe<QuestAcceptedEvent>(OnQuestAccepted);
            GameEntry.Event.Unsubscribe<DemoStateChangedEvent>(OnStateChanged);
            GameEntry.Event.Unsubscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
        }
    }

    private void RefreshTitle()
    {
        titleText.text = GameEntry.Localization.GetString("UI_QuestTitle");
    }

    private void BuildList()
    {
        if (listContainer == null || acceptedContainer == null)
        {
            return;
        }

        for (int i = listContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = listContainer.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        for (int i = acceptedContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = acceptedContainer.GetChild(i);
            if (child.name.StartsWith("Quest_"))
            {
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        IReadOnlyList<Demo_QuestConfig> quests = GameEntry.Config.GetAllConfigs<Demo_QuestConfig>();
        foreach (Demo_QuestConfig q in quests)
        {
            if (DemoGameState.AcceptedQuests.Contains(q.Id))
            {
                BuildQuestItem(q, true);
            }
            else
            {
                BuildQuestItem(q, false);
            }
        }
    }

    private void BuildQuestItem(Demo_QuestConfig q, bool accepted)
    {
        Transform parent = accepted ? acceptedContainer : listContainer;
        bool selected = DemoGameState.SelectedQuestId == q.Id;
        Color itemColor = selected ? new Color(0.52f, 0.78f, 0.58f) :
            (accepted ? new Color(0.66f, 0.82f, 0.96f) : new Color(0.71f, 0.83f, 0.96f));
        RectTransform item = DemoUIHelper.MakePanel(parent, "Quest_" + q.Id, itemColor);
        LayoutElement itemLayout = item.gameObject.AddComponent<LayoutElement>();
        itemLayout.minHeight = 102f;
        itemLayout.preferredHeight = 102f;
        itemLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = item.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.padding = new RectOffset(8, 8, 8, 8);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;

        RectTransform col = DemoUIHelper.MakeRect(hlg.transform, "Col");
        VerticalLayoutGroup cvlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        cvlg.childControlWidth = true;
        cvlg.childControlHeight = true;
        cvlg.childForceExpandHeight = false;
        col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        Demo_EnemyConfig enemy = GameEntry.Config.GetConfig<Demo_EnemyConfig>(q.EnemyId);
        string enemyName = enemy != null ? GameEntry.Localization.GetString(enemy.NameKey) : q.EnemyId.ToString();
        string name = GameEntry.Localization.GetString(q.NameKey) + " · " + GameEntry.Localization.GetString("UI_Enemy") + " " + enemyName;
        DemoUIHelper.MakeText(col, name, 12, new Color(0.04f, 0.17f, 0.32f))
            .gameObject.AddComponent<LayoutElement>().minHeight = 32;
        string diff = GameEntry.Localization.GetString("UI_Difficulty") + " " + q.Difficulty + " · " + GameEntry.Localization.GetString("UI_Reward") + " " + q.RewardGold + " " + GameEntry.Localization.GetString("UI_Gold");
        DemoUIHelper.MakeText(col, diff, 11, new Color(0.09f, 0.37f, 0.65f))
            .gameObject.AddComponent<LayoutElement>().minHeight = 32;

        if (!accepted)
        {
            int id = q.Id;
            DemoUIHelper.MakeButton(hlg.transform, GameEntry.Localization.GetString("UI_Accept"), new Color(0.23f, 0.43f, 0.07f), () => AcceptQuest(id))
                .gameObject.AddComponent<LayoutElement>().minWidth = 100;
        }
        else
        {
            int id = q.Id;
            Button button = DemoUIHelper.MakeButton(hlg.transform,
                selected ? GameEntry.Localization.GetString("UI_Selected") : GameEntry.Localization.GetString("UI_Select"),
                selected ? new Color(0.12f, 0.46f, 0.22f) : new Color(0.09f, 0.37f, 0.65f),
                () => SelectQuest(id));
            button.gameObject.AddComponent<LayoutElement>().minWidth = 100;
            button.interactable = !selected;
        }
    }

    private void AcceptQuest(int id)
    {
        DemoGameState.AcceptQuest(id);
        GameEntry.Event.Fire(new QuestAcceptedEvent(id));
        GameEntry.Event.Fire(new DemoStateChangedEvent());
    }

    private void SelectQuest(int id)
    {
        if (DemoGameState.SelectQuest(id))
        {
            GameEntry.Event.Fire(new DemoStateChangedEvent());
        }
    }

    private void OnQuestAccepted(QuestAcceptedEvent e)
    {
        Log.Info("[Demo] QuestBoard: quest {0} accepted", e.QuestId);
    }

    private void OnStateChanged(DemoStateChangedEvent e)
    {
        ScheduleRebuild();
    }

    private void OnLanguageChanged(DemoLanguageChangedEvent e)
    {
        RefreshTitle();
        ScheduleRebuild();
    }

    private void ScheduleRebuild()
    {
        if (rebuildRoutine == null)
        {
            rebuildRoutine = StartCoroutine(RebuildNextFrame());
        }
    }

    private IEnumerator RebuildNextFrame()
    {
        yield return null;
        rebuildRoutine = null;
        BuildList();
    }
}
