依赖项说明
==========

请将以下 DLL 文件复制到此目录：

1. UnityModManager.dll
   - 来源：安装 UnityModManager 后，在游戏目录的 Mods\UnityModManager\ 文件夹中找到
   - 下载地址：https://www.nexusmods.com/site/mods/21

2. 0Harmony.dll
   - 来源：UnityModManager 安装包中自带，同样在 Mods\UnityModManager\ 文件夹中
   - 或者从 Harmony 官方发布获取：https://github.com/pardeike/Harmony

安装步骤
========

方法一：从游戏目录复制（推荐）
1. 确保已安装 UnityModManager
2. 进入游戏的 Mods\UnityModManager\ 目录
3. 复制 UnityModManager.dll 和 0Harmony.dll 到此 libs 目录

方法二：手动下载
1. 下载 UnityModManager：https://www.nexusmods.com/site/mods/21
2. 解压后将 UnityModManager.dll 和 0Harmony.dll 复制到此目录

验证
====
放置完成后，此目录应包含：
- UnityModManager.dll
- 0Harmony.dll

然后重新运行 build.bat 即可编译。
