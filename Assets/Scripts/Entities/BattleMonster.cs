using UnityEngine;
using Rzz.Core;
using Rzz.Core.Data;

namespace Rzz.Entities
{
    // Ports scripts/entities/monster.gd (Phase 1 simplified: melee chase only).
    // Phase 1 omits: ranged attack patterns, shield, freeze, burn, vulnerable mark, split.
    public class BattleMonster : MonoBehaviour
    {
        public string KindId = "NORMAL";
        public string DisplayName = "";
        public bool Alive = true;
        public bool Dying { get; private set; }

        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Defense { get; private set; }
        public int Attack { get; private set; }
        public float AttackInterval = 1.0f;
        public float AttackTimer;

        public float MoveSpeed = 19f;
        public bool CanMove = true;
        public float HitboxRadius = 13f;
        public float Facing = 1f;

        // Death animation
        public float SpawnLockTimer;
        public float DeathDelay;
        public float DeathTimer;
        public float DeathFadeDur = 0.28f;
        public float HurtReactionTimer;

        [SerializeField] SpriteRenderer _sprite;
        Vector3 _deathBaseScale;
        Color _baseColor = Color.white;
        const float MELEE_STOP_PAD = 2f;

        public float GetHitboxRadius() => HitboxRadius;
        public bool IsCombatTargetable() => Alive && !Dying && SpawnLockTimer <= 0;
        public bool IsSpawnLocked() => SpawnLockTimer > 0;
        public bool IsFrozen() => false; // Phase 3

        public void Setup(string kindId, int stageIndex, Vector2 spawnPos)
        {
            KindId = kindId;
            var stats = Boot.Config.ScaledMonsterStats(kindId, stageIndex);
            if (stats == null)
            {
                Debug.LogWarning($"[Monster] Unknown kind_id: {kindId}");
                stats = new MonsterData { hp = 1, attack = 1, attack_interval = 1f, speed = 19f, size = 13f };
            }
            MaxHp = stats.hp;
            Hp = MaxHp;
            Defense = stats.def;
            Attack = stats.attack;
            AttackInterval = stats.attack_interval;
            MoveSpeed = stats.speed;
            CanMove = stats.can_move != 0;
            HitboxRadius = Boot.Config.ScaleWorld(stats.size);
            DisplayName = stats.name_cn ?? kindId;
            transform.position = new Vector3(spawnPos.x, spawnPos.y, 0);
            SpawnLockTimer = Boot.Config.GetTuning("monster_spawn_anim", 0.42f);
            DeathFadeDur = Boot.Config.GetTuning("monster_death_fade", 0.28f);
            _deathBaseScale = transform.localScale;
            if (_sprite != null) _baseColor = _sprite.color;
        }

        public void UpdateAi(float delta, BattlePlayer player, Rzz.Battle.BattleController battle)
        {
            if (!Alive || Dying || player == null) return;
            if (SpawnLockTimer > 0) { SpawnLockTimer -= delta; return; }
            if (HurtReactionTimer > 0) HurtReactionTimer -= delta;

            Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
            Facing = toPlayer.x >= 0 ? 1f : -1f;
            if (_sprite != null) _sprite.flipX = Facing < 0;

            if (CanMove)
            {
                float dist = toPlayer.magnitude;
                float stopDist = player.GetEffectiveRadius() + HitboxRadius + MELEE_STOP_PAD;
                if (dist > stopDist)
                {
                    Vector2 dir = toPlayer.normalized;
                    transform.position += (Vector3)(dir * MoveSpeed * delta);
                }
            }

            // Attack
            bool canAttack = battle.Combat.ShouldMonstersAttack(player);
            if (canAttack && HurtReactionTimer <= 0)
            {
                AttackTimer -= delta;
                if (AttackTimer > 0) return;
                float meleeRange = player.GetEffectiveRadius() + HitboxRadius + MELEE_STOP_PAD + 4f;
                if (toPlayer.magnitude > meleeRange) return;
                AttackTimer = AttackInterval;
                player.TakeDamage(Attack);
            }
        }

        public DamageResult TakeDamage(int rawDamage, Vector2 fromPos)
        {
            if (!Alive || Dying) return new DamageResult { Damage = 0, IsCrit = false };
            int actual = Mathf.Max(1, Mathf.RoundToInt(rawDamage - Defense));
            Hp -= actual;
            var result = new DamageResult { Damage = actual, IsCrit = false };
            if (Hp <= 0)
            {
                Hp = 0;
                BeginDying(0);
                result.StartedDying = true;
            }
            else
            {
                HurtReactionTimer = 0.18f;
                Facing = fromPos.x >= transform.position.x ? 1f : -1f;
                if (_sprite != null) _sprite.flipX = Facing < 0;
            }
            return result;
        }

        public bool BeginDying(float staggerDelay)
        {
            if (Dying || !Alive) return false;
            Dying = true;
            DeathFadeDur = Boot.Config.GetTuning("monster_death_fade", 0.28f);
            DeathDelay = Mathf.Max(0, staggerDelay);
            DeathTimer = DeathFadeDur;
            EventBus.RaiseMonsterKilled(this);
            return true;
        }

        public void UpdateDeath(float delta)
        {
            if (!Dying) return;
            if (DeathDelay > 0) { DeathDelay -= delta; return; }
            DeathTimer -= delta;
            float alpha = Mathf.Clamp01(DeathTimer / Mathf.Max(0.001f, DeathFadeDur));
            float shrink = Mathf.Lerp(0.72f, 1f, alpha);
            transform.localScale = _deathBaseScale * shrink;
            if (_sprite != null)
            {
                var c = _baseColor; c.a = alpha;
                _sprite.color = c;
            }
            if (DeathTimer <= 0) FinishDeath();
        }

        void FinishDeath()
        {
            Alive = false;
            gameObject.SetActive(false);
        }

        public struct DamageResult
        {
            public int Damage;
            public bool IsCrit;
            public bool BlockedByShield;
            public bool StartedDying;
        }
    }
}
