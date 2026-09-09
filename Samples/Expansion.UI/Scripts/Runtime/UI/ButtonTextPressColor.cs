using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace UnityRFramework.Expansion
{
    [RequireComponent(typeof(Button))]
    public class ButtonTextPressColor : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("需要变色的按钮文字")]
        private Text targetText;

        [SerializeField, Tooltip("正常状态的文字颜色")]
        private Color normalColor = Color.white;

        [SerializeField, Tooltip("按下按钮时的文字颜色")]
        private Color pressedColor = Color.gray;

        public void OnPointerDown(PointerEventData eventData) => SetTextColor(pressedColor);

        public void OnPointerUp(PointerEventData eventData) => SetTextColor(normalColor);

        public void OnPointerExit(PointerEventData eventData) => SetTextColor(normalColor);

        private void OnDisable() => SetTextColor(normalColor);

        private void SetTextColor(Color color)
        {
            if (targetText != null)
            {
                targetText.color = color;
            }
        }
    }

}

