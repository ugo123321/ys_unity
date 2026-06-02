using System;

namespace Rzz.Core.Data
{
    [Serializable]
    public class UpgradeData
    {
        public string id;
        public string name_cn;
        public string rarity;
        public string icon;
        public string icon_file;
        public string desc_cn;
        public int is_pet;
        public int max_level;
        public int once_per_run;
        public int once_per_chapter;
        public string apply_type;
        public float apply_value;
        public string category;
        public string trigger_condition;
        public string effect_pack;
        public string effect_name;
    }
}
