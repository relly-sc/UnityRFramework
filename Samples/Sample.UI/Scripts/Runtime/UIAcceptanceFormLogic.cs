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
            lifecycleText.text = "已打开";
        }

        protected override void OnPause()
        {
            lifecycleText.text = "已被全屏界面暂停";
        }

        protected override void OnResume()
        {
            lifecycleText.text = "已恢复";
        }

        protected override void OnClose(object userData)
        {
            lifecycleText.text = "已关闭";
        }
    }
}
