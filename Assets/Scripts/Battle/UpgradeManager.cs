using System.Collections.Generic;
using UnityEngine;
using Rzz.Core;
using Rzz.Core.Data;
using Rzz.Entities;

namespace Rzz.Battle
{
    // Ports scripts/core/upgrade_manager.gd. Phase 2.2: rolls 3 choices weighted by rarity
    // (blue/purple/orange via upgrade_fx.json), filters by player's once_per_run / once_per_chapter
    // / max_level state, and applies the selection.
    public class UpgradeManager
    {
        public bool Active { get; private set; }
        public List<UpgradeData> Choices { get; } = new List<UpgradeData>(3);
        public string RolledRarity { get; private set; } = "blue";

        const int CHOICE_COUNT = 3;

        public void GenerateChoices(BattlePlayer player)
        {
            Active = true;
            Choices.Clear();
            RolledRarity = RollRarity(player);
            var pool = BuildPool(RolledRarity, player);
            if (pool.Count == 0) pool = BuildPool(null, player);
            var available = new List<UpgradeData>(pool);
            for (int i = 0; i < CHOICE_COUNT; i++)
            {
                if (available.Count == 0)
                {
                    var fallback = pool.Count > 0 ? pool : BuildPool(null, player);
                    if (fallback.Count == 0 && Boot.Config.Upgrades.Count > 0)
                        Choices.Add(Boot.Config.Upgrades[Random.Range(0, Boot.Config.Upgrades.Count)]);
                    else if (fallback.Count > 0)
                        Choices.Add(fallback[Random.Range(0, fallback.Count)]);
                }
                else
                {
                    int idx = Random.Range(0, available.Count);
                    Choices.Add(available[idx]);
                    available.RemoveAt(idx);
                }
            }
        }

        List<UpgradeData> BuildPool(string rarity, BattlePlayer player)
        {
            var pool = new List<UpgradeData>();
            foreach (var u in Boot.Config.Upgrades)
            {
                if (!string.IsNullOrEmpty(rarity) && u.rarity != rarity) continue;
                if (!IsAvailable(u, player)) continue;
                pool.Add(u);
            }
            return pool;
        }

        bool IsAvailable(UpgradeData def, BattlePlayer player)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return false;
            if (player == null) return true;
            if (player.IsUpgradePoolBlocked(def.id)) return false;
            int maxLv = def.max_level <= 0 ? 9 : def.max_level;
            if (player.GetUpgradeLevel(def.id) >= maxLv) return false;
            return true;
        }

        public UpgradeData Select(int index, BattlePlayer player)
        {
            if (!Active || index < 0 || index >= Choices.Count) return null;
            var def = Choices[index];
            player.ApplyUpgrade(def);
            Active = false;
            Choices.Clear();
            return def;
        }

        string RollRarity(BattlePlayer player)
        {
            float blueW = 0.30f, purpleW = 0.30f, orangeW = 0.10f;
            if (Boot.Config.UpgradeFx.TryGetValue("blue", out var b))   blueW = b.Value<float?>("chance") ?? blueW;
            if (Boot.Config.UpgradeFx.TryGetValue("purple", out var p)) purpleW = p.Value<float?>("chance") ?? purpleW;
            if (Boot.Config.UpgradeFx.TryGetValue("orange", out var o)) orangeW = o.Value<float?>("chance") ?? orangeW;
            if (player != null)
            {
                var off = player.GetLuckRollOffsets();
                blueW = Mathf.Max(0.01f, blueW + off.x);
                purpleW = Mathf.Max(0.01f, purpleW + off.y);
                orangeW = Mathf.Max(0.01f, orangeW + off.z);
            }
            float total = blueW + purpleW + orangeW;
            float r = Random.value * total;
            if (r <= blueW) return "blue";
            if (r <= blueW + purpleW) return "purple";
            return "orange";
        }
    }
}
