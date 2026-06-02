using System;

namespace Rzz.Core
{
    // Ports scripts/autoload/event_bus.gd. Static facade so any script can publish/subscribe
    // without going through DI. Subscribers should unsubscribe on disable/destroy.
    public static class EventBus
    {
        public static event Action<GameState, GameState> GameStateChanged;
        public static event Action<int> StageStarted;
        public static event Action<int> StageCleared;
        public static event Action<int, int> PlayerDamaged;   // (amount, remainingHp)
        public static event Action<int, int> PlayerHealed;    // (amount, remainingHp)
        public static event Action<object> MonsterKilled;     // monster ref (typed in Phase 2)
        public static event Action<int, int, int> ExpChanged; // (level, exp, expToNext)
        public static event Action<string> UpgradeSelected;
        public static event Action<int> ComboChanged;
        public static event Action<int> GoldChanged;
        public static event Action EquipmentChanged;

        public static void RaiseGameStateChanged(GameState oldS, GameState newS) => GameStateChanged?.Invoke(oldS, newS);
        public static void RaiseStageStarted(int idx) => StageStarted?.Invoke(idx);
        public static void RaiseStageCleared(int idx) => StageCleared?.Invoke(idx);
        public static void RaisePlayerDamaged(int amount, int hp) => PlayerDamaged?.Invoke(amount, hp);
        public static void RaisePlayerHealed(int amount, int hp) => PlayerHealed?.Invoke(amount, hp);
        public static void RaiseMonsterKilled(object monster) => MonsterKilled?.Invoke(monster);
        public static void RaiseExpChanged(int lvl, int exp, int next) => ExpChanged?.Invoke(lvl, exp, next);
        public static void RaiseUpgradeSelected(string id) => UpgradeSelected?.Invoke(id);
        public static void RaiseComboChanged(int combo) => ComboChanged?.Invoke(combo);
        public static void RaiseGoldChanged(int total) => GoldChanged?.Invoke(total);
        public static void RaiseEquipmentChanged() => EquipmentChanged?.Invoke();
    }
}
