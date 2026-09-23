using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ADOFAI.AgentKeyViewer.Bootstrap;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 游戏内更新器：检查 GitHub Release、下载新版、应用更新。
    ///
    /// 更新分两部分：
    ///   - <b>核心</b> AgentKeyViewer.Core.dll：直接覆盖文件后请求引导器热替换，<b>不需要重启游戏</b>；
    ///   - <b>引导器</b> ADOFAI.AgentKeyViewer.dll：它是 UMM 载入且无法卸载的，替换后需重启游戏才生效。
    /// 更新包通常只改核心，因此绝大多数更新都能热生效。
    /// </summary>
    public static class ModUpdater
    {
        public const string RepoOwner = "1600yuanshi";
        public const string RepoName = "adofai-keyviewer-mod";
        public const string ApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";

        public enum UpdateState { Idle, Checking, UpToDate, HasUpdate, Downloading, Ready, Failed }

        public static UpdateState State { get; private set; } = UpdateState.Idle;
        public static string CurrentVersion => CoreEntry.CoreVersionText;
        public static string LatestVersion { get; private set; } = "";
        public static string ReleaseNotes { get; private set; } = "";
        public static string DownloadUrl { get; private set; } = "";
        public static string Error { get; private set; } = "";
        /// <summary>已下载并解包的更新目录（Ready 状态时非空）</summary>
        public static string StagedDir { get; private set; } = "";
        /// <summary>本次更新是否包含引导器改动（需要重启游戏）</summary>
        public static bool BootstrapChanged { get; private set; }

        private static Task _task;
        private static int _reloadDelayFrames;

        private static string ModDir
        {
            get
            {
                string dir = CoreEntry.ModEntry != null ? Path.GetDirectoryName(CoreEntry.ModEntry.Path) : null;
                return string.IsNullOrEmpty(dir) ? "." : dir;
            }
        }

        private static string UpdateRoot
        {
            get
            {
                var gameRoot = ModPathHelper.GetGameDir(CoreEntry.ModEntry);
                return Path.Combine(gameRoot, "AgentKeyViewer_config", "update");
            }
        }

        public static bool IsBusy => _task != null && !_task.IsCompleted;

        // ====================================================================
        //  检查更新
        // ====================================================================

        public static bool Check()
        {
            if (IsBusy) return false;
            State = UpdateState.Checking;
            Error = "";
            _task = Task.Run(() =>
            {
                try
                {
                    string json = HttpGet(ApiUrl);
                    string tag = Json.GetString(json, "tag_name");
                    string body = Json.GetString(json, "body");
                    string url = FindZipAssetUrl(json);

                    LatestVersion = tag?.TrimStart('v', 'V') ?? "";
                    ReleaseNotes = body ?? "";
                    DownloadUrl = url ?? "";

                    if (string.IsNullOrEmpty(LatestVersion))
                        throw new Exception("未能从 Release 响应中解析出版本号");

                    State = IsNewer(LatestVersion, CurrentVersion) ? UpdateState.HasUpdate : UpdateState.UpToDate;
                    CoreEntry.ModEntry?.Logger.Log($"[Updater] 当前 {CurrentVersion}，最新 {LatestVersion}，状态 {State}");
                    return;
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    State = UpdateState.Failed;
                    CoreEntry.ModEntry?.Logger.Error($"[Updater] 检查更新失败: {ex.Message}");
                }
            });
            return true;
        }

        /// <summary>下载并解包更新（后台线程）</summary>
        public static bool Download()
        {
            if (IsBusy) return false;
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                Error = "没有可用的下载地址，请先检查更新";
                State = UpdateState.Failed;
                return false;
            }
            State = UpdateState.Downloading;
            Error = "";
            _task = Task.Run(() =>
            {
                try
                {
                    string root = UpdateRoot;
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                    Directory.CreateDirectory(root);

                    string zipPath = Path.Combine(root, "update.zip");
                    DownloadFile(DownloadUrl, zipPath);
                    CoreEntry.ModEntry?.Logger.Log($"[Updater] 已下载更新包: {zipPath}");

                    string staged = Path.Combine(root, "staged");
                    if (Directory.Exists(staged)) Directory.Delete(staged, true);
                    ZipFile.ExtractToDirectory(zipPath, staged);

                    // 校验必需文件
                    string core = Path.Combine(staged, BootstrapMain.CoreAssemblyName);
                    if (!File.Exists(core))
                        throw new Exception($"更新包中缺少 {BootstrapMain.CoreAssemblyName}");

                    StagedDir = staged;
                    State = UpdateState.Ready;
                    CoreEntry.ModEntry?.Logger.Log($"[Updater] 更新包已解包就绪: {staged}");
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    State = UpdateState.Failed;
                    CoreEntry.ModEntry?.Logger.Error($"[Updater] 下载更新失败: {ex.Message}");
                }
            });
            return true;
        }

        // ====================================================================
        //  应用更新（主线程）
        // ====================================================================

        /// <summary>
        /// 应用已下载的更新：覆盖核心/引导器/Info.json，并请求引导器热替换核心。
        /// 必须在主线程调用；热替换会延迟若干帧，以便当前 HTTP 响应先写回浏览器。
        /// </summary>
        public static bool ApplyStaged(out string message)
        {
            message = "";
            if (State != UpdateState.Ready || string.IsNullOrEmpty(StagedDir))
            {
                message = "没有已就绪的更新，请先检查并下载";
                return false;
            }

            try
            {
                string modDir = ModDir;
                string stagedCore = Path.Combine(StagedDir, BootstrapMain.CoreAssemblyName);
                string stagedBoot = Path.Combine(StagedDir, "ADOFAI.AgentKeyViewer.dll");

                // 1) 覆盖核心（我们只按字节读取核心，不持有文件句柄，可安全覆盖）
                File.Copy(stagedCore, Path.Combine(modDir, BootstrapMain.CoreAssemblyName), true);

                // 2) 引导器有变化则一并覆盖，但它需要重启游戏才生效
                BootstrapChanged = false;
                if (File.Exists(stagedBoot))
                {
                    string installedBoot = Path.Combine(modDir, "ADOFAI.AgentKeyViewer.dll");
                    if (!FilesEqual(stagedBoot, installedBoot))
                    {
                        File.Copy(stagedBoot, installedBoot, true);
                        BootstrapChanged = true;
                    }
                }

                // 3) 同步 Info.json，让版本号与界面显示一致
                string stagedInfo = Path.Combine(StagedDir, "Info.json");
                if (File.Exists(stagedInfo))
                    File.Copy(stagedInfo, Path.Combine(modDir, "Info.json"), true);

                // 4) 请求引导器热替换核心（延迟若干帧，先让浏览器拿到响应）
                _reloadDelayFrames = 30;
                State = UpdateState.Idle;
                message = BootstrapChanged
                    ? "核心已更新并将在本帧后热生效；引导器有改动，需重启游戏才完全生效"
                    : "核心已更新，将在本帧后热生效（无需重启游戏）";
                CoreEntry.ModEntry?.Logger.Log($"[Updater] {message}");
                return true;
            }
            catch (Exception ex)
            {
                message = "应用更新失败: " + ex.Message;
                State = UpdateState.Failed;
                Error = ex.Message;
                CoreEntry.ModEntry?.Logger.Error($"[Updater] {message}");
                return false;
            }
        }

        /// <summary>仅重载核心（本地开发用：重新编译 DLL 后点一下即可生效，不必重启游戏）</summary>
        public static void RequestCoreReload()
        {
            _reloadDelayFrames = 30;
            CoreEntry.ModEntry?.Logger.Log("[Updater] 已请求热重载核心");
        }

        /// <summary>由 CoreEntry.Update 每帧调用（主线程）</summary>
        public static void Tick()
        {
            if (_reloadDelayFrames > 0)
            {
                _reloadDelayFrames--;
                if (_reloadDelayFrames == 0)
                {
                    BootstrapMain.RequestCoreReload();
                }
            }
        }

        // ====================================================================
        //  工具
        // ====================================================================

        /// <summary>从 Release JSON 中找出 .zip 附件下载地址</summary>
        private static string FindZipAssetUrl(string json)
        {
            int arrStart = KVConfig.FindArrayStart(json, Math.Max(0, json.IndexOf("\"assets\"", StringComparison.Ordinal)));
            if (arrStart < 0) return "";
            int arrEnd = KVConfig.FindMatchingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) arrEnd = json.Length - 1;

            string fallback = "";
            int i = arrStart + 1;
            while (i < arrEnd)
            {
                int objStart = KVConfig.FindObjectStart(json, i, arrEnd);
                if (objStart < 0) break;
                int objEnd = KVConfig.FindMatchingBracket(json, objStart, '{', '}');
                if (objEnd < 0 || objEnd > arrEnd) break;

                string obj = json.Substring(objStart, objEnd - objStart + 1);
                string name = Json.GetString(obj, "name");
                string url = Json.GetString(obj, "browser_download_url");
                if (!string.IsNullOrEmpty(url))
                {
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return url;
                    if (string.IsNullOrEmpty(fallback)) fallback = url;
                }
                i = objEnd + 1;
            }
            return fallback;
        }

        /// <summary>版本比较：按 . 分段数值比较，latest 大于 current 返回 true</summary>
        public static bool IsNewer(string latest, string current)
        {
            if (string.IsNullOrWhiteSpace(latest)) return false;
            if (string.IsNullOrWhiteSpace(current)) return true;

            var a = latest.Split('.');
            var b = current.Split('.');
            int n = Math.Max(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int va = i < a.Length && int.TryParse(a[i], out int x) ? x : 0;
                int vb = i < b.Length && int.TryParse(b[i], out int y) ? y : 0;
                if (va != vb) return va > vb;
            }
            return false;
        }

        private static string HttpGet(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            // GitHub API 要求带 User-Agent
            req.UserAgent = "AgentKeyViewer-Updater";
            req.Accept = "application/vnd.github+json";
            req.Timeout = 30000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static void DownloadFile(string url, string destPath)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "AgentKeyViewer-Updater";
            req.Timeout = 120000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(destPath))
                src.CopyTo(dst);
        }

        private static bool FilesEqual(string a, string b)
        {
            try
            {
                if (!File.Exists(a) || !File.Exists(b)) return false;
                var fa = new FileInfo(a);
                var fb = new FileInfo(b);
                if (fa.Length != fb.Length) return false;
                return File.ReadAllBytes(a).Length == File.ReadAllBytes(b).Length
                       && Convert.ToBase64String(File.ReadAllBytes(a)) == Convert.ToBase64String(File.ReadAllBytes(b));
            }
            catch { return false; }
        }
    }
}
