using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// ButtonState 与 ButtonStateGroup 的状态契约测试。
    /// </summary>
    public sealed class ButtonStateTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        /// <summary>
        /// 清理每个测试创建的场景对象。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        /// <summary>
        /// 未分组按钮应支持独立选中与取消。
        /// </summary>
        [Test]
        public void StandaloneButtonStateCanToggle()
        {
            ButtonState state = CreateState("Standalone", null);

            Assert.IsTrue(state.SetSelected(true));
            Assert.IsTrue(state.IsSelected);
            Assert.IsTrue(state.SetSelected(false));
            Assert.IsFalse(state.IsSelected);
        }

        /// <summary>
        /// 状态组应保持单选，并按旧项、新项、组事件的顺序通知。
        /// </summary>
        [Test]
        public void GroupSwitchesSelectionInDocumentedOrder()
        {
            ButtonStateGroup group = CreateGroup("Group");
            ButtonState first = CreateState("First", group);
            ButtonState second = CreateState("Second", group);
            List<string> events = new List<string>();

            first.StateChanged += (_, selected) => events.Add("first:" + selected);
            second.StateChanged += (_, selected) => events.Add("second:" + selected);
            group.SelectionChanged += state =>
                events.Add("group:" + (state != null ? state.name : "null"));

            Assert.IsTrue(group.Select(first, false));
            Assert.IsTrue(group.Select(second));

            CollectionAssert.AreEqual(
                new[]
                {
                    "first:False",
                    "second:True",
                    "group:Second"
                },
                events);
            Assert.AreSame(second, group.Selected);
            Assert.IsFalse(first.IsSelected);
            Assert.IsTrue(second.IsSelected);
        }

        /// <summary>
        /// 禁止无选择时不能取消当前项；允许后可以取消。
        /// </summary>
        [Test]
        public void GroupHonorsAllowNoSelection()
        {
            ButtonStateGroup group = CreateGroup("Group");
            ButtonState state = CreateState("State", group);
            group.Select(state, false);

            Assert.IsFalse(state.SetSelected(false, false));
            Assert.AreSame(state, group.Selected);

            group.AllowNoSelection = true;
            Assert.IsTrue(state.SetSelected(false, false));
            Assert.IsNull(group.Selected);
            Assert.IsFalse(state.IsSelected);
        }

        /// <summary>
        /// 动态改组应注销旧组、恢复旧组后备项并注册到新组。
        /// </summary>
        [Test]
        public void MovingMemberBetweenGroupsKeepsBothGroupsCoherent()
        {
            ButtonStateGroup firstGroup = CreateGroup("FirstGroup");
            ButtonStateGroup secondGroup = CreateGroup("SecondGroup");
            ButtonState moving = CreateState("Moving", firstGroup);
            ButtonState fallback = CreateState("Fallback", firstGroup);
            firstGroup.Select(moving, false);

            moving.SetGroup(secondGroup);

            Assert.AreSame(fallback, firstGroup.Selected);
            Assert.IsFalse(moving.IsSelected);
            Assert.IsTrue(secondGroup.Select(moving, false));
            Assert.AreSame(moving, secondGroup.Selected);
        }

        private ButtonStateGroup CreateGroup(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<ButtonStateGroup>();
        }

        private ButtonState CreateState(
            string objectName,
            ButtonStateGroup group)
        {
            GameObject gameObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            createdObjects.Add(gameObject);
            ButtonState state = gameObject.AddComponent<ButtonState>();
            state.SetGroup(group);
            return state;
        }
    }
}
