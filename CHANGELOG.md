# 更新日志

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
