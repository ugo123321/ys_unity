using System;

namespace Rzz.Core.Data
{
    [Serializable]
    public class MonsterData
    {
        public string kind_id;
        public int spawn_order;
        public int unlock_at_stage;
        public string name_cn;
        public int hp;
        public int def;
        public int attack;
        public float attack_interval;
        public float size;
        public float speed;
        public string color_hex;
        public string grade;
        public int can_move;
        public float attack_range;
        public float arrow_speed;
        public int ranged;
        public int ki_drain_on_hit;
        public int max_split_tier;
        public int split_count;
        public int exp_reward;
        public string character_folder;
        public string sprite_prefix;
        public string projectile_folder;
        public string attack_pattern;
        public string projectile_effect;
        public int spread_count;
        public float spread_angle_deg;
        public int bounce_count;
        public string sprite_tint_hex;
    }
}
