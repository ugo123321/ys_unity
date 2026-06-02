using UnityEngine;
using Rzz.Core;
using Rzz.Entities;
using Rzz.Systems;
using Rzz.UI;

namespace Rzz.Battle
{
    // Ports scripts/battle.gd (Phase 1 simplified).
    // Phase 1 omits: experience/upgrade managers, buff orbs, abilities, summons, particles,
    // blood stains, ground effects, boss logic, level overlay, fail animator, pause menu.
    public class BattleController : MonoBehaviour
    {
        [Header("Scene Refs")]
        [SerializeField] BattlePlayer _player;
        [SerializeField] MonsterSpawner _spawner;
        [SerializeField] CombatDirector _combat;
        [SerializeField] PathInput _pathInput;
        [SerializeField] Camera _camera;
        [SerializeField] BattleMonster _monsterPrefab;
        [SerializeField] Transform _monsterContainer;
        [Header("Phase 2.2")]
        [SerializeField] ExperienceManager _experience;
        [SerializeField] UpgradePopup _upgradePopup;
        [Header("Phase 2.3 / 2.4")]
        [SerializeField] Rzz.UI.DamageNumbers _damageNumbers;
        [SerializeField] Rzz.UI.PauseMenu _pauseMenu;

        public BattlePlayer Player => _player;
        public CombatDirector Combat => _combat;
        public ExperienceManager Experience => _experience;
        public Rzz.UI.DamageNumbers DamageNumbers => _damageNumbers;
        public Rzz.UI.PauseMenu PauseMenu => _pauseMenu;
        public UpgradeManager Upgrades { get; } = new UpgradeManager();
        public GameState State { get; private set; } = GameState.Menu;
        public float TimeScale { get; private set; } = 1f;
        public int StageIndex { get; private set; }

        Vector2 _logicalSize;
        float _shakeMag;
        float _shakeDur;
        float _shakeTimer;
        Vector3 _cameraHome;

        bool _pointerWasDown;

        void Awake()
        {
            if (Boot.Config == null)
            {
                Debug.LogError("[Battle] Boot.Config not initialized");
                return;
            }
            _logicalSize = Boot.Config.GetLogicalSize();
        }

        void Start()
        {
            if (_combat != null) _combat.Setup(this);
            if (_spawner != null) _spawner.Setup(this, _monsterPrefab, _monsterContainer);
            if (_pathInput != null) _pathInput.Setup(this);
            if (_experience != null) _experience.Setup(this);
            if (_upgradePopup != null) _upgradePopup.Setup(this, Upgrades);
            if (_player != null)
            {
                _player.Bind(this);
                // Set position BEFORE ApplyConfig so HomePosition picks up the spawn location.
                // Player sits near the BOTTOM of the screen in Godot (y=742 of 1280).
                // Unity Y is up, so we mirror: spawnY = logicalHeight - 742 ≈ 538.
                float spawnY = _logicalSize.y - 742f;
                _player.transform.position = new Vector3(_logicalSize.x * 0.5f, spawnY, 0);
                _player.ApplyConfig();
                _player.BeginStage();
            }
            if (_camera != null)
            {
                // Frame the logical 720x1280 area in orthographic mode.
                // Choose orthographicSize so the logical world fits regardless of viewport aspect:
                //   targetAspect = logicalW / logicalH (portrait < 1)
                //   if viewport is wider than that, height drives size; otherwise width drives size.
                _camera.orthographic = true;
                float logicalAspect = _logicalSize.x / _logicalSize.y;
                float viewAspect = _camera.aspect;
                float halfH = _logicalSize.y * 0.5f;
                if (viewAspect < logicalAspect)
                    halfH = (_logicalSize.x * 0.5f) / viewAspect;
                _camera.orthographicSize = halfH;
                _camera.transform.position = new Vector3(_logicalSize.x * 0.5f, _logicalSize.y * 0.5f, -10);
                _cameraHome = _camera.transform.position;
                _camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            }
            StartGame();
        }

        public void StartGame()
        {
            ChangeState(GameState.Playing);
            StageIndex = 0;
            _combat.ResetForStage();
            if (_experience != null) _experience.ResetRun();
            _spawner.SpawnStage(StageIndex);
            EventBus.RaiseStageStarted(StageIndex);
        }

        public void EnterLevelUp()
        {
            if (State == GameState.LevelUp) return;
            ChangeState(GameState.LevelUp);
            Upgrades.GenerateChoices(_player);
            if (_upgradePopup != null) _upgradePopup.Show();
        }

        public void ExitLevelUp()
        {
            if (State != GameState.LevelUp) return;
            if (_upgradePopup != null) _upgradePopup.Hide();
            ChangeState(GameState.Playing);
        }

        public void NextStage()
        {
            StageIndex++;
            _combat.ResetForStage();
            _player.BeginStage();
            _spawner.SpawnStage(StageIndex);
            ChangeState(GameState.Playing);
            EventBus.RaiseStageStarted(StageIndex);
        }

        public void EnterPause()
        {
            if (State == GameState.Paused) return;
            ChangeState(GameState.Paused);
        }

        public void ExitPause(GameState resumeTo)
        {
            if (State != GameState.Paused) return;
            ChangeState(resumeTo == GameState.Paused ? GameState.Playing : resumeTo);
        }

        void Update()
        {
            if (State == GameState.Paused) return;
            // Clamp delta so a long editor stall (focus loss, profiler hang, MCP blocking) doesn't teleport entities.
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float scaledDelta = delta * TimeScale;
            // Pointer input always processed (lets you cancel a drag even if state changes).
            if (State == GameState.Playing) HandlePointerInput();
            switch (State)
            {
                case GameState.Playing:
                    UpdatePlaying(scaledDelta, delta);
                    if (_experience != null) _experience.TryTriggerUpgrade(this);
                    break;
            }
            UpdateCameraShake(delta);
        }

        void UpdatePlaying(float scaledDelta, float realDelta)
        {
            float idleScale = TimeScale < 1f ? TimeScale : 1f;
            _player.UpdateIdle(realDelta, idleScale);

            if (_player.State == BattlePlayer.PlayerState.Attacking)
            {
                float attackDelta = TimeScale < 1f ? realDelta : scaledDelta;
                bool done = _player.UpdateAttack(attackDelta, _combat, _spawner.GetActiveMonsters());
                if (done) ResumeBattleTime();
            }

            _combat.UpdateResolve(scaledDelta, _player);
            _combat.UpdateDamageNumbers(realDelta);
            _spawner.UpdateSpawns(realDelta);

            float monsterDelta = TimeScale >= 1f ? scaledDelta : 0f;
            foreach (var m in _spawner.GetActiveMonsters())
            {
                if (m == null) continue;
                m.UpdateDeath(realDelta);
                m.UpdateAi(monsterDelta, _player, this);
            }

            if (_player.Hp <= 0)
            {
                ChangeState(GameState.Fail);
                Debug.Log("[Battle] Player died - GAME OVER (Phase 1 stub)");
            }
            else if (_spawner.AllDead() && !_combat.IsResolving() && _player.State == BattlePlayer.PlayerState.Idle)
            {
                EventBus.RaiseStageCleared(StageIndex);
                int next = StageIndex + 1;
                if (Boot.Config.GetStage(next) != null)
                {
                    Debug.Log($"[Battle] Stage {StageIndex} cleared → advancing to stage {next}");
                    StageIndex = next;
                    _combat.ResetForStage();
                    _player.BeginStage();
                    _spawner.SpawnStage(StageIndex);
                    EventBus.RaiseStageStarted(StageIndex);
                }
                else
                {
                    ChangeState(GameState.Complete);
                    Debug.Log("[Battle] All stages cleared - COMPLETE");
                }
            }
        }

        void HandlePointerInput()
        {
            // Mouse-only for Phase 1 (PC editor). Phase 2 will switch to Input System EnhancedTouch.
            bool down = Input.GetMouseButton(0);
            Vector2 screenPos = Input.mousePosition;
            if (down && !_pointerWasDown) _pathInput.HandleStart(screenPos);
            else if (down) _pathInput.HandleMove(screenPos);
            else if (!down && _pointerWasDown) _pathInput.HandleEnd();
            _pointerWasDown = down;
        }

        public void EnterBulletTime()
        {
            TimeScale = Boot.Config.GetTuning("bullet_time_scale", 0.14f);
        }

        public void ExitBulletTime(bool cancelled)
        {
            if (cancelled)
            {
                ResumeBattleTime();
                if (_player.State == BattlePlayer.PlayerState.BulletTime) _player.InvalidatePath();
                return;
            }
            _player.StartAttack();
        }

        public void ResumeBattleTime()
        {
            TimeScale = 1f;
        }

        public Vector2 ScreenToWorld(Vector2 screenPos)
        {
            if (_camera == null) return screenPos;
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -_camera.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        public bool IsInBounds(Vector2 pos)
        {
            return pos.x >= 0 && pos.y >= 0 && pos.x <= _logicalSize.x && pos.y <= _logicalSize.y;
        }

        public void ShakeCamera(float magnitude, float duration)
        {
            _shakeMag = Mathf.Max(_shakeMag, magnitude);
            _shakeDur = duration;
            _shakeTimer = duration;
        }

        void UpdateCameraShake(float delta)
        {
            if (_camera == null) return;
            if (_shakeTimer <= 0)
            {
                _camera.transform.position = _cameraHome;
                return;
            }
            _shakeTimer -= delta;
            float t = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.001f, _shakeDur));
            float amp = _shakeMag * t;
            _camera.transform.position = _cameraHome + (Vector3)(Random.insideUnitCircle * amp);
            if (_shakeTimer <= 0) _shakeMag = 0;
        }

        void ChangeState(GameState newState)
        {
            if (State == newState) return;
            var old = State;
            State = newState;
            EventBus.RaiseGameStateChanged(old, newState);
        }
    }
}
