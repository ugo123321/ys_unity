using UnityEngine;
using Rzz.Core;
using Rzz.Entities;

namespace Rzz.Battle
{
    // Ports scripts/core/experience_manager.gd. Tracks level/exp, banks pending level-ups,
    // and triggers BattleController.EnterLevelUp once it's safe to interrupt play.
    public class ExperienceManager : MonoBehaviour
    {
        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int ExpToNext { get; private set; } = 100;
        public int PendingLevelUps { get; private set; }

        BattleController _battle;

        public void Setup(BattleController battle)
        {
            _battle = battle;
            EventBus.MonsterKilled += HandleMonsterKilled;
        }

        void OnDestroy()
        {
            EventBus.MonsterKilled -= HandleMonsterKilled;
        }

        public void ResetRun()
        {
            Level = 1;
            Exp = 0;
            ExpToNext = CalcExpToNext(1);
            PendingLevelUps = 0;
            EventBus.RaiseExpChanged(Level, Exp, ExpToNext);
        }

        int CalcExpToNext(int currentLevel)
        {
            float baseExp = Boot.Config.GetTuning("exp_base_to_level", 100f);
            float growth = Boot.Config.GetTuning("exp_growth", 1.22f);
            return Mathf.RoundToInt(baseExp * Mathf.Pow(growth, currentLevel - 1));
        }

        void HandleMonsterKilled(object monster)
        {
            var bm = monster as BattleMonster;
            if (bm == null) return;
            var row = Boot.Config.GetMonster(bm.KindId);
            int reward = row != null ? row.exp_reward : 2;
            if (reward <= 0) reward = 2;
            AddExp(reward);
        }

        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            Exp += amount;
            while (Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                ExpToNext = CalcExpToNext(Level);
                PendingLevelUps++;
            }
            EventBus.RaiseExpChanged(Level, Exp, ExpToNext);
        }

        // Called every frame by BattleController in Playing state. Returns true when we
        // successfully transitioned the battle into LevelUp.
        public bool TryTriggerUpgrade(BattleController battle)
        {
            if (PendingLevelUps <= 0) return false;
            if (battle.State != GameState.Playing) return false;
            if (battle.Combat.IsResolving()) return false;
            if (battle.Player.State != BattlePlayer.PlayerState.Idle) return false;
            PendingLevelUps--;
            battle.EnterLevelUp();
            return true;
        }
    }
}
