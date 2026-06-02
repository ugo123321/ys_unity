using System;

namespace Rzz.Core.Data
{
    // Schema used by player.json and game_tuning.json:
    // [{ "key": "...", "value": <any>, "description": "..." }, ...]
    // Stored as object so callers can cast to int/float/string.
    [Serializable]
    public class KeyValueEntry
    {
        public string key;
        public object value;
        public string description;
    }
}
