using System;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample.UI
{
    /// <summary>
    /// Sample.UI 的轻量交互控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIAcceptanceController : MonoBehaviour
    {
        private const string PanelAPath = "UIAcceptance/PanelA";
        private const string PanelBPath = "UIAcceptance/PanelB";
        private const string FullScreenPath = "UIAcceptance/FullScreen";
        private const string IndependentCanvasPath = "UIAcceptance/IndependentCanvas";

        [SerializeField] private Button openPanelAButton;
        [SerializeField] private Button openPanelBButton;
        [SerializeField] private Button openFullScreenButton;
        [SerializeField] private Button openIndependentCanvasButton;
        [SerializeField] private Button closeTopButton;
        [SerializeField] private Button closeAllButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Text statusText;

        private int sequence;
        private bool opening;

        private void Start()
        {
            openPanelAButton.onClick.AddListener(() => OpenAsync(PanelAPath, UILayer.Panel, false));
            openPanelBButton.onClick.AddListener(() => OpenAsync(PanelBPath, UILayer.Panel, false));
            openFullScreenButton.onClick.AddListener(() => OpenAsync(
                FullScreenPath, UILayer.Popup, true));
            openIndependentCanvasButton.onClick.AddListener(() => OpenAsync(
                IndependentCanvasPath, UILayer.Panel, false));
            closeTopButton.onClick.AddListener(CloseTop);
            closeAllButton.onClick.AddListener(CloseAll);
            restartButton.onClick.AddListener(GameEntry.Restart);
            RefreshStatus("准备完成");
        }

        private void OnDestroy()
        {
            openPanelAButton.onClick.RemoveAllListeners();
            openPanelBButton.onClick.RemoveAllListeners();
            openFullScreenButton.onClick.RemoveAllListeners();
            openIndependentCanvasButton.onClick.RemoveAllListeners();
            closeTopButton.onClick.RemoveAllListeners();
            closeAllButton.onClick.RemoveAllListeners();
            restartButton.onClick.RemoveAllListeners();
        }

        private async void OpenAsync(string path, int layer, bool fullScreen)
        {
            if (opening || GameEntry.UI == null)
            {
                return;
            }

            opening = true;
            SetButtonsInteractable(false);
            try
            {
                string title = fullScreen
                    ? "全屏弹窗"
                    : path.EndsWith("PanelA")
                        ? "普通面板 A"
                        : path.EndsWith("PanelB") ? "普通面板 B" : "独立画布界面";
                UIAcceptancePayload payload = new UIAcceptancePayload(title, ++sequence);
                await GameEntry.UI.OpenUIFormAsync(path, layer, fullScreen, userData: payload);
                RefreshStatus("已打开" + payload.Title);
            }
            catch (Exception ex)
            {
                RefreshStatus("打开失败：" + ex.Message);
            }
            finally
            {
                opening = false;
                SetButtonsInteractable(true);
            }
        }

        private void CloseTop()
        {
            bool closed = GameEntry.UI != null && GameEntry.UI.CloseTopUIForm();
            RefreshStatus(closed ? "已关闭顶部界面" : "没有可关闭的界面");
        }

        private void CloseAll()
        {
            GameEntry.UI?.CloseAllUIForms();
            RefreshStatus("已关闭全部界面");
        }

        private void SetButtonsInteractable(bool value)
        {
            openPanelAButton.interactable = value;
            openPanelBButton.interactable = value;
            openFullScreenButton.interactable = value;
            openIndependentCanvasButton.interactable = value;
        }

        private void RefreshStatus(string message)
        {
            IUIForm top = GameEntry.UI?.GetTopUIForm();
            int count = GameEntry.UI?.UIFormCount ?? 0;
            statusText.text = message + "\n已管理：" + count +
                "   顶部界面：" + (top?.AssetName ?? "无") +
                "\n场景 HUD 仍由外部管理。";
        }
    }
}
