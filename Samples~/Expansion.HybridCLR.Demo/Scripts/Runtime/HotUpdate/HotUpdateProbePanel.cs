using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Sample
{
    /// <summary>
    /// 展示当前代码版本，并由热更新代码控制进入 AOT Demo。
    /// </summary>
    public sealed class HotUpdateProbePanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("显示当前热更新代码版本。")]
        private Text versionText;

        [SerializeField]
        [Tooltip("显示热更新入口执行状态。")]
        private Text statusText;

        [SerializeField]
        [Tooltip("继续进入现有 Demo 的按钮。")]
        private Button continueButton;

        [SerializeField]
        [Tooltip("继续按钮的文字。")]
        private Text continueButtonText;

        private Func<CancellationToken, Task> continueAction;
        private CancellationTokenSource lifetimeCts;
        private bool isContinuing;

        /// <summary>
        /// 绑定 AOT 回调并显示当前代码版本。
        /// </summary>
        public void Initialize(
            string codeVersion,
            Func<CancellationToken, Task> onContinue)
        {
            continueAction = onContinue
                ?? throw new ArgumentNullException(nameof(onContinue));
            lifetimeCts = new CancellationTokenSource();
            versionText.text = $"热更新代码版本：{codeVersion}";
            statusText.text = "热更新程序集已加载，入口逻辑正在运行。";
            continueButtonText.text = "进入 Demo";
            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.interactable = true;
        }

        /// <summary>
        /// 解除事件并取消尚未完成的继续操作。
        /// </summary>
        public void Shutdown()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            lifetimeCts?.Cancel();
            lifetimeCts?.Dispose();
            lifetimeCts = null;
            continueAction = null;
        }

        private void OnContinueClicked()
        {
            if (isContinuing || continueAction == null || lifetimeCts == null)
            {
                return;
            }

            _ = ContinueAsync(lifetimeCts.Token);
        }

        private async Task ContinueAsync(CancellationToken ct)
        {
            isContinuing = true;
            continueButton.interactable = false;
            statusText.text = "正在进入 Demo...";
            try
            {
                await continueAction(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 场景销毁或框架退出时正常结束。
            }
            catch (Exception exception)
            {
                statusText.text = $"进入 Demo 失败：{exception.Message}";
                continueButton.interactable = true;
            }
            finally
            {
                isContinuing = false;
            }
        }
    }
}
