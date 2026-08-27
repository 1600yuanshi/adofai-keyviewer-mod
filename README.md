# Agent Key Viewer
*本模组由AI生成！* 

**一句话生成你的按键显示配置。**

Agent Key Viewer 是《冰与火之舞》(A Dance of Fire and Ice) 的 **CheryTools 附属模组**：内置 AI，把自然语言描述直接生成为 CheryTools 可导入的 `.ctkv` 键位配置包（ZIP 内含 KeyViewer.xml 的标准 CT 包格式）。游戏内的实际渲染由 CherryTools 完成，本模组专注"生成"这一步。

> 「16K 全键盘，紫色到天蓝渐变边框，按下白色高亮，开启键雨」→ 一键生成 → CT 导入即用

***

## 特性

- **描述即配置**：自然语言下单，AI 两轮「生成 + 自检」输出标准 XML，无 Markdown 残留
- **任意键位**：默认 16K 键位&#x20;
``` 
tab 1 2 e p = backspace \  
Lshift Lctrl space c enter Rshift , k
```
- 支持 4K/8K/12K/16K 自动居中节选，也支持自定义
- **键雨逐键变色**：第一排键雨自动跟随各键边框色（红键红雨、蓝键蓝雨），第二排固定白色且更细，上下两排起始高度几何精确对齐
- **显示模式混合**：每个键可在 键 / KPS / Total / 图片 四种模式间切换；KPS 默认居左且数值左对齐，Total 居右且数值右对齐
- **细节兜底修正**：AI 容易漏填的高度偏移、逐键雨色、数值对齐等字段由代码强制补正，开箱即用
- **外观定制**：圆角边框、键背景图片（JPG/PNG/GIF）、透明背景、按下白色高亮
- **旧包修复**：`.ctkv 文件管理` 页提供「修正键雨」按钮，一键就地升级旧配置

## 安装

1. 安装 [Unity Mod Manager](https://github.com/newman55/unity-mod-manager)（UMM）
2. 安装 [CheryTools](https://github.com/adofaiex/CheryTools) 并确认其可正常显示 KV
3. 将 `ADOFAI.AgentKeyViewer-v{版本}.zip` 放入游戏目录的 `Mods/` 下解压（或用 UMM 安装）
4. 启动游戏，在 UMM 设置中展开 **Agent Key Viewer**

## 使用步骤

1. **选择密钥**：在「Agent 生成 .ctkv」页选择 API 密钥；没有就点开「密钥管理」添加——选好供应商后只需粘贴 API Key
2. **描述需求**：在输入框写一句话，如示例
3. **生成并保存**：点击「生成 .ctkv」，成功后输入文件名保存
4. **CT 导入**：打开 CherryTools → KeyViewer 设置 → 导入配置包，选中保存的文件即可

顶部「使用说明」内置于界面，随版本更新。

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
    ├── ctkv/               # 生成的 .ctkv 配置包
    ├── images/             # 键背景图片（JPG/PNG/GIF）
    └── logs/               # AI 请求/响应日志（含错误详情）
```

## 常见问题

- **生成失败 / 403 Forbidden**：检查 API Key 与供应商是否匹配、是否有速率限制；详情看 `logs/`
- **输出变成思维链导致解析失败**：保持「关闭模型思考」勾选（默认开启），内置了对各供应商的思考关闭参数
- **更新模组后没生效**：需完全重启游戏（UMM 只在启动时加载 DLL）
- **旧生成的配置键雨颜色单一/偏移不对**：在「.ctkv 文件管理」对该文件点「修正键雨」即可
- **推荐模型**：DeepSeek 或 TokenRa 网关下的较大模型；过小的模型可能截断长 XML

## 版本历史

见 [CHANGELOG.md](CHANGELOG.md)。当前版本功能概要：

- **1.4.0** KPS 左/Total 右摆位 + 数值对齐（CountTextAlignment 兜底）
- **1.3.0** 界面精简重构，内置使用说明
- **1.2.0** 键雨逐键变色、「修正键雨」就地修复按钮
- **1.0\~1.1** .ctkv 双向导入导出、圆角/图片/GIF、TokenRa 多模型、思考关闭、密钥持久化

***

*本模组为 Chery Tools 的附属生成器，仅负责配置生成，不参与游戏内渲染。*
