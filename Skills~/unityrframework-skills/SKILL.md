---
name: unityrframework-skills
description: 在已安装 UnityRFramework 的 Unity 项目中实现业务功能或排查接入问题时使用，指导 AI 选择模块、遵守业务脚本与生命周期规范，并从当前安装版本的文档和源码核对 API。适用于设置、存档、配置、本地化、资源、UI、实体、音频、流程、网络、广告和可选扩展；不用于普通 Unity 项目或框架仓库的发布维护。
---

# UnityRFramework Skills

指导 AI 在使用 UnityRFramework 的项目中开发业务。不要凭其他框架的同名 API 猜实现，也不要把开发仓库中所有 Samples、插件和测试当成消费项目的必备内容。

## 开始工作

1. 确认当前核心包的版本和真实位置，以及项目实际导入的 Samples、第三方包、asmdef、启动场景和 Helper。查找方式见 [API 查询](references/API查询.md)。
2. 根据业务需求选择现有模块，阅读下面路由对应的章节；只加载本次相关参考，不必通读所有模块。
3. 遵循 [业务脚本规范](references/业务脚本规范.md)，核对当前 Component 的真实签名、失败行为和资源所有权，再实现项目自己的业务代码。
4. 按改动风险验证并报告实际结果。静态核对、编译、Editor 运行、Player 运行、真机验收不是同一层证据；没有执行就明确说明。

框架未安装、API 不存在或扩展缺失时，指出具体缺口；不要编造接口或默认安装全部依赖。消费项目的问题优先在项目内解决，不擅自修改只读框架包。

## 按需求路由

| 需求 | 阅读位置 |
| --- | --- |
| 音量、语言、分辨率等少量设置 | [Setting](references/核心模块.md#setting)；持久化不等于应用设置。 |
| 进度存档、手动槽位、自动存档 | [Storage](references/核心模块.md#storage)；与 Setting、Config 分开。 |
| 配置表、CSV/Excel、二进制和加密 | [Config](references/核心模块.md#config)，需要 Excel 时再读可选扩展。 |
| 静态 UGUI/TMP 多语言、动态文本、语言切换 | [Localization](references/核心模块.md#localization)。 |
| Prefab、图片、本地文件、资源更新 | [Resource](references/核心模块.md#resource)；更新能力再读 YooAsset 扩展。 |
| 窗口、场景 HUD、层级与独立 Canvas | [UI](references/核心模块.md#ui)；绑定、红点、列表等再读 UI 扩展。 |
| 角色、可复用场景实体、临时对象池 | [Entity](references/核心模块.md#entity) 与 [Pool](references/核心模块.md#pool)，先明确所有权。 |
| 音频、事件、计时、状态机、应用流程、场景、通信 | [核心模块](references/核心模块.md) 中对应章节。 |
| 广告或简单数值防护 | [Advertisement](references/核心模块.md#advertisement) 或 [基础安全](references/核心模块.md#基础安全)，不夸大默认能力。 |
| 构建 Player/资源/热更，处理 Profile 和报告 | [构建工具](references/构建工具.md)。 |
| YooAsset、HybridCLR、Obfuz、Excel、UI 工具、R3、平台密钥仓 | [可选扩展](references/可选扩展.md)，再看当前导入扩展的 README 和源码。 |
| 设置界面、读档、加载界面等跨模块业务 | [业务示例](references/业务示例.md)，地址和参数由项目提供。 |
| 查方法签名、生命周期、Helper、示例或测试 | [API 查询](references/API查询.md)，以当前安装源码为事实源。 |

## 不能越过的边界

- 优先通过 `UnityRFramework.Runtime.GameEntry` 使用模块；只有需要扩展或测试时才进入 Library 契约。不要在业务脚本中另建一套模块或重复放置框架根节点。
- 尊重项目现有业务架构。MVC、MVP、MVVM、独立 MonoBehaviour 都可以；技能只约束框架接入，不强制业务模式、全局单例或服务层。
- Helper 决定解析方式、资源来源和地址。不要将 Resources 路径规则直接套给 LocalFile 或 YooAsset，不把 Config 格式、通用 JSON Helper 和 Bundle 加密混为一谈。
- 不擅自切换 Helper、启用加密、生成/更换密钥、安装第三方包、保存未保存场景或改写项目资产。用户可在面板安全调整的值，先说明位置与建议；明确授权、批量迁移或必要自动验证时再代为修改。
- 广告默认只有空实现；真实平台 SDK 尚未接入。网络参考 Helper 不等于生产协议实现。简单数值和本地加密不等于专业反破解或服务器权威校验。
- 本技能是开发指引，不是执行工具。若 Unity Skills 与 Unity 官方技能都可用，项目操作优先 Unity Skills；YooAsset、HybridCLR 和框架操作使用 Unity Skills，官方技能仅辅助基础 API/最佳实践查询。缺少编辑器工具时可以做源码工作或提供手动步骤，不自动安装工具，也不声称已执行编辑器操作。
