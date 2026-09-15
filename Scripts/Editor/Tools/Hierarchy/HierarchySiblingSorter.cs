using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 对所选 GameObject 的直接子节点按名称进行自然排序。
    /// </summary>
    internal static class HierarchySiblingSorter
    {
        private const string AscendingMenu =
            "GameObject/UnityRFramework/子物体排序/名称升序";
        private const string DescendingMenu =
            "GameObject/UnityRFramework/子物体排序/名称降序";

        /// <summary>
        /// 按名称升序排列直接子节点。
        /// </summary>
        [MenuItem(AscendingMenu, false, -999)]
        private static void SortAscending()
        {
            Sort(false);
        }

        /// <summary>
        /// 判断当前选择是否可排序。
        /// </summary>
        /// <returns>至少一个所选对象拥有两个直接子节点时返回 true。</returns>
        [MenuItem(AscendingMenu, true)]
        private static bool ValidateSortAscending()
        {
            return CanSortSelection();
        }

        /// <summary>
        /// 按名称降序排列直接子节点。
        /// </summary>
        [MenuItem(DescendingMenu, false, -998)]
        private static void SortDescending()
        {
            Sort(true);
        }

        /// <summary>
        /// 判断当前选择是否可排序。
        /// </summary>
        /// <returns>至少一个所选对象拥有两个直接子节点时返回 true。</returns>
        [MenuItem(DescendingMenu, true)]
        private static bool ValidateSortDescending()
        {
            return CanSortSelection();
        }

        private static void Sort(bool descending)
        {
            GameObject[] selected = Selection.gameObjects;
            HashSet<int> processed = new HashSet<int>();
            for (int selectionIndex = 0;
                 selectionIndex < selected.Length;
                 selectionIndex++)
            {
                Transform parent = selected[selectionIndex]?.transform;
                if (parent == null
                    || parent.childCount < 2
                    || !processed.Add(parent.GetInstanceID()))
                {
                    continue;
                }

                List<ChildEntry> children =
                    new List<ChildEntry>(parent.childCount);
                UnityEngine.Object[] undoTargets =
                    new UnityEngine.Object[parent.childCount];
                for (int childIndex = 0;
                     childIndex < parent.childCount;
                     childIndex++)
                {
                    Transform child = parent.GetChild(childIndex);
                    children.Add(new ChildEntry(child, childIndex));
                    undoTargets[childIndex] = child;
                }

                children.Sort((left, right) =>
                {
                    int result = NaturalNameComparer.Compare(
                        left.Transform.name, right.Transform.name);
                    if (result != 0)
                    {
                        return descending ? -result : result;
                    }

                    return left.OriginalIndex.CompareTo(
                        right.OriginalIndex);
                });

                Undo.RecordObjects(
                    undoTargets,
                    descending
                        ? "Sort Children Descending"
                        : "Sort Children Ascending");
                for (int childIndex = 0;
                     childIndex < children.Count;
                     childIndex++)
                {
                    children[childIndex].Transform.SetSiblingIndex(childIndex);
                }

                EditorUtility.SetDirty(parent);
            }
        }

        private static bool CanSortSelection()
        {
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] != null
                    && selected[i].transform.childCount >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct ChildEntry
        {
            internal ChildEntry(Transform transform, int originalIndex)
            {
                Transform = transform;
                OriginalIndex = originalIndex;
            }

            internal Transform Transform { get; }

            internal int OriginalIndex { get; }
        }

        private static class NaturalNameComparer
        {
            internal static int Compare(string left, string right)
            {
                left = left ?? string.Empty;
                right = right ?? string.Empty;
                int leftIndex = 0;
                int rightIndex = 0;
                while (leftIndex < left.Length
                    && rightIndex < right.Length)
                {
                    char leftCharacter = left[leftIndex];
                    char rightCharacter = right[rightIndex];
                    if (char.IsDigit(leftCharacter)
                        && char.IsDigit(rightCharacter))
                    {
                        int numberResult = CompareNumber(
                            left,
                            ref leftIndex,
                            right,
                            ref rightIndex);
                        if (numberResult != 0)
                        {
                            return numberResult;
                        }

                        continue;
                    }

                    int characterResult = char.ToUpperInvariant(leftCharacter)
                        .CompareTo(char.ToUpperInvariant(rightCharacter));
                    if (characterResult != 0)
                    {
                        return characterResult;
                    }

                    leftIndex++;
                    rightIndex++;
                }

                return left.Length.CompareTo(right.Length);
            }

            private static int CompareNumber(
                string left,
                ref int leftIndex,
                string right,
                ref int rightIndex)
            {
                int leftStart = leftIndex;
                int rightStart = rightIndex;
                while (leftIndex < left.Length
                    && char.IsDigit(left[leftIndex]))
                {
                    leftIndex++;
                }

                while (rightIndex < right.Length
                    && char.IsDigit(right[rightIndex]))
                {
                    rightIndex++;
                }

                int leftSignificant = leftStart;
                int rightSignificant = rightStart;
                while (leftSignificant < leftIndex - 1
                    && left[leftSignificant] == '0')
                {
                    leftSignificant++;
                }

                while (rightSignificant < rightIndex - 1
                    && right[rightSignificant] == '0')
                {
                    rightSignificant++;
                }

                int leftLength = leftIndex - leftSignificant;
                int rightLength = rightIndex - rightSignificant;
                if (leftLength != rightLength)
                {
                    return leftLength.CompareTo(rightLength);
                }

                for (int i = 0; i < leftLength; i++)
                {
                    int result = left[leftSignificant + i]
                        .CompareTo(right[rightSignificant + i]);
                    if (result != 0)
                    {
                        return result;
                    }
                }

                return (leftIndex - leftStart)
                    .CompareTo(rightIndex - rightStart);
            }
        }
    }
}
