# UnityRFramework · Expansion

`Expansion` 用于承载 UnityRFramework 的可选第三方插件适配代码。核心框架不依赖这些插件；只有导入本 Sample 并安装对应依赖后，相关 Helper 才参与编译和运行。

## 定位与边界

| 位置 | 职责 | 第三方依赖 |
|---|---|---|
| `Library` / `Runtime` | 框架接口、默认实现和基础运行能力 | 禁止依赖 |
| `Samples/Expansion` | 第三方 Helper、适配器和配套工具 | 允许按功能引入 |
| `Samples/ExpansionDemo` | 第三方接入的可运行验收示例 | 允许依赖 Expansion |

Expansion 只负责适配，不把第三方插件的类型或生命周期反向扩散到核心框架。未导入 Expansion 时，框架必须仍可正常编译、启动、关闭和重启。

## 当前状态

| 集成 | 当前实现 | 状态 |
|---|---|---|
| YooAsset | `Scripts/Runtime/Resource/YooAssetResourceHelper.cs` | 阶段 1 EditorSimulate 验收完成 |
| UniTask | `Scripts/Runtime/WebRequest/UniTaskWebRequestHelper.cs` | 阶段 1 验收完成 |
| Excel 解析扩展 | 尚未实现 | 阶段 2 |
| Luban | 尚未实现 | 阶段 3 |
| MemoryPack | 尚未实现 | 阶段 4 |
| HybridCLR | 尚未实现 | 阶段 5 |

2026-07-27 已通过 ExpansionDemo 完成 EditorSimulate 端到端验收：二进制
`TextAsset` 加载与卸载、Additive 场景加载与卸载、Web 成功请求、主动取消，
以及不退出进程的框架关闭和重启后重复执行。Offline 与 Host 已实现初始化配置，
仍需在生成实际 YooAsset 包、配置内置文件或远端地址后做对应环境验收。

## 推荐实施顺序

### 阶段 1：复核 YooAsset 与 UniTask

先收口现有适配，避免在未验证的基础上继续增加第三方依赖。

工作内容：

1. 按项目当前安装版本核对 YooAsset、UniTask API 和生命周期。
2. 验证 Helper 初始化、正常请求、取消、异常和关闭行为。
3. 完善 `ExpansionDemo`，覆盖框架启动、资源加载、场景切换、Web 请求、关闭和不退出进程的重启。
4. 确认第三方模块失败时不会破坏框架其他模块的运行与关闭。

完成标准：

- Expansion 与 ExpansionDemo 编译无错误。
- YooAsset 的 EditorSimulate、Offline、Host 中实际支持的模式均有明确配置和验收结果。
- UniTask Web 请求的成功、失败、取消和 Shutdown 路径均可结束，不遗留悬空任务。
- ExpansionDemo 可完成一次“启动 → 使用 → 关闭 → 重启 → 再次使用”。

### 阶段 2：接入轻量 Excel 解析扩展

在 Luban 前先提供一条更容易理解和维护的 Excel 直出管线。该扩展只负责在 Editor 中读取 `.xlsx`，然后复用现有 ConfigPipeline 生成 JSON、URFC/URFM、URFL/URLM 和配置代码；Runtime 不直接读取 Excel，也不依赖 EPPlus、NPOI 等库。

Excel 内容继续遵循 Demo 现有约定，不另建一套规则：

- Config 第一行为字段名，第二行为字段类型，第三行为字段注释，第四行开始为数据，并包含唯一的 `int Id` 字段。
- Localization 第一行为 `Key,Value`，第二行为 `string,string`，第三行为字段注释，第四行开始为数据。
- 字段类型、枚举、数组、`List<T>`、自定义 Codec、同类型分片和重复 ID/Key 校验均沿用现有 ConfigPipeline 规则。
- 工作表名称用于确定逻辑表或分片；具体映射必须保持与现有 `表名@分片名` 语义一致。

工作内容：

1. 在 Expansion 的 Editor 侧定义最小 Excel 工作簿读取抽象，使 EPPlus、NPOI 或其他解析器可以替换。
2. 先选择一个解析器实现 `.xlsx` 读取，不让其类型进入 Runtime 或 Library。
3. 将单元格转换成现有 CSV 等价中间模型，复用 Schema 解析、校验、代码生成、JSON 和二进制导出逻辑。
4. 支持 Config、Localization、多 Sheet、同类型分片及明确的空单元格处理。
5. 公式单元格第一版只读取文件中已保存的计算结果，不自行实现 Excel 公式计算引擎；缺少缓存结果时应明确报错。
6. 在 ExpansionDemo 中验证 Excel 直出产物与 CSV 管线的运行时读取结果一致。

完成标准：用户无需先手动转 CSV，即可从符合现有规则的 `.xlsx` 生成同等 JSON/二进制产物；移除该 Expansion 后，默认 CSV 管线仍可独立工作。

这条轻量管线适合规则固定、表结构简单的项目。需要多种表定义方式、复杂引用、自动代码生成规则或大型配置生产体系时，再使用 Luban。

### 阶段 3：接入 Luban

Luban 作为配置生产管线扩展，不替换框架默认的 JSON/URFC 二进制实现。

工作内容：

1. 建立 Excel 到 Luban 输出文件的 Editor 侧转换流程。
2. 实现 Luban 配置 Helper 或适配层，映射到现有 Config 模块接口。
3. 支持 Luban 的 JSON 和二进制输出，并验证单表、分表合并及 Bundle 加载。
4. 补齐 IL2CPP、泛型保留和 `link.xml` 需求。
5. 在 ExpansionDemo 中独立验证加载、查询、重复 ID、格式错误、关闭和重启。

完成标准：Luban 生成、加载、解析、缓存和重启链路均可运行，且不修改核心 Config 接口来迁就 Luban 私有类型。

### 阶段 4：接入 MemoryPack

MemoryPack 作为可选的高性能序列化方案，用于配置或网络数据；不替换框架默认 URFC 格式，也不成为核心框架必需依赖。

工作内容：

1. 明确 MemoryPack 在 Config、Network 中各自的适用边界。
2. 实现独立序列化适配器及必要的代码生成流程。
3. 验证版本兼容、无效数据、AOT/IL2CPP、取消、关闭和重启。
4. 用 ExpansionDemo 对比默认实现与 MemoryPack 实现的行为一致性。

完成标准：未安装 MemoryPack 时核心框架不受影响；安装后可通过显式配置启用，并通过目标平台构建验证。

### 阶段 5：接入 HybridCLR

HybridCLR 涉及程序集划分、AOT 泛型补充、热更新 DLL、资源交付和平台构建，放在其他适配稳定后实施。

工作内容：

1. 确定热更新程序集边界和依赖方向。
2. 接入补充元数据、热更新 DLL 加载及版本校验。
3. 通过 Resource Helper 获取 DLL，不让核心框架直接依赖具体资源后端。
4. 验证 Editor、目标真机、IL2CPP、更新失败回退、关闭和重启。

完成标准：热更新链路在目标平台可构建、可运行、可失败回退，且不破坏未启用 HybridCLR 的默认运行路径。

## 通用验收规则

每个第三方适配都必须满足以下规则：

1. 未导入对应 Sample 或未启用对应 Helper 时，核心框架保持零第三方依赖。
2. 依赖缺失时给出清晰的导入说明，不能让错误扩散为核心框架故障。
3. Helper 的初始化、更新、异常、取消、Shutdown 和 Restart 语义与框架模块契约一致。
4. 不在 Library 层直接输出日志；Library 通过 `RFrameworkException` 表达错误，Runtime/Expansion 的运行日志通过 Runtime `Log` 输出。
5. 公开配置需在 Inspector 或文档中说明用途、默认值和生效条件。
6. 至少通过一次 ExpansionDemo 端到端运行；涉及 AOT 的集成还必须通过目标平台构建和真机验证。

## 当前 Helper 使用方式

1. 在 Package Manager 中导入 **Expansion** Sample。
2. 手动安装所需第三方包。UPM 不会根据 Sample 内容自动安装可选依赖。
3. 在 `UnityRFramework` 预制体 Inspector 中配置对应 Helper：
   - Resource Helper：`UnityRFramework.Expansion.YooAssetResourceHelper`
   - Web Request Helper：`UnityRFramework.Expansion.UniTaskWebRequestHelper`
4. 按 Helper 的 Inspector 配置初始化参数，再通过 ExpansionDemo 验证，不要直接用正式业务场景代替首次验收。

当前开发工程已安装 YooAsset 与 UniTask。MemoryPack 当前未安装，仅保留为阶段 4
的可选规划；每个 Helper 只应依赖自己实际使用的插件。

YooAsset v3 的 EditorSimulate 单包只能使用一种虚拟 Bundle 类型。当前
`YooAssetResourceHelper` 面向同时包含 Prefab、场景、JSON 和二进制配置的普通
资源包，因此 `byte[]` 与 `string` 从普通 Bundle 内的 `TextAsset` 转换，不按
RawFile 规则收集。需要直接访问视频等原生文件时，应使用独立 RawFile 包及专用
适配，不要与当前混合资源包共用同一包配置。

## Sample 关系

```text
Samples/
├── Expansion/         第三方 Helper 与工具，本目录，不单独运行
├── Demo/              使用框架默认实现的完整示例
└── ExpansionDemo/     使用 Expansion 实现的可运行验收示例
```

Demo 与 ExpansionDemo 应覆盖相同的主要业务链路：前者验证默认实现，后者验证第三方实现。两者不得通过复制核心框架代码形成两套行为不一致的实现。

## 开发与发布约定

开发阶段使用 `Samples/`，让 Unity 随宿主工程编译并便于调试；发布 UPM 包时使用 `Samples~/`，由 Package Manager 按 `package.json` 的 `samples` 声明导入。

本开发工程只维护 `Samples/` 下的内容；`Samples~/` 发布副本由维护者在发布前
手动全量同步。新增第三方集成时，应更新本文件的状态、依赖、配置方式和验收结果。
