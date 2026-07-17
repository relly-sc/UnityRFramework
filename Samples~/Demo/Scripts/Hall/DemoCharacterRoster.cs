using System.Collections;
using System.Collections.Generic;
using Game.Config;
using UnityRFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 英雄栏。展示所有可招募英雄卡片（从 Character 配置读取），
/// 点击卡片选中英雄（写入 DemoGameState），并响应升级事件重建以刷新等级。
/// 自身只负责左侧面板展示与交互。
/// 固定容器和布局由 DemoHallUI 预制体提供，角色卡按配置动态生成。
/// </summary>
public class DemoCharacterRoster : MonoBehaviour
{
    private Transform cardsContainer;
    private UnityEngine.UI.Text titleText;
    private Coroutine rebuildRoutine;

    private void Awake()
    {
        Transform title = transform.Find("RosterTitle");
        cardsContainer = transform.Find("CharacterList");
        titleText = title != null ? title.GetComponent<UnityEngine.UI.Text>() : null;
        if (titleText == null || cardsContainer == null)
        {
            Log.Error("[Demo] DemoCharacterRoster prefab references are incomplete.");
            enabled = false;
            return;
        }

        RefreshTitle();
        BuildCards();
        GameEntry.Event.Subscribe<CharacterUpgradedEvent>(OnCharacterUpgraded);
        GameEntry.Event.Subscribe<DemoStateChangedEvent>(OnStateChanged);
        GameEntry.Event.Subscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
    }

    private void OnDestroy()
    {
        if (GameEntry.Event != null)
        {
            GameEntry.Event.Unsubscribe<CharacterUpgradedEvent>(OnCharacterUpgraded);
            GameEntry.Event.Unsubscribe<DemoStateChangedEvent>(OnStateChanged);
            GameEntry.Event.Unsubscribe<DemoLanguageChangedEvent>(OnLanguageChanged);
        }
    }

    private void RefreshTitle()
    {
        titleText.text = GameEntry.Localization.GetString("UI_RosterTitle");
    }

    private void BuildCards()
    {
        if (cardsContainer == null)
        {
            return;
        }

        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = cardsContainer.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        IReadOnlyList<Demo_CharacterConfig> characters = GameEntry.Config.GetAllConfigs<Demo_CharacterConfig>();
        foreach (Demo_CharacterConfig c in characters)
        {
            BuildCard(c);
        }
    }

    private void BuildCard(Demo_CharacterConfig c)
    {
        bool selected = DemoGameState.SelectedCharacterId == c.Id;
        Color cardColor = selected ? new Color(0.52f, 0.78f, 0.58f) : new Color(0.81f, 0.78f, 0.96f);
        RectTransform card = DemoUIHelper.MakePanel(cardsContainer, "Card_" + c.Id, cardColor);
        LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
        cardLayout.minHeight = 118f;
        cardLayout.preferredHeight = 118f;
        cardLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup hlg = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.padding = new RectOffset(8, 8, 8, 8);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;

        RectTransform avatar = DemoUIHelper.MakePanel(hlg.transform, "Avatar", new Color(0.68f, 0.66f, 0.92f));
        LayoutElement ale = avatar.gameObject.AddComponent<LayoutElement>();
        ale.minWidth = 56;
        ale.minHeight = 56;
        ale.preferredWidth = 56;
        ale.preferredHeight = 56;

        RectTransform col = DemoUIHelper.MakeRect(hlg.transform, "Col");
        VerticalLayoutGroup colVlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        colVlg.childControlWidth = true;
        colVlg.childControlHeight = true;
        colVlg.childForceExpandHeight = false;
        col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        string name = GameEntry.Localization.GetString(c.NameKey);
        string job = GameEntry.Localization.GetString(c.ClassKey);
        DemoUIHelper.MakeText(col, name + " Lv." + DemoGameState.GetLevel(c.Id) + " · " + job, 12, new Color(0.15f, 0.13f, 0.36f))
            .gameObject.AddComponent<LayoutElement>().minHeight = 32;
        string stats = GameEntry.Localization.GetString("UI_ATK") + " " + DemoGameState.GetAttack(c) +
            "  " + GameEntry.Localization.GetString("UI_DEF") + " " + DemoGameState.GetDefense(c) +
            "  " + GameEntry.Localization.GetString("UI_HP") + " " + DemoGameState.GetMaxHp(c);
        DemoUIHelper.MakeText(col, stats, 11, new Color(0.32f, 0.29f, 0.72f))
            .gameObject.AddComponent<LayoutElement>().minHeight = 32;

        int id = c.Id;
        RectTransform actions = DemoUIHelper.MakeRect(hlg.transform, "Actions");
        LayoutElement actionsElement = actions.gameObject.AddComponent<LayoutElement>();
        actionsElement.minWidth = 142f;
        actionsElement.preferredWidth = 142f;
        actionsElement.flexibleWidth = 0f;
        VerticalLayoutGroup actionsLayout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
        actionsLayout.spacing = 6f;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = true;
        actionsLayout.childForceExpandHeight = false;

        RectTransform selectSlot = DemoUIHelper.MakeRect(actions, "SelectSlot");
        selectSlot.gameObject.AddComponent<LayoutElement>().minHeight = 44f;
        string selectLabel = selected ? GameEntry.Localization.GetString("UI_Selected") : GameEntry.Localization.GetString("UI_Select");
        Button selectButton = DemoUIHelper.MakeButton(selectSlot, selectLabel,
            selected ? new Color(0.12f, 0.46f, 0.22f) : new Color(0.25f, 0.39f, 0.68f), () => SelectCharacter(id));
        selectButton.interactable = !selected;

        RectTransform upgradeSlot = DemoUIHelper.MakeRect(actions, "UpgradeSlot");
        upgradeSlot.gameObject.AddComponent<LayoutElement>().minHeight = 44f;
        int cost = c.UpgradeCost * DemoGameState.GetLevel(c.Id);
        DemoUIHelper.MakeButton(upgradeSlot,
            GameEntry.Localization.GetString("UI_Upgrade") + " " + cost,
            new Color(0.67f, 0.43f, 0.08f), () => UpgradeCharacter(id));
    }

    private void SelectCharacter(int id)
    {
        DemoGameState.SelectCharacter(id);
        Log.Info("[Demo] Roster: selected character {0}", id);
        GameEntry.Event.Fire(new DemoStateChangedEvent());
    }

    private void UpgradeCharacter(int id)
    {
        Demo_CharacterConfig character = GameEntry.Config.GetConfig<Demo_CharacterConfig>(id);
        if (!DemoGameState.TryUpgradeCharacter(character, out int newLevel))
        {
            Log.Warning("[Demo] Roster: insufficient gold to upgrade character {0}.", id);
            return;
        }

        GameEntry.Event.Fire(new CharacterUpgradedEvent(id, newLevel));
        GameEntry.Event.Fire(new DemoStateChangedEvent());
    }

    private void OnCharacterUpgraded(CharacterUpgradedEvent e)
    {
        Log.Info("[Demo] Roster: character {0} upgraded to Lv.{1}", e.CharacterId, e.NewLevel);
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
        BuildCards();
    }
}
