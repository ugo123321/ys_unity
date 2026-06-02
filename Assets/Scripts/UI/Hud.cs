using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rzz.Core;
using Rzz.Entities;

namespace Rzz.UI
{
    // Phase 2.3 expanded HUD: HP/KI bars, Combo, Stage label, Pause button, Gold widget,
    // Exp bar, Message banner. Buff notice reuses the message banner pipeline.
    public class Hud : MonoBehaviour
    {
        [Header("Phase 1 minimal")]
        [SerializeField] BattlePlayer _player;
        [SerializeField] Slider _hpBar;
        [SerializeField] Slider _kiBar;
        [SerializeField] TMP_Text _comboText;
        [SerializeField] TMP_Text _stageText;
        [SerializeField] TMP_Text _hpText;

        [Header("Phase 2.3 additions")]
        [SerializeField] Button _pauseButton;
        [SerializeField] Rzz.UI.PauseMenu _pauseMenu;
        [SerializeField] TMP_Text _goldText;
        [SerializeField] Slider _expBar;
        [SerializeField] TMP_Text _expText;
        [SerializeField] GameObject _messageBanner;
        [SerializeField] TMP_Text _messageText;
        [SerializeField] CanvasGroup _messageGroup;

        Coroutine _messageRoutine;

        void OnEnable()
        {
            EventBus.PlayerDamaged += OnPlayerDamaged;
            EventBus.PlayerHealed += OnPlayerHealed;
            EventBus.ComboChanged += OnComboChanged;
            EventBus.StageStarted += OnStageStarted;
            EventBus.ExpChanged += OnExpChanged;
            EventBus.GoldChanged += OnGoldChanged;
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPausePressed);
        }

        void OnDisable()
        {
            EventBus.PlayerDamaged -= OnPlayerDamaged;
            EventBus.PlayerHealed -= OnPlayerHealed;
            EventBus.ComboChanged -= OnComboChanged;
            EventBus.StageStarted -= OnStageStarted;
            EventBus.ExpChanged -= OnExpChanged;
            EventBus.GoldChanged -= OnGoldChanged;
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPausePressed);
        }

        void Start()
        {
            if (_messageBanner != null) _messageBanner.SetActive(false);
        }

        void Update()
        {
            if (_player == null) return;
            if (_hpBar != null) _hpBar.value = (float)_player.Hp / Mathf.Max(1, _player.MaxHp);
            if (_kiBar != null) _kiBar.value = _player.Ki / Mathf.Max(1f, _player.KiMax);
            if (_hpText != null) _hpText.text = $"{_player.Hp}/{_player.MaxHp}";
        }

        void OnPlayerDamaged(int amount, int hp) { }
        void OnPlayerHealed(int amount, int hp) { }

        void OnComboChanged(int combo)
        {
            if (_comboText != null) _comboText.text = combo > 0 ? $"COMBO {combo}" : "";
        }

        void OnStageStarted(int idx)
        {
            if (_stageText != null) _stageText.text = $"第 {idx + 1} 关";
            if (_comboText != null) _comboText.text = "";
            ShowMessage($"第 {idx + 1} 关", 1.2f);
        }

        void OnExpChanged(int level, int exp, int next)
        {
            if (_expBar != null) _expBar.value = next > 0 ? (float)exp / next : 0f;
            if (_expText != null) _expText.text = $"Lv {level}   {exp}/{next}";
        }

        void OnGoldChanged(int total)
        {
            if (_goldText != null) _goldText.text = "$ " + total;
        }

        void OnPausePressed()
        {
            if (_pauseMenu != null) _pauseMenu.Show();
        }

        public void ShowMessage(string text, float duration = 1.2f)
        {
            if (_messageBanner == null || _messageText == null) return;
            if (_messageRoutine != null) StopCoroutine(_messageRoutine);
            _messageRoutine = StartCoroutine(MessageRoutine(text, duration));
        }

        IEnumerator MessageRoutine(string text, float duration)
        {
            _messageText.text = text;
            _messageBanner.SetActive(true);
            if (_messageGroup != null) _messageGroup.alpha = 1f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (_messageGroup != null)
                {
                    float k = Mathf.Clamp01(1f - (t / duration));
                    _messageGroup.alpha = Mathf.Pow(k, 0.5f);
                }
                yield return null;
            }
            _messageBanner.SetActive(false);
            _messageRoutine = null;
        }
    }
}
