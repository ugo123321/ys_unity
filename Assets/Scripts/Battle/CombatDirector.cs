using System.Collections.Generic;
using UnityEngine;
using Rzz.Core;
using Rzz.Entities;

namespace Rzz.Battle
{
    // Ports scripts/core/combat_director.gd (Phase 1 simplified).
    // Phase 1 omits: afterimages, slash_hit_fx, sound, abilities.on_combo_hit, shield blocks,
    // particles death effect, path preview highlights.
    public class CombatDirector : MonoBehaviour
    {
        public bool Resolving { get; private set; }
        public bool RoundAttackResolved { get; private set; } = true;

        struct PendingHit
        {
            public BattleMonster Monster;
            public int SegmentIndex;
            public Vector2 Pos;
        }

        readonly Queue<PendingHit> _pendingHits = new Queue<PendingHit>(64);
        float _resolveTimer;
        BattleController _battle;

        float _firstHitDelay = 0.04f;
        float _hitInterval = 0.012f;
        float _deathStagger = 0.012f;
        float _deathStaggerAccumulator;

        readonly List<DamageNumber> _damageNumbers = new List<DamageNumber>(32);

        public void Setup(BattleController battle)
        {
            _battle = battle;
            var t = Boot.Config;
            _firstHitDelay = t.GetTuning("combat_first_hit_delay", 0.04f);
            _hitInterval = t.GetTuning("combat_hit_interval", 0.012f);
            _deathStagger = t.GetTuning("combat_death_stagger", 0.012f);
        }

        public void ResetForStage()
        {
            _pendingHits.Clear();
            Resolving = false;
            RoundAttackResolved = true;
            _resolveTimer = 0;
            _deathStaggerAccumulator = 0;
            _damageNumbers.Clear();
        }

        public bool IsResolving() => Resolving;

        public bool ShouldMonstersAttack(BattlePlayer player)
        {
            if (player == null) return true;
            if (player.State == BattlePlayer.PlayerState.BulletTime || player.State == BattlePlayer.PlayerState.Attacking) return false;
            if (Resolving) return false;
            return true;
        }

        public void BeginRoundAttack()
        {
            RoundAttackResolved = false;
            _pendingHits.Clear();
            _deathStaggerAccumulator = 0;
        }

        public bool ConsumeRoundAttack()
        {
            if (RoundAttackResolved) return false;
            RoundAttackResolved = true;
            return true;
        }

        public void QueueHit(BattleMonster monster, int segmentIndex, Vector2 hitPos)
        {
            _pendingHits.Enqueue(new PendingHit { Monster = monster, SegmentIndex = segmentIndex, Pos = hitPos });
        }

        public float ScheduleDeathFade()
        {
            float d = _deathStaggerAccumulator;
            _deathStaggerAccumulator += _deathStagger;
            return d;
        }

        public void BeginResolve(BattlePlayer player)
        {
            if (_pendingHits.Count == 0)
            {
                FinishResolve(player);
                return;
            }
            Resolving = true;
            _resolveTimer = _firstHitDelay;
        }

        public void UpdateResolve(float delta, BattlePlayer player)
        {
            if (!Resolving) return;
            _resolveTimer -= delta;
            if (_resolveTimer > 0) return;
            if (_pendingHits.Count == 0)
            {
                FinishResolve(player);
                return;
            }
            var hit = _pendingHits.Dequeue();
            ApplyHit(player, hit);
            _resolveTimer = _hitInterval;
        }

        void ApplyHit(BattlePlayer player, PendingHit hit)
        {
            var m = hit.Monster;
            if (m == null || !m.IsCombatTargetable()) return;
            var dmgInfo = player.GetAttackDamage(player.ComboCount);
            var result = m.TakeDamage(dmgInfo.Amount, player.transform.position);
            float combo = player.RegisterComboHit();
            SpawnDamageNumber(hit.Pos, result.Damage, dmgInfo.IsCrit);
            if (dmgInfo.IsCrit) _battle.ShakeCamera(6f + Mathf.Min(combo * 0.15f, 4f), 0.14f);
            else _battle.ShakeCamera(3f, 0.08f);
        }

        void FinishResolve(BattlePlayer player)
        {
            Resolving = false;
            RoundAttackResolved = true;
        }

        public void SpawnDamageNumber(Vector2 pos, int damage, bool isCrit)
        {
            _damageNumbers.Add(new DamageNumber
            {
                Pos = pos,
                Damage = damage,
                IsCrit = isCrit,
                Life = 0.65f,
                MaxLife = 0.65f,
                Vy = 80f,
            });
            var ui = _battle != null ? _battle.DamageNumbers : null;
            if (ui != null) ui.Spawn(new Vector3(pos.x, pos.y, 0f), damage, isCrit, false);
        }

        public void UpdateDamageNumbers(float delta)
        {
            for (int i = _damageNumbers.Count - 1; i >= 0; i--)
            {
                var dn = _damageNumbers[i];
                dn.Life -= delta;
                dn.Pos.y += dn.Vy * delta;
                dn.Vy *= 0.92f;
                if (dn.Life <= 0) { _damageNumbers.RemoveAt(i); continue; }
                _damageNumbers[i] = dn;
            }
        }

        public IReadOnlyList<DamageNumber> DamageNumbers => _damageNumbers;

        public struct DamageNumber
        {
            public Vector2 Pos;
            public int Damage;
            public bool IsCrit;
            public float Life;
            public float MaxLife;
            public float Vy;
        }
    }
}
