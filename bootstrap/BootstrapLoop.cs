using UnityEngine;

namespace ADOFAI.AgentKeyViewer.Bootstrap
{
    /// <summary>
    /// 每帧驱动器。刻意放在引导器里：核心可以被热替换，而 MonoBehaviour 一旦挂到
    /// GameObject 上就不该再更换类型，所以驱动器由稳定的引导器持有，只调用核心的 Update。
    /// </summary>
    public class BootstrapLoop : MonoBehaviour
    {
        private static BootstrapLoop _instance;

        public static BootstrapLoop EnsureExists()
        {
            if (_instance == null)
            {
                var go = new GameObject("[AgentKeyViewer_BootstrapLoop]");
                _instance = go.AddComponent<BootstrapLoop>();
                DontDestroyOnLoad(go);
                BootstrapMain.Log("已创建每帧驱动器");
            }
            return _instance;
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
        }

        private void Update() => BootstrapMain.Tick();

        private void OnGUI() => BootstrapMain.DrawInGame();

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
