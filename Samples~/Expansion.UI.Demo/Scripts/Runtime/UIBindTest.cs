using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityRFramework.Sample.UI
{
    public partial class UIBindTest : MonoBehaviour
    {
        private void Start()
        {
            text.text = "等待输入";
            input.text = string.Empty;

            foreach (var image in icon)
            {
                image.color = Color.white;
            }
        }

        partial void OnInputEndEdit(string value)
        {
            text.text = "UGUI: " + value;
        }

        partial void OnInputTmpSubmit(string value)
        {
            text.text = "TMP: " + value;
        }

        partial void OnBtnClick()
        {
            text.text = "按钮已点击";
        }
    }
}