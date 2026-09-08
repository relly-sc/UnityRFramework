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
            RefreshStatus("Ready");
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
                    ? "Full-screen Popup"
                    : path.EndsWith("PanelA")
                        ? "Panel A"
                        : path.EndsWith("PanelB") ? "Panel B" : "Independent Canvas";
                UIAcceptancePayload payload = new UIAcceptancePayload(title, ++sequence);
                await GameEntry.UI.OpenUIFormAsync(path, layer, fullScreen, userData: payload);
                RefreshStatus("Opened " + payload.Title);
            }
            catch (Exception ex)
            {
                RefreshStatus("Open failed: " + ex.Message);
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
            RefreshStatus(closed ? "Closed top window" : "No window to close");
        }

        private void CloseAll()
        {
            GameEntry.UI?.CloseAllUIForms();
            RefreshStatus("Closed all windows");
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
            statusText.text = message + "\nManaged: " + count +
                "   Top: " + (top?.AssetName ?? "None") +
                "\nScene HUD remains externally owned.";
        }
    }
}
