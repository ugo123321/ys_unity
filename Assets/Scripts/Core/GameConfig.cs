using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rzz.Core.Data;
using UnityEngine;

namespace Rzz.Core
{
    // Ports scripts/autoload/game_config.gd. JSON lives in StreamingAssets/Data and is
    // read on Boot before any game system initializes. Heterogeneous "value" fields
    // (int/float/string) are kept as JToken so callers cast on demand.
    public class GameConfig
    {
        const float BASE_LOGICAL_WIDTH = 390f;
        const float BASE_LOGICAL_HEIGHT = 700f;
        const float DEFAULT_LOGICAL_WIDTH = 720f;
        const float DEFAULT_LOGICAL_HEIGHT = 1280f;

        public List<JObject> Chapters { get; private set; } = new List<JObject>();
        public List<StageData> Stages { get; private set; } = new List<StageData>();
        public Dictionary<string, MonsterData> Monsters { get; private set; } = new Dictionary<string, MonsterData>();
        public Dictionary<string, JToken> Player { get; private set; } = new Dictionary<string, JToken>();
        public List<UpgradeData> Upgrades { get; private set; } = new List<UpgradeData>();
        public Dictionary<string, JObject> UpgradeFx { get; private set; } = new Dictionary<string, JObject>();
        public Dictionary<string, JToken> Tuning { get; private set; } = new Dictionary<string, JToken>();
        public List<JObject> AssetMapping { get; private set; } = new List<JObject>();
        public JObject Bosses { get; private set; } = new JObject();
        public JObject BuffOrbs { get; private set; } = new JObject();

        public void Reload()
        {
            Chapters = LoadArray<JObject>("chapters");
            Stages = LoadArray<StageData>("stages");

            Monsters.Clear();
            foreach (var m in LoadArray<MonsterData>("monsters"))
                Monsters[m.kind_id ?? ""] = m;

            Player.Clear();
            foreach (var row in LoadArray<JObject>("player"))
                Player[row.Value<string>("key") ?? ""] = row["value"];

            Upgrades = LoadArray<UpgradeData>("upgrades");

            UpgradeFx.Clear();
            foreach (var row in LoadArray<JObject>("upgrade_fx"))
                UpgradeFx[row.Value<string>("rarity") ?? "blue"] = row;

            Tuning.Clear();
            foreach (var row in LoadArray<JObject>("game_tuning"))
                Tuning[row.Value<string>("key") ?? ""] = row["value"];

            AssetMapping = LoadArray<JObject>("asset_mapping");
            Bosses = LoadDict("bosses");
            BuffOrbs = LoadDict("buff_orbs");

            ApplyFrameSettings();
        }

        void ApplyFrameSettings()
        {
            int fps = GetTuning("target_fps", 60);
            Application.targetFrameRate = fps > 0 ? fps : -1;
        }

        public T GetTuning<T>(string key, T defaultValue)
        {
            if (Tuning.TryGetValue(key, out var token) && token != null)
                return token.ToObject<T>();
            return defaultValue;
        }

        public T GetPlayerValue<T>(string key, T defaultValue)
        {
            if (Player.TryGetValue(key, out var token) && token != null)
                return token.ToObject<T>();
            return defaultValue;
        }

        public Vector2 GetLogicalSize()
        {
            return new Vector2(
                GetTuning("logical_width", DEFAULT_LOGICAL_WIDTH),
                GetTuning("logical_height", DEFAULT_LOGICAL_HEIGHT));
        }

        public float GetWorldScale()
        {
            return Mathf.Max(0.1f, GetTuning("world_scale", 1f));
        }

        public float GetUiScale()
        {
            return Mathf.Max(0.1f, GetTuning("ui_scale", 1f));
        }

        public float GetResolutionScale()
        {
            var logical = GetLogicalSize();
            return Mathf.Max(0.1f, Mathf.Min(logical.x / BASE_LOGICAL_WIDTH, logical.y / BASE_LOGICAL_HEIGHT));
        }

        public float GetUiLayoutScale()
        {
            var logical = GetLogicalSize();
            return Mathf.Max(0.1f, Mathf.Min(logical.x / DEFAULT_LOGICAL_WIDTH, logical.y / DEFAULT_LOGICAL_HEIGHT)) * GetUiScale();
        }

        public float ScaleWorld(float value)
        {
            return value * GetResolutionScale() * GetWorldScale();
        }

        public float ScaleUi(float value)
        {
            return value * GetUiLayoutScale();
        }

        public MonsterData GetMonster(string kindId)
        {
            return Monsters.TryGetValue(kindId, out var m) ? m : null;
        }

        public StageData GetStage(int index)
        {
            if (index < 0 || index >= Stages.Count) return null;
            return Stages[index];
        }

        public UpgradeData GetUpgrade(string id)
        {
            return Upgrades.Find(u => u.id == id);
        }

        public List<UpgradeData> GetUpgradesByCategory(string category)
        {
            return Upgrades.FindAll(u => u.category == category);
        }

        // Apply per-stage HP/DEF/ATK power curves from tuning, matching scaled_monster_stats().
        public MonsterData ScaledMonsterStats(string kindId, int stageIndex)
        {
            var src = GetMonster(kindId);
            if (src == null) return null;
            var clone = new MonsterData
            {
                kind_id = src.kind_id,
                spawn_order = src.spawn_order,
                unlock_at_stage = src.unlock_at_stage,
                name_cn = src.name_cn,
                hp = src.hp,
                def = src.def,
                attack = src.attack,
                attack_interval = src.attack_interval,
                size = src.size,
                speed = src.speed,
                color_hex = src.color_hex,
                grade = src.grade,
                can_move = src.can_move,
                attack_range = src.attack_range,
                arrow_speed = src.arrow_speed,
                ranged = src.ranged,
                ki_drain_on_hit = src.ki_drain_on_hit,
                max_split_tier = src.max_split_tier,
                split_count = src.split_count,
                exp_reward = src.exp_reward,
                character_folder = src.character_folder,
                sprite_prefix = src.sprite_prefix,
                projectile_folder = src.projectile_folder,
                attack_pattern = src.attack_pattern,
                projectile_effect = src.projectile_effect,
                spread_count = src.spread_count,
                spread_angle_deg = src.spread_angle_deg,
                bounce_count = src.bounce_count,
                sprite_tint_hex = src.sprite_tint_hex,
            };
            float hpG = GetTuning("stage_hp_growth", 1.2f);
            float defG = GetTuning("stage_def_growth", 1.1f);
            float atkG = GetTuning("stage_atk_growth", 1.12f);
            clone.hp = Mathf.RoundToInt(src.hp * Mathf.Pow(hpG, stageIndex));
            clone.def = Mathf.RoundToInt(src.def * Mathf.Pow(defG, stageIndex));
            clone.attack = Mathf.RoundToInt(src.attack * Mathf.Pow(atkG, stageIndex));
            float speedMul = Mathf.Max(0.1f, GetTuning("monster_speed_mul", 1.15f));
            clone.speed = Mathf.Max(1f, src.speed * speedMul);
            return clone;
        }

        List<T> LoadArray<T>(string name)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Data", name + ".json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("GameConfig: missing " + path);
                return new List<T>();
            }
            var text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<T>>(text) ?? new List<T>();
        }

        JObject LoadDict(string name)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Data", name + ".json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("GameConfig: missing " + path);
                return new JObject();
            }
            var text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<JObject>(text) ?? new JObject();
        }
    }
}
