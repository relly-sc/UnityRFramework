using RFramework;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Sample.Security
{
    /// <summary>Protected 基础数值的独立验收控制器。</summary>
    [DisallowMultipleComponent]
    public sealed class SecurityAcceptanceController : MonoBehaviour
    {
        [SerializeField] private Text normalValueText;
        [SerializeField] private Text protectedValueText;
        [SerializeField] private Text tamperText;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button recreateButton;

        private int normalValue = 1000;
        private ProtectedInt protectedValue = new ProtectedInt(1000);
        private int tamperCount;
        private IEventModule eventModule;

        private void Start()
        {
            increaseButton.onClick.AddListener(IncreaseValues);
            resetButton.onClick.AddListener(ResetValues);
            recreateButton.onClick.AddListener(RecreateProtectedValue);
            eventModule = RFrameworkModuleHost.Get<IEventModule>();
            eventModule.Subscribe<MemoryTamperEvent>(OnMemoryTampered);
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (eventModule != null)
            {
                eventModule.Unsubscribe<MemoryTamperEvent>(OnMemoryTampered);
            }

            increaseButton.onClick.RemoveAllListeners();
            resetButton.onClick.RemoveAllListeners();
            recreateButton.onClick.RemoveAllListeners();
        }

        private void IncreaseValues()
        {
            normalValue += 10;
            protectedValue = new ProtectedInt((int)protectedValue + 10);
            RefreshDisplay();
        }

        private void ResetValues()
        {
            normalValue = 1000;
            protectedValue = new ProtectedInt(1000);
            tamperCount = 0;
            RefreshDisplay();
        }

        private void RecreateProtectedValue()
        {
            protectedValue = new ProtectedInt((int)protectedValue);
            RefreshDisplay();
        }

        private void OnMemoryTampered(MemoryTamperEvent message)
        {
            tamperCount++;
            tamperText.text = "篡改事件：已检测到 " + tamperCount + " 次（" + message.ValueTypeName + "）";
            protectedValueText.text = "ProtectedInt：" + (int)protectedValue;
        }

        private void RefreshDisplay()
        {
            normalValueText.text = "普通整数：" + normalValue;
            protectedValueText.text = "ProtectedInt：" + (int)protectedValue;
            tamperText.text = tamperCount == 0
                ? "篡改事件：暂无"
                : "篡改事件：已检测到 " + tamperCount + " 次";
        }
    }
}
