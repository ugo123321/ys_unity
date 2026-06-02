using UnityEngine;
using UnityEngine.UI;
using Rzz.Core;
using Rzz.Battle;

namespace Rzz.UI
{
    // Phase 2.4 pause menu. Shows when PauseButton or Escape is pressed.
    // Uses Time.timeScale = 0 to freeze gameplay. Battle.State is moved to Paused so
    // BattleController.Update short-circuits.
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] Button _resumeButton;
        [SerializeField] Button _restartButton;
        [SerializeField] Button _mainMenuButton;
        [SerializeField] BattleController _battle;

        GameState _prevState;

        void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_resumeButton != null) _resumeButton.onClick.AddListener(Hide);
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        void Update()
        {
            bool esc = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) esc = true;
#endif
            if (!esc && Input.GetKeyDown(KeyCode.Escape)) esc = true;
            if (esc)
            {
                if (IsVisible()) Hide();
                else Show();
            }
        }

        public bool IsVisible() => _root != null && _root.activeSelf;

        public void Show()
        {
            if (_root == null) return;
            if (IsVisible()) return;
            _prevState = _battle != null ? _battle.State : GameState.Playing;
            _root.SetActive(true);
            Time.timeScale = 0f;
            if (_battle != null) _battle.EnterPause();
        }

        public void Hide()
        {
            if (_root == null) return;
            if (!IsVisible()) return;
            _root.SetActive(false);
            Time.timeScale = 1f;
            if (_battle != null) _battle.ExitPause(_prevState);
        }

        void OnRestart()
        {
            Time.timeScale = 1f;
            GameFlowController.RestartCurrentScene();
        }

        void OnMainMenu()
        {
            Time.timeScale = 1f;
            GameFlowController.LoadMain();
        }
    }
}
