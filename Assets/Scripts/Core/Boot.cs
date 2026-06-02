using UnityEngine;

namespace Rzz.Core
{
    // Boot is the single entry point that loads GameConfig before any game system runs.
    // Phase 1: simple static singleton. Phase 2: refactor to VContainer DI.
    public class Boot : MonoBehaviour
    {
        public static GameConfig Config { get; private set; }
        public static bool Ready { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Config = new GameConfig();
            Config.Reload();
            Ready = true;
            Debug.Log($"[Boot] GameConfig loaded: {Config.Monsters.Count} monsters, {Config.Stages.Count} stages, {Config.Upgrades.Count} upgrades, {Config.Tuning.Count} tuning keys.");
        }
    }
}
