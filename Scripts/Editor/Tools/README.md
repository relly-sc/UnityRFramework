# UnityRFramework Editor Tools

本目录提供零第三方依赖的通用 Unity Editor 工具。工具只在用户显式点击菜单后执行，
不会在编辑器启动、进入 Play Mode 或资源变化时自动修改工程。

## 工具列表

| 工具 | 菜单 | 操作范围 |
|---|---|---|
| 子物体自然排序 | `GameObject/UnityRFramework/子物体排序` | 所选对象的直接子节点，支持 Undo |
| 批量重命名 | `Assets/UnityRFramework/批量重命名`、`GameObject/UnityRFramework/批量重命名` | 当前选择的主资源或场景 GameObject；支持首尾字符删除、查找替换、模板、前后缀和序号，可批量或逐项执行 |
| Missing Script 清理 | `Assets/UnityRFramework/Missing Script 清理`、`GameObject/UnityRFramework/Missing Script 清理` | 当前选择的场景层级、Prefab 或文件夹中的 Prefab |
| 纹理批量设置 | `Assets/UnityRFramework/纹理批量设置` | 当前选择的纹理或文件夹内纹理，仅重导设置发生变化的资源 |
| UGUI 字体替换 | `Assets/UnityRFramework/UGUI 字体替换`、`GameObject/UnityRFramework/UGUI 字体替换` | 当前选择的场景层级、Prefab 或文件夹中的 Prefab |
| 名称同步到 UGUI Text | `GameObject/UnityRFramework/名称同步到子级 Text` | 所选场景对象及其子级；Button 文本采用按钮 GameObject 名称，支持 Undo |
| 批量创建文件夹 | `Assets/UnityRFramework/批量创建文件夹` | 选中文件夹时在其内部创建；选中文件时在同级目录创建；无选择时回退到 `Assets` |
| UTF-8 C# 脚本生成 | `Assets/UnityRFramework/创建 UTF-8 C# 脚本` | 在选中文件夹或文件同级目录创建 UTF-8 无 BOM、LF 的 class/struct/interface/enum；窗口实时跟随 Project 选择，class 可选继承 MonoBehaviour 或自定义父类 |

依赖 Project 或 Hierarchy 当前选择的工具只放在各自的
`Assets/UnityRFramework` 或 `GameObject/UnityRFramework` 右键菜单中。
只有全局扫描、编辑器设置、功能设置或不属于这两个区域的工具才放在顶部
`UnityRFramework` 菜单。

每个编辑器工具默认只保留一个最贴合其操作范围的菜单入口。只有工具确实需要分别消费
Project 与 Hierarchy 两套选择上下文，或维护者明确要求时，才允许为同一工具提供多个入口。

## 安全边界

- 场景对象修改使用 Unity Undo。
- Prefab 修改通过 `PrefabUtility.LoadPrefabContents` 完成；保存后的 Prefab 资产修改
  不属于场景 Undo，执行前会明确确认。
- Missing Script 不扫描所有已加载对象，只处理当前选择。
- 批量重命名会校验资源非法名称、现有资源冲突和同批次资源冲突；Hierarchy
  遵循 Unity 规则允许同名。
- 文件夹路径必须位于 Assets 内，拒绝绝对路径、`.`、`..` 和非法名称。
- 字体工具只替换 UGUI `Text.font`，不修改字号、文本和布局。
- 工具脚本统一使用 `UnityRFramework.Editor` 命名空间，不依赖 Runtime Module。
