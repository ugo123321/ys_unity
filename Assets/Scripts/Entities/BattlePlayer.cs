using System.Collections.Generic;
using UnityEngine;
using Rzz.Core;
using Rzz.Core.Data;

namespace Rzz.Entities
{
    // Ports scripts/entities/player.gd (Phase 1 simplified).
    // Phase 1 omits: charge_strike, holy_shield, all upgrades, auto-bullet projectiles,
    // desperate counter, stillness heart, steadfast guard, summons, equipment bonuses.
    public class BattlePlayer : MonoBehaviour
    {
        public enum PlayerState { Idle, BulletTime, Attacking }

        // Stats (loaded from player.json)
        public float BaseAttack = 95f;
        public float AttackPowerScale = 1f;
        public float CritRate = 0.08f;
        public float CritDamage = 1.6f;

        public int MaxHp = 100;
        public int Hp = 100;
        public float InvincibleTimer;
        public float DamageFlashTimer;

        public float BaseKi = 234f;
        public float KiMax = 234f;
        public float Ki = 234f;
        public float KiRegenSpeed = 60f;
        public float ComboDamageBonus = 0.01f;

        public float HitboxRadius = 22f;
        public float TriggerRadiusRatio = 0.06f;
        public float TriggerRadiusMin = 55f;

        // State
        public PlayerState State = PlayerState.Idle;
        public Vector2 HomePosition;
        public float ComboCount;
        public int ComboHitCount;

        // Path
        public List<Vector2> AttackPath { get; private set; } = new List<Vector2>(64);
        public int PathIndex;
        public float PathProgress;
        Vector2 _lastAttackPos;
        bool _attackHitsPrimed;
        readonly Dictionary<int, bool> _pathHitInside = new Dictionary<int, bool>(32);

        // Upgrade stacks (Phase 2.2). id -> level. Persisted across stages within a run.
        public Dictionary<string, int> UpgradeStacks { get; private set; } = new Dictionary<string, int>();
        public int BulletCount = 1;
        public float AttackSpeedMult = 1f;
        public float KiRegenMult = 1f;
        public float BonusAttackMult = 1f;
        public float LuckRollBlueOffset;
        public float LuckRollPurpleOffset;
        public float LuckRollOrangeOffset;
        readonly HashSet<string> _runAcquiredOnce = new HashSet<string>();
        readonly HashSet<string> _chapterAcquiredOnce = new HashSet<string>();

        const float PATH_HIT_PAD_RATIO = 0.68f;
        const float MIN_PATH_STEP = 2f;

        [SerializeField] SpriteRenderer _sprite;
        [SerializeField] LineRenderer _pathLine;

        Rzz.Battle.BattleController _battle;
        Color _baseSpriteColor = Color.white;
        float _kiPerPixel = 0.18f;
        float _attackSpeed = 2850f;
        float _moveSpeed = 120f;
        float _invincibleDuration = 0.45f;

        public void Bind(Rzz.Battle.BattleController battle) => _battle = battle;

        public float GetEffectiveRadius() => HitboxRadius;
        public float GetPathHitPad() => HitboxRadius * PATH_HIT_PAD_RATIO;

        public float GetTriggerRadius()
        {
            float screenW = Boot.Config.GetLogicalSize().x;
            return Mathf.Max(TriggerRadiusMin, screenW * TriggerRadiusRatio + HitboxRadius);
        }

        public bool IsKiFull() => Ki >= KiMax - 0.01f;
        public bool IsInAttackMode() => State == PlayerState.Attacking;
        public bool IsAttackInvincible() => State == PlayerState.Attacking || State == PlayerState.BulletTime;

        public void ApplyConfig()
        {
            var c = Boot.Config;
            BaseAttack = c.GetPlayerValue("base_attack", 95f);
            MaxHp = c.GetPlayerValue("base_hp", 100);
            Hp = MaxHp;
            BaseKi = c.GetPlayerValue("base_ki", 234f);
            KiMax = BaseKi;
            Ki = KiMax;
            CritRate = c.GetPlayerValue("base_crit_rate", 0.08f);
            CritDamage = c.GetPlayerValue("base_crit_damage", 1.6f);
            KiRegenSpeed = c.GetPlayerValue("ki_regen_speed", 60f);
            ComboDamageBonus = c.GetPlayerValue("combo_damage_bonus", 0.01f);
            HitboxRadius = c.GetPlayerValue("hitbox_radius", 22f);
            TriggerRadiusRatio = c.GetPlayerValue("trigger_radius_ratio", 0.06f);
            TriggerRadiusMin = c.GetPlayerValue("trigger_radius_min", 55f);
            _kiPerPixel = c.GetPlayerValue("ki_per_pixel", 0.18f);
            _attackSpeed = c.GetPlayerValue("attack_speed", 2850f);
            _moveSpeed = c.GetPlayerValue("move_speed", 120f);
            _invincibleDuration = c.GetPlayerValue("invincible_time", 0.45f);
            HomePosition = transform.position;
            if (_sprite != null) _baseSpriteColor = _sprite.color;
            UpdatePathLine();
        }

        public void BeginStage()
        {
            State = PlayerState.Idle;
            AttackPath.Clear();
            PathIndex = 0;
            PathProgress = 0;
            Ki = KiMax;
            Hp = MaxHp;
            ComboCount = 0;
            ComboHitCount = 0;
            UpdatePathLine();
        }

        public void UpdateIdle(float delta, float timeScale)
        {
            if (State != PlayerState.Idle) return;
            InvincibleTimer = Mathf.Max(0, InvincibleTimer - delta);
            DamageFlashTimer = Mathf.Max(0, DamageFlashTimer - delta);
            ApplyCombatModulate();
            if (Ki < KiMax)
                Ki = Mathf.Min(KiMax, Ki + KiRegenSpeed * delta * timeScale);
        }

        public void UpdateJoystickLocomotion(Vector2 dir, float delta, Rzz.Battle.BattleController battle)
        {
            if (State != PlayerState.Idle) return;
            if (dir.sqrMagnitude < 0.01f) return;
            Vector2 next = (Vector2)transform.position + dir.normalized * (_moveSpeed * delta);
            if (battle.IsInBounds(next))
            {
                transform.position = new Vector3(next.x, next.y, transform.position.z);
                HomePosition = next;
            }
            if (_sprite != null) _sprite.flipX = dir.x < 0;
        }

        public void AddPathPoint(Vector2 point)
        {
            if (AttackPath.Count == 0 || Vector2.Distance(AttackPath[AttackPath.Count - 1], point) >= MIN_PATH_STEP)
            {
                AttackPath.Add(point);
                UpdatePathLine();
            }
        }

        public bool ConsumeKiByDistance(float distance)
        {
            float cost = distance * _kiPerPixel;
            if (Ki < cost) return false;
            Ki -= cost;
            return true;
        }

        public void InvalidatePath()
        {
            State = PlayerState.Idle;
            AttackPath.Clear();
            PathIndex = 0;
            PathProgress = 0;
            UpdatePathLine();
        }

        public void StartBulletTime()
        {
            State = PlayerState.BulletTime;
            AttackPath.Clear();
            PathIndex = 0;
            PathProgress = 0;
            AddPathPoint(HomePosition);
        }

        public void StartAttack()
        {
            if (AttackPath.Count < 2) { InvalidatePath(); return; }
            State = PlayerState.Attacking;
            ComboCount = 0;
            ComboHitCount = 0;
            _battle.Combat.BeginRoundAttack();
            PathIndex = 0;
            PathProgress = 0;
            _pathHitInside.Clear();
            _attackHitsPrimed = false;
            _lastAttackPos = AttackPath[0];
        }

        public bool UpdateAttack(float delta, Rzz.Battle.CombatDirector combat, List<BattleMonster> monsters)
        {
            if (State != PlayerState.Attacking || AttackPath.Count < 2) return false;
            if (!_attackHitsPrimed)
            {
                PrimePathStartHits(combat, monsters);
                _attackHitsPrimed = true;
            }
            PathProgress += _attackSpeed * delta;
            while (PathIndex < AttackPath.Count - 1)
            {
                Vector2 from = AttackPath[PathIndex];
                Vector2 to = AttackPath[PathIndex + 1];
                float segLen = Vector2.Distance(from, to);
                if (segLen < 0.001f) { PathIndex++; continue; }
                if (PathProgress >= segLen)
                {
                    RecordPathCrossings(_lastAttackPos, to, combat, monsters, PathIndex);
                    _lastAttackPos = to;
                    PathProgress -= segLen;
                    PathIndex++;
                    continue;
                }
                float t = PathProgress / segLen;
                Vector2 pos = Vector2.Lerp(from, to, t);
                transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                RecordPathCrossings(_lastAttackPos, pos, combat, monsters, PathIndex);
                _lastAttackPos = pos;
                return false;
            }
            FinishAttack(combat);
            return true;
        }

        void FinishAttack(Rzz.Battle.CombatDirector combat)
        {
            transform.position = new Vector3(_lastAttackPos.x, _lastAttackPos.y, transform.position.z);
            HomePosition = _lastAttackPos;
            combat.BeginResolve(this);
            State = PlayerState.Idle;
            AttackPath.Clear();
            PathIndex = 0;
            PathProgress = 0;
            UpdatePathLine();
        }

        void PrimePathStartHits(Rzz.Battle.CombatDirector combat, List<BattleMonster> monsters)
        {
            float hitPad = GetPathHitPad();
            Vector2 center = AttackPath[0];
            foreach (var m in monsters)
            {
                if (m == null || !m.IsCombatTargetable()) continue;
                float hitR = m.GetHitboxRadius() + hitPad;
                bool inside = ((Vector2)m.transform.position - center).sqrMagnitude <= hitR * hitR;
                _pathHitInside[m.GetInstanceID()] = inside;
                if (inside)
                    combat.QueueHit(m, 0, center);
            }
        }

        void RecordPathCrossings(Vector2 prev, Vector2 curr, Rzz.Battle.CombatDirector combat, List<BattleMonster> monsters, int segmentIndex)
        {
            float hitPad = GetPathHitPad();
            foreach (var m in monsters)
            {
                if (m == null || !m.IsCombatTargetable()) continue;
                float hitR = m.GetHitboxRadius() + hitPad;
                bool was = _pathHitInside.TryGetValue(m.GetInstanceID(), out var v) && v;
                Vector2 c = m.transform.position;
                bool now = (curr - c).sqrMagnitude <= hitR * hitR;
                if (!was && now)
                {
                    combat.QueueHit(m, segmentIndex, c);
                }
                _pathHitInside[m.GetInstanceID()] = now;
            }
        }

        public DamageInfo GetAttackDamage(float combo)
        {
            float bonus = 1f + ComboDamageBonus * combo;
            float raw = BaseAttack * AttackPowerScale * bonus;
            return RollCritDamage(raw);
        }

        public DamageInfo RollCritDamage(float raw)
        {
            bool isCrit = Random.value < CritRate;
            float final = raw;
            if (isCrit) final *= CritDamage;
            return new DamageInfo { Amount = Mathf.RoundToInt(final), IsCrit = isCrit };
        }

        public float RegisterComboHit()
        {
            ComboHitCount++;
            ComboCount += 1f;
            EventBus.RaiseComboChanged(ComboHitCount);
            return ComboCount;
        }

        public int TakeDamage(int amount)
        {
            if (InvincibleTimer > 0 || IsAttackInvincible()) return 0;
            int final = Mathf.Max(1, amount);
            Hp = Mathf.Max(0, Hp - final);
            InvincibleTimer = _invincibleDuration;
            DamageFlashTimer = 0.42f;
            EventBus.RaisePlayerDamaged(final, Hp);
            if (_battle != null)
            {
                _battle.ShakeCamera(4f, 0.12f);
                var dn = _battle.DamageNumbers;
                if (dn != null) dn.Spawn(transform.position + Vector3.up * 18f, final, false, false);
            }
            return final;
        }

        void ApplyCombatModulate()
        {
            if (_sprite == null) return;
            if (DamageFlashTimer > 0)
            {
                float t = Mathf.Clamp01(DamageFlashTimer / 0.42f);
                _sprite.color = Color.Lerp(_baseSpriteColor, Color.red, t * 0.7f);
            }
            else if (InvincibleTimer > 0)
            {
                float blink = (Mathf.Sin(Time.time * 30f) + 1f) * 0.5f;
                var c = _baseSpriteColor; c.a = Mathf.Lerp(0.4f, 1f, blink);
                _sprite.color = c;
            }
            else
            {
                _sprite.color = _baseSpriteColor;
            }
        }

        void UpdatePathLine()
        {
            if (_pathLine == null) return;
            _pathLine.positionCount = AttackPath.Count;
            for (int i = 0; i < AttackPath.Count; i++)
                _pathLine.SetPosition(i, new Vector3(AttackPath[i].x, AttackPath[i].y, 0));
            _pathLine.enabled = AttackPath.Count >= 2;
        }

        // ── Phase 2.2 upgrade plumbing ──
        public int GetUpgradeLevel(string id)
        {
            return UpgradeStacks.TryGetValue(id, out var lv) ? lv : 0;
        }

        public bool IsUpgradePoolBlocked(string id)
        {
            var def = Boot.Config.GetUpgrade(id);
            if (def == null) return false;
            if (def.once_per_run != 0 && _runAcquiredOnce.Contains(id)) return true;
            if (def.once_per_chapter != 0 && _chapterAcquiredOnce.Contains(id)) return true;
            return false;
        }

        public void ApplyUpgrade(UpgradeData def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            var id = def.id;
            if (def.once_per_run != 0) _runAcquiredOnce.Add(id);
            if (def.once_per_chapter != 0) _chapterAcquiredOnce.Add(id);
            UpgradeStacks[id] = (UpgradeStacks.TryGetValue(id, out var cur) ? cur : 0) + 1;
            RebuildUpgrades();
            if (id == "super_mushroom") Hp = MaxHp;
            EventBus.RaiseUpgradeSelected(id);
        }

        // Mirrors player.gd _rebuild_upgrades. Phase 2.2 implements only the 6 core apply_types;
        // anything else is a no-op (the stack count is still tracked for Phase 3 to wire up).
        public void RebuildUpgrades()
        {
            var c = Boot.Config;
            BaseAttack = c.GetPlayerValue("base_attack", 95f);
            BaseKi = c.GetPlayerValue("base_ki", 234f);
            MaxHp = c.GetPlayerValue("base_hp", 100);
            CritRate = c.GetPlayerValue("base_crit_rate", 0.08f);
            KiRegenSpeed = c.GetPlayerValue("ki_regen_speed", 60f);
            BulletCount = 1;
            AttackSpeedMult = 1f;
            KiRegenMult = 1f;
            BonusAttackMult = 1f;
            LuckRollBlueOffset = 0f;
            LuckRollPurpleOffset = 0f;
            LuckRollOrangeOffset = 0f;
            foreach (var kv in UpgradeStacks)
            {
                int level = kv.Value;
                if (level <= 0) continue;
                var def = c.GetUpgrade(kv.Key);
                if (def == null) continue;
                switch (def.apply_type)
                {
                    case "ki_mult":
                        BaseKi = Mathf.Round(BaseKi * Mathf.Pow(def.apply_value <= 0 ? 1.2f : def.apply_value, level));
                        break;
                    case "bullet_count":
                        BulletCount += level;
                        break;
                    case "crit_rate":
                        CritRate += (def.apply_value <= 0 ? 0.05f : def.apply_value) * level;
                        break;
                    case "godspeed":
                        AttackSpeedMult *= Mathf.Pow(def.apply_value <= 0 ? 1.3f : def.apply_value, level);
                        KiRegenMult *= Mathf.Pow(0.85f, level);
                        break;
                    case "luck_roll":
                        LuckRollBlueOffset -= 0.04f * level;
                        LuckRollPurpleOffset += 0.03f * level;
                        LuckRollOrangeOffset += 0.01f * level;
                        break;
                    case "super_mushroom":
                        MaxHp = Mathf.RoundToInt(MaxHp * 1.2f);
                        break;
                    case "barrage_king":
                        BulletCount += 3;
                        break;
                    // All other apply_types (pierce, bounce_bullet, holy_shield, charge_strike,
                    // mirror_bullet, laser_blast, spirit_bomb, wild_*, divine_god, stillness_heart,
                    // steadfast_guard, etc.) intentionally land in Phase 3.3 — stack count is still
                    // recorded so future logic just reads GetUpgradeLevel(id).
                }
            }
            KiMax = BaseKi;
            KiRegenSpeed *= KiRegenMult;
            Hp = Mathf.Min(Hp, MaxHp);
        }

        public void OnChapterStarted()
        {
            _chapterAcquiredOnce.Clear();
        }

        public Vector3 GetLuckRollOffsets()
        {
            return new Vector3(LuckRollBlueOffset, LuckRollPurpleOffset, LuckRollOrangeOffset);
        }

        public struct DamageInfo
        {
            public int Amount;
            public bool IsCrit;
        }
    }
}
