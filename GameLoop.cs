using UnityEngine;

namespace ADOFAI.AgentKeyViewer
{
    /// <summary>
    /// 游戏循环组件 - 挂载到DontDestroyOnLoad的GameObject上，确保每帧运行
    /// </summary>
    public class GameLoop : MonoBehaviour
    {
        private static GameLoop _instance;

        public static GameLoop Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AgentKeyViewer_GameLoop]");
                    _instance = go.AddComponent<GameLoop>();
                    DontDestroyOnLoad(go);
                    Main.ModEntry?.Logger.Log($"[GameLoop] Created new GameObject and component");
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Main.ModEntry?.Logger.Log($"[GameLoop] Awake, GameObject name={gameObject.name}");
        }

        private void Update()
        {
            if (!Main.Enabled) return;

            // 纯 .ctkv 生成器模式：停用本模组的游戏内按键渲染（仅保留 agent 生成）
            if (Main.RenderInGame)
            {
                // 按键录制模式：识别用户按下的键并绑定
                Main.HandleKeyRecording();

                // 传入DisplayRenderer的布局给InputCapture
                var layout = Main.DisplayRenderer?.CurrentLayout;
                Main.InputCapture?.Update(layout);

                // 更新显示渲染器
                Main.DisplayRenderer?.Update();
            }

            // 驱动 AI 配置生成（后台任务完成时解析结果）
            Main.AIGen?.Tick();
        }

        private void OnGUI()
        {
            if (!Main.Enabled) return;
            if (Main.RenderInGame)
                Main.DisplayRenderer?.OnGUI();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                Main.ModEntry?.Logger.Log("[GameLoop] OnDestroy");
                _instance = null;
            }
        }
    }
}
