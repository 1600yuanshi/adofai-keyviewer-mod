# Agent Key Viewer
*本模组由AI生成！* 

**一句话生成你的按键显示配置。**

Agent Key Viewer 是《冰与火之舞》(A Dance of Fire and Ice) 的 **CheryTools 附属模组**：内置 AI，把自然语言描述直接生成为 CherryTools 可导入的配置包——按键显示用 `.ctkv`，Overlayer 覆盖物用 `.ctov`。游戏内的实际渲染由 CherryTools 完成，本模组专注"生成"这一步。

> 「16K 全键盘，紫色到天蓝渐变边框，按下白色高亮，开启键雨」→ 一键生成 → CT 导入即用

***

## 特性

- **本地 WebUI 界面**：游戏启动后在浏览器打开 `127.0.0.1:8765` 即可完成全部操作（密钥、生成、文件管理），不再依赖 UMM 的 IMGUI 面板
- **描述即配置**：自然语言下单，AI 两轮「生成 + 自检」输出标准配置，无 Markdown 残留
- **支持 CT 重构版（Sonnet）新格式**：默认生成 `KeyViewer.Sonnet.xml` 新包，也可一键切换为旧格式以兼容旧版 CT
- **Overlayer 覆盖物生成**：按描述生成文本 / 进度条组件，产出 `.ctov` 包（内置 CT 官方 66 个 token，如 `{cbpm}`、`{progress:2}`、`{xacc:2}`、`{fm}`）
- **两种生成模式可切换**：
  - **AI 输出 JSON 规格**（推荐）：AI 只描述意图，由代码确定性地构建 XML，字段更不容易出错
  - **AI 直写 XML**：由 AI 直接输出配置 XML，代码再做兜底修正
- **任意键位**：默认 16K 键位

```
tab 1 2 e p = backspace \
Lshift Lctrl space c enter Rshift , k
```

- 支持 4K/8K/12K/16K 自动居中节选，也支持自定义
- **键雨逐键变色**：第一排键雨自动跟随各键边框色（红键红雨、蓝键蓝雨），第二排固定白色且更细，上下两排起始高度几何精确对齐
- **显示模式混合**：每个键可在 键 / KPS / Total / 图片 四种模式间切换；KPS 默认居左且数值左对齐，Total 居右且数值右对齐
- **细节兜底修正**：两排键雨偏移、逐键雨色、数值对齐等 AI 容易漏填的字段由代码在构建期强制补正，开箱即用
- **外观定制**：圆角边框、键背景图片（JPG/PNG/GIF）、透明背景、按下白色高亮
- **旧包修复**：文件管理页提供「修正键雨」按钮，一键就地升级旧配置（新旧两种包都支持）

## 安装

1. 安装 [Unity Mod Manager](https://github.com/newman55/unity-mod-manager)（UMM）
2. 安装 [CheryTools](https://github.com/adofaiex/CheryTools) 并确认其可正常显示 KV
3. 将 `ADOFAI.AgentKeyViewer-v{版本}.zip` 放入游戏目录的 `Mods/` 下解压（或用 UMM 安装）
4. 启动游戏，在 UMM 设置中展开 **Agent Key Viewer**，点击「在浏览器中打开 WebUI」

> 安装后 `Mods/ADOFAI.AgentKeyViewer/` 下会有两个 DLL：`ADOFAI.AgentKeyViewer.dll`（引导器）与 `AgentKeyViewer.Core.dll`（核心），两者都要在，不要只复制其中一个。

## 游戏内热更新

模组拆成「引导器 + 核心」两个程序集，目的是**改代码不必重启游戏**：

| 程序集 | 作用 | 能否热更新 |
| --- | --- | --- |
| `ADOFAI.AgentKeyViewer.dll` | UMM 入口：注册回调、加载核心、每帧驱动 | 改动它需重启游戏 |
| `AgentKeyViewer.Core.dll` | 全部功能逻辑 | **可直接热替换，无需重启** |

WebUI 的「更新」页提供：

- **检查更新**：比对 GitHub Release 的最新版本号
- **下载更新** → **应用并热生效**：覆盖核心文件并立即切换到新版本；若引导器也有改动会提示需重启
- **手动热重载核心**：本地改完代码重新编译后点一下即生效，不必重启游戏

另外，**提示词与供应商预设也是热重载的**：提示词在 `AgentKeyViewer_config/prompts/*.txt`，预设在同目录的 `presets.json`，改完下一次生成即生效。

## 使用步骤

1. **打开 WebUI**：启动游戏后，在 UMM 的 Agent Key Viewer 面板点「🌐 在浏览器中打开 WebUI」；地址形如 `http://127.0.0.1:8765/?t=令牌`，令牌每次启动随机生成
2. **配置密钥**：在「密钥管理」页选择供应商预设并粘贴 API Key（没有密钥时先添加）
3. **描述需求**：在「生成 .ctkv」页写一句话，选择生成模式与目标格式
4. **生成并保存**：点击「生成配置」，成功后填写文件名保存
5. **CT 导入**：打开 CherryTools → KeyViewer 设置 → 导入配置包，选中保存的 `.ctkv`
6. **Overlayer 覆盖物**：见下方「OV 覆盖物怎么导入」

## OV 覆盖物怎么导入（重要）

CT 当前版本的「**导入 .ctov**」有一个 Bug：Overlayer 模块读取包时用到 `XmlReaderSettings.DtdProcessing`，该类型在当前 Unity 运行时解析不到，会报
`Could not resolve type with token 010000a3 from typeref (expected class 'System.Xml.DtdProcessing' ...)`。
这个失败发生在读取内容之前，所以 **CT 自己导出的 `.ctov` 也一样导入失败**，改包内容无法绕过（该模块导出正常、导入必挂）。

本模组提供绕过路径：

1. 生成 OV 配置后，点「**导出为 CT 设置文件**」
2. **完全关闭游戏**（CT 把 Overlayer 设置常驻内存并在关闭时回写，必须在关闭后替换）
3. 把导出的文件复制并改名覆盖到：
   `Mods/CheryTools/Modules/CheryTools.Overlayer.Preview.xml`
4. 重新启动游戏，覆盖物即会显示

> 若你更新了 CT 且它已修复该导入 Bug，则可直接用「保存为 .ctov」+ CT 的导入按钮。
> 注：KeyViewer 的 `.ctkv` 导入不受此 Bug 影响。

## 支持的 AI 供应商

| 供应商        | Base URL                       | 说明                                                       |
| ---------- | ------------------------------ | -------------------------------------------------------- |
| DeepSeek   | `https://api.deepseek.com`     | 关闭思考，maxTokens=16384                                     |
| 通义千问 Qwen  | 阿里百炼 compatible-mode           | 关闭思考                                                     |
| TokenRa 网关 | `https://tokenra.io/v1`        | 11 个模型可选（GLM-5.x、deepseek-v4-flash、kimi-k3、MiniMax-M3 等） |
| OpenRouter | `https://openrouter.ai/api/v1` | 通用，自填模型                                                  |
| 自定义        | 手动填写                           | 任意 OpenAI 兼容端点                                           |

密钥保存在本地磁盘，重启不丢失。

## 文件位置

```
<游戏目录>/
└── AgentKeyViewer_config/
    ├── apikeys.json        # API 密钥与供应商设置
    ├── presets.json        # 供应商预设（可热重载）
    ├── prompts/            # 提示词（可热重载，每个键一个 .txt）
    ├── ctkv/               # 生成的 .ctkv 键位配置包与 .ctov 覆盖物包
    ├── update/             # 更新包下载与解包目录
    ├── images/             # 键背景图片（JPG/PNG/GIF）
    └── logs/               # AI 请求/响应日志（含错误详情）
```

## 常见问题

- **WebUI 打不开**：确认 UMM 面板显示「WebUI 运行中」；端口默认 8765，被占用会自动顺延，以面板显示为准。若浏览器提示令牌无效，请带上面板中的完整地址重新打开
- **生成失败 / 403 Forbidden**：检查 API Key 与供应商是否匹配、是否有速率限制；详情看 `logs/`
- **输出变成思维链导致解析失败**：保持「关闭模型思考」勾选（默认开启），内置了对各供应商的思考关闭参数
- **新格式导入失败**：CT 26.5+ 才能导入新格式包；旧版 CT 请在生成时把「目标格式」切换为「旧格式」
- **更新后没生效**：若更新只改了核心，应用后会立即热生效；若提示「引导器有改动」，则需完全重启游戏（UMM 只在启动时加载引导器）
- **热重载后 WebUI 打不开**：热重载会重启 WebUI 服务并更换访问令牌，请回 UMM 面板取新地址
- **更新检查失败**：需要能访问 `api.github.com`；若网络受限可手动下载 zip 覆盖 `Mods/ADOFAI.AgentKeyViewer/` 下的两个 DLL
- **旧生成的配置键雨颜色单一/偏移不对**：在「文件管理」页对该文件点「修正键雨」即可
- **推荐模型**：DeepSeek 或 TokenRa 网关下的较大模型；过小的模型可能截断长输出

## 版本历史

见 [CHANGELOG.md](CHANGELOG.md)。当前版本功能概要：

- **2.1.0** 游戏内热更新：引导器/核心拆分，核心可热替换不重启；WebUI「更新」页支持检查/下载/应用；提示词与供应商预设热重载
- **2.0.0** 支持 CT 重构版（Sonnet）新包格式与 Overlayer `.ctov` 生成；新增本地 WebUI 取代 UMM 面板；双生成模式可切换；适配 ADOFAI r150；可选 CT 面板模块
- **1.4.0** KPS 左/Total 右摆位 + 数值对齐（CountTextAlignment 兜底）
- **1.3.0** 界面精简重构，内置使用说明
- **1.2.0** 键雨逐键变色、「修正键雨」就地修复按钮
- **1.0\~1.1** .ctkv 双向导入导出、圆角/图片/GIF、TokenRa 多模型、思考关闭、密钥持久化

***

*本模组为 Chery Tools 的附属生成器，仅负责配置生成，不参与游戏内渲染。*
