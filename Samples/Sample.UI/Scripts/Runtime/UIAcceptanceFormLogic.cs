using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample.UI
{
    /// <summary>
    /// 显示窗口参数和生命周期状态的 Sample UI 逻辑。
    /// </summary>
    public sealed class UIAcceptanceFormLogic : UIFormLogic
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text lifecycleText;

        protected override void OnOpen(object userData)
        {
            UIAcceptancePayload payload = userData as UIAcceptancePayload;
            titleText.text = payload == null
                ? Owner.AssetName
                : payload.Title + "  #" + payload.Sequence;
            lifecycleText.text = "Opened";
        }

        protected override void OnPause()
        {
            lifecycleText.text = "Paused by full-screen window";
        }

        protected override void OnResume()
        {
            lifecycleText.text = "Resumed";
        }

        protected override void OnClose(object userData)
        {
            lifecycleText.text = "Closed";
        }
    }
}
