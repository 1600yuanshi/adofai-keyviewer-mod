# 更新日志

## 2.1.0（2026-09-24）
### 新增
- **游戏内热更新（架构拆分）**：模组拆成两个程序集——
  - `ADOFAI.AgentKeyViewer.dll`（**引导器**）：UMM 入口，只负责注册回调、加载核心、每帧驱动，刻意保持极小且稳定；
  - `AgentKeyViewer.Core.dll`（**核心**）：全部功能逻辑，可被热替换。
  由于 UMM/Mono 无法卸载已加载的程序集，核心改用 `Assembly.Load(字节)` 加载（不走路径缓存），因此替换磁盘上的同名 DLL 后可在**游戏内直接切换到新版本，不需要重启游戏**。只有引导器本身改动时才需要重启。
- **WebUI 新增「更新」页**：一键检查 GitHub Release 是否有新版本、下载更新包、应用并热生效；并显示当前/最新版本、已热更新次数。更新只改核心时即时生效；若引导器也有改动会明确提示需要重启。
- **手动热重载核心**：本地改完代码重新编译后，点一下即可让新核心生效，不必重启游戏（重载会重启 WebUI 服务并更换访问令牌）。
- **提示词与供应商预设热重载**：提示词外置为 `<游戏目录>/AgentKeyViewer_config/prompts/<键名>.txt`（首次运行自动播种），供应商预设外置为 `presets.json`。可直接用编辑器改文件，或在 WebUI 新增的「提示词」页里在线编辑/保存/恢复默认；按文件时间戳自动重读，改完**下一次生成即生效**，不需要重启游戏。

### 变更
- `Info.json` 入口改为 `ADOFAI.AgentKeyViewer.Bootstrap.BootstrapMain.Load`。
- 打包产物由单个 DLL 变为两个 DLL（引导器 + 核心）；`build.bat` 与 `package.ps1` 已同步更新。
- CherryTools 面板模块改为只引用引导器并通过 `BootstrapMain.Core` 取当前核心，避免核心热替换后指向旧实例。

### 已知问题与绕过
- **CT 的「导入 .ctov」在当前 Unity 运行时必定失败（CT 侧 Bug，非本模组问题）**：Overlayer 模块 `OvPackageService.DeserializeEntry` 使用
  `XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, ... })`，
  而 `System.Xml.DtdProcessing` 在该运行时解析不到，报 `Could not resolve type with token 010000a3 from typeref`。
  该构造发生在读取任何内容之前，因此**与包内容无关**，CT 自己导出的 `.ctov` 同样导入失败，改编码/加 BOM/改元素均无法绕过。
  （导出走 `serializer.Serialize(stream)`，所以导出正常——导出/导入路径不对称。）
- **本模组提供的绕过路径**：生成 OV 配置后点「**导出为 CT 设置文件**」，然后**完全关闭游戏**，把导出的文件复制并改名覆盖到
  `Mods/CheryTools/Modules/CheryTools.Overlayer.Preview.xml`，再启动游戏即可看到覆盖物。
  之所以必须关闭游戏：CT 把 Overlayer 设置常驻内存并在关闭时回写，运行时替换会被覆盖。
  CT 的设置读取走 `XmlSerializer` 的 Stream 重载，不受该 Bug 影响。
- 注：CT 的 **KeyViewer 模块导入 `.ctkv` 不受影响**（`KvPackageService.Read` 用的是 `Deserialize(stream)`）。

## 2.0.0（2026-09-24）
### 适配 CherryTools 重构版（Sonnet 26.5+）与 ADOFAI r150
- **新增 Sonnet 包格式支持**：CT 重构后 KV 包清单由 `KeyViewer.xml` 改为 `KeyViewer.Sonnet.xml`，根元素由 `<KeyViewerPackage>` 改为 `<CheryToolsSonnetKeyViewer>`，节点模型由 `KVNode` 换为全新的 `KvProfile`/`KvKey`。本模组现在可生成该新格式，旧格式仍可切换导出以兼容旧版 CT。
- **坐标与字段映射按 CT 官方转换逻辑 1:1 镜像**：新格式 `KvKey.PositionX/Y` 是按键**中心**且 **Y 轴取反**（`PositionX = x + w/2`、`PositionY = -(y + h/2)`）；键雨行号语义由旧格式的 `1/0` 变为 **`1=上排 / 2=下排`**；键雨偏移量同值直传不取反。
- **两排键雨对齐改为构建期计算**：不再依赖「生成后拿 XmlDocument 打补丁」，而是在构建时按几何公式算好 `RainYOffset`/`RainYOffset2`，同时强制写入逐键 `UseCustomRain`/`RainColor`（上排跟随各自边框色、下排白色且更细）与 `CountAlignment`（KPS 左对齐 0 / Total 右对齐 2）。
- **适配 ADOFAI r150**：对 r150 的 Managed 程序集重新编译验证通过（模组只依赖 UnityModManager + UnityEngine，无游戏类型依赖）。
- **适配 CT 新 UI 环境**：CT 已从 UMM IMGUI 改为 ImGui 模块化框架，新增可选的 `CheryTools.AgentKeyViewer.dll` 模块，可直接挂进 CT 面板（`package.ps1 -WithCtModule` 打包，产物需复制到 `Mods/CheryTools/Modules/`）。

### 新增
- **本地 WebUI（取代原 UMM IMGUI 面板）**：游戏启动后在浏览器打开 `127.0.0.1:8765`（端口占用自动顺延），提供密钥管理、生成 `.ctkv` / `.ctov`、文件管理（预览/下载/修正键雨/删除）。仅绑定回环地址，每次启动生成随机访问令牌，未带令牌的请求返回 403。
- **Overlayer(OV) 生成**：新增按自然语言生成 Overlayer 覆盖物配置的能力，产出 CT Sonnet Overlayer 模块可导入的 `.ctov` 包（清单 `Overlayer.Sonnet.xml`）。支持文本组件（含 66 个官方 token 目录，如 `{cbpm}`、`{progress:2}`、`{xacc:2}`、`{fm}`）与进度条组件。
- **双生成模式可切换**：
  - `AI 输出 JSON 规格`（推荐）：AI 只描述意图（键位/hex 颜色/布局/键雨），由代码确定性地构建 XML，避免新格式 ~150 个字段被 AI 填错；
  - `AI 直写 XML`：保留原有模式，Sonnet 直写时会先反序列化为对象做键雨兜底再重新序列化。
- 新增默认 **16K 布局预设**（上排 `Tab 1 2 E P = Backspace \`，下排 `LShift LCtrl Space C Enter RShift , K`）。

### 变更
- UMM 面板精简为「WebUI 状态 + 打开按钮 + 生成状态速览」，原 IMGUI 生成/文件管理界面已停用（功能迁移至 WebUI）。

### 修复
- 修复 Overlayer 配置解析失败：Unity `JsonUtility` 不支持 `double` 字段，遇到不支持的字段类型会整体静默失败导致组件列表为空。已将进度条的 `min`/`max` 改为 `float`，并改为逐元素反序列化，单个元素异常不再影响整体。

## 1.4.0（2026-08-27）
### 新增
- KPS/Total 默认摆位与数值对齐（依据 CT 源码 KVNode.CountTextAlignment：0=左/1=中/2=右，仅作用于数值文本）：
  - 提示词规则：KPS 节点默认放键位区最左侧、Total 放最右侧；KPS 数值左对齐(CountTextAlignment=0)、Total 数值右对齐(=2)。
  - 代码兜底：生成/修正键雨时自动将 NodeType=1 的节点 CountTextAlignment 置 0、NodeType=2 置 2，旧 .ctkv 用「修正键雨」按钮同样生效。

## 1.3.0（2026-08-27）
### 界面优化
- 新增顶部可折叠「使用说明」：五步引导（选密钥→描述需求→生成保存→CT 导入→键雨规则说明），默认展开。
- 标题与简介精简，步骤编号统一为 ①②③④；密钥管理说明合并为单行提示。
- .ctkv 文件管理页新增按钮用途说明（查看内容 / 修正键雨）。
- 移除约 1000 行无引用的旧界面死代码（基础设置/键雨/颜色/外观/FreeMake 编辑器/统计与 Agent 等旧 Tab 及其辅助方法，纯 .ctkv 生成器模式下均已停用）。

## 1.2.0（2026-08-27）
### 新增
- 「.ctkv 文件管理」新增**修正键雨**按钮：对已生成的旧 .ctkv 包就地应用键雨兜底修正（无需重新生成），修复旧文件整排雨为单色的问题。
### 修复
- 上层键雨逐键变色（第二排同理独立配置）：CT 行级 KeyRainColorRow* 只是整排单色，无法逐键变色。现生成后强制为每个键写入节点级 UseCustomRain=true + RainColor=该键边框色（第一排）/白色（第二排），并同步写节点级 RainYOffset 与 RainWidthRatio（第一排=1、第二排=0.7），CT 渲染时优先读取节点级参数。
- 注意：此修复需要重新生成或对旧 .ctkv 点击「修正键雨」才会生效；游戏内需重启加载新版本 DLL。

## 1.1.5（2026-08-27）
### 修复
- 修复两排键雨偏移仍为 0：原兜底公式「Row2偏移=第二排高度−第一排高度」在两排等高时结果为 0。经确认改为几何精确对齐公式：KeyRainYOffsetRow2 = (下排键底Y − 上排键底Y) + KeyRainYOffsetRow1（键底Y=PositionY+Height），数学上保证两排雨条起始线同一 Y。
- 修复 rain 颜色单一/不符合要求：键雨颜色兜底由「仅缺失时补」改为**无条件强制覆盖**——KeyRainColorRow1 一律取上排键边框色、KeyRainColorRow2 一律白色、KeyRainWidthRatio1=1/Ratio2=0.7，不再信任 AI 写入的值。
- 提示词与自检规则同步更新为几何对齐公式。

## 1.1.4（2026-08-27）
### 修复
- 修复两排布局键雨对齐失效：AI 生成的 XML 常完全缺失 KeyRainYOffsetRow1/Row2 且 RainRow 分配混乱，提示词约束不可靠。新增程序化兜底 FixTwoRowRainLayout：生成成功后按 PositionY 自动分组检测两排布局，强制修正每键 RainRow（上排=1、下排=0）、按公式计算注入 KeyRainYOffsetRow2=第二排高度−第一排高度、补齐 KeyRainColorRow1（取上排边框色）/KeyRainColorRow2（白色）/KeyRainWidthRatio1=1/Ratio2=0.7。兜底过程写入 UMM 日志便于核对。

## 1.1.3（2026-08-27）
### 修复
- 修复 API 密钥保存失效：Unity JsonUtility 对嵌套 List 序列化不稳定，导致 apikeys.json 实际写为 `{}`、重启后密钥丢失。改为手写 JSON 序列化/反序列化，保证密钥可靠持久化。
### 优化
- 提示词强化键雨规则：明确「第一排(上排)=RainRow1 用 Row1 参数、第二排(下排)=RainRow0 用 Row2 参数」，并列出 CT 配置级字段名（KeyRainYOffsetRow1/2、KeyRainColorRow1/2、KeyRainWidthRatio1/2）。
- 强制 AI 必须按公式 KeyRainYOffsetRow2=第二排按键高度−第一排按键高度算出具体非零偏移值，不得为 0/漏填；第一排键雨颜色 KeyRainColorRow1=上排键边框色、第二排 KeyRainColorRow2=白色。
- 自检提示同步加入上述校验规则。

## 1.1.2（2026-08-27）
### 修复/优化
- 键雨对齐公式改为：KeyRainYOffsetRow2 = 第二排按键高度 − 第一排按键高度。
- 键雨宽度比例：第一排 KeyRainWidthRatio1 默认 1，第二排 KeyRainWidthRatio2 默认 0.7。
- 按键间距收敛到 3~7 像素。
- 自检提示同步更新对应规则。

## 1.1.1（2026-08-27）
### 修复/优化
- 提示词补充键雨对齐精确公式：KeyRainYOffsetRow2 = KeyRainYOffsetRow1 + 第一排按键高度 +（第一排y − 第二排y）。
- 按键间距收敛到 5~10 像素；圆角半径建议 3~8。
- 第一排键雨颜色须等于该排键的边框颜色；第二排键雨固定白色且比第一排细（KeyRainWidthRatio2 < KeyRainWidthRatio1）。
- 自检提示同步增加上述校验规则。

## 1.1.0（2026-08-27）

### 新增
- 供应商下拉一键选择（DeepSeek / 通义千问 Qwen / TokenRa 网关 / OpenRouter / 自定义），只需输入 API Key 即可添加密钥。
- TokenRa 网关支持多模型下拉选择：artsdance-2-5-pro-260801、deepseek-v4-flash、glm-5.2、glm-5.3、glm-5.3-flash、kimi-k3、MiniMax-M3、qwen3.8-max、seedance-2-0-fast / mini / pro。
- 支持 DeepSeek、Qwen、TokenRa 等供应商的「关闭模型思考」参数，并可在界面勾选全局开关（默认开）。
- 新增「请求时按 Base URL 实时匹配供应商预设取参数」：预设改动无需重新添加密钥即生效。
- 日志新增 `model` 字段，并记录每次请求的 prompt 缓存命中率（prompt_cache_hit_tokens / prompt_cache_miss_tokens）。

### 优化
- 两轮生成流程：第一轮 XML 有效则直接采用并跳过第二轮；第二轮仅在无效时作为「修复器」运行（附具体校验错误），大幅降低输出截断风险与请求开销。
- XML 解析更健壮：支持剥离 Markdown 围栏、前置说明文字、`\uXXXX` unicode 转义还原。
- 提示词强化：默认键位布局（tab 1 2 e p = backspace \ / Lshift Lctrl space c enter Rshift , k）、K 数居中节选、上下排键雨对齐（RainRow + KeyRainYOffsetRow）、排布与图层规则、颜色必须用 `<float>` 子节点等。
- max_tokens 按供应商配置（DeepSeek/TokenRa 16384）。

### 修复
- 修复 API 返回 HTML / 403 / XML 截断 / 颜色格式错误等导致生成失败的问题。
- 修复重装 Mod 后 API 配置丢失（迁移历史 apikeys.json 位置）。
- 排除 ct_decomp 反编译文件被误纳入编译导致的 CS1525。

## 1.0.0（2026-08-26）
- 初始版本。
