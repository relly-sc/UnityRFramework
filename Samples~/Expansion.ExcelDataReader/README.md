# UnityRFramework · Expansion.ExcelDataReader

`Expansion.ExcelDataReader` 是轻量 Excel 导表扩展，可直接读取 `.xlsx` 与 `.xls`，并
复用框架 ConfigPipeline 导出 Config 与 Localization 的 JSON 或二进制文件。

该 Sample 只在 Unity Editor 中运行，不向 Player 引入 Excel 读取逻辑。

## 依赖

Sample 已包含以下 EditorOnly 程序集，无需再通过 NuGet 或 Package Manager 安装：

- `ExcelDataReader.dll`
- `ExcelDataReader.DataSet.dll`

程序集位于 `Plugins/ExcelDataReader`，对应许可证也保存在同一目录。插件 Import
Settings 已限制为 Editor 平台，不会进入运行时程序集。

## Excel 表约定

Config 与 Localization 使用和现有 CSV 工具一致的四行结构：

| 行 | 内容 |
|---|---|
| 第 1 行 | 字段名 |
| 第 2 行 | 字段类型 |
| 第 3 行 | 字段注释 |
| 第 4 行起 | 数据 |

补充规则：

- 字段名以 `!` 开头时忽略整列，`!` 可以出现在任意列。
- Config 必须包含唯一的 `int Id` 字段。
- Localization 使用 `Key`、`Value` 两列，类型均为 `string`。
- 单个工作簿只有一个非空 Sheet 时，逻辑表名使用 Excel 文件名。
- 单个工作簿包含多个非空 Sheet 时，每个 Sheet 使用自己的 Sheet 名。
- 表名、字段名和类型仍需满足 ConfigPipeline 的校验规则。

## 工具窗口

菜单入口：

`UnityRFramework/Expansion/Excel 配置表工具`

工具窗口分别提供 Config 与 Localization 区域，可配置：

- Excel 源目录。
- 导出目录。
- JSON、Binary 或已注册的自定义导出格式。
- Config 行类型与 URFC Codec 代码生成。
- 生成代码目录与可留空的命名空间。
- Localization 多语言 Bundle 导出选项。

生成命名空间留空时，生成的配置代码不声明命名空间。工具设置会被 Project 视图右键
导出入口复用。

## Project 右键入口

选中 Unity 项目内的 Excel 文件或包含 Excel 的文件夹后，可使用：

```text
Assets/UnityRFramework/Excel/Config/按工具所选格式导出
Assets/UnityRFramework/Excel/Config/导出为 JSON
Assets/UnityRFramework/Excel/Config/导出为 Binary
Assets/UnityRFramework/Excel/Localization/按工具所选格式导出
Assets/UnityRFramework/Excel/Localization/导出为 JSON
Assets/UnityRFramework/Excel/Localization/导出为 Binary
```

右键入口使用工具保存的规则和框架固定输出目录，适合日常快速重导；需要临时修改源
目录、输出目录、代码生成或 Localization Bundle 选项时，应打开完整工具窗口。

## 输出格式

- Config JSON：由 `JsonConfigHelper` 读取。
- Config Binary：URFC v2，由 `BinaryConfigHelper` 和生成的 Codec 读取。
- Localization JSON：由 `JsonLocalizationHelper` 读取。
- Localization Binary：单语言 URFL v2；多语言 Bundle 使用 URLM v1。

导表只负责将 Excel 转换为客户端可加载数据，不负责 Excel 文件的版本管理，也不替代
业务数据校验。自定义格式可通过现有 Excel 导出器注册接口扩展，不需要修改工作簿读取
和表结构解析流程。
