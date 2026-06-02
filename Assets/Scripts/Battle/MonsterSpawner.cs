using System.Collections.Generic;
using UnityEngine;
using Rzz.Core;
using Rzz.Core.Data;
using Rzz.Entities;

namespace Rzz.Battle
{
    // Ports scripts/core/monster_spawner.gd (Phase 1 simplified).
    // Phase 1: reads stages.json for stage 0 and spawns NORMAL monsters at the top edge,
    // walking down toward the player. No elite/shield/archer/boss/split/cluster timing.
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] BattleMonster _monsterPrefab;
        [SerializeField] Transform _monsterContainer;

        readonly List<BattleMonster> _monsters = new List<BattleMonster>(32);
        BattleController _battle;
        int _stageIndex = -1;

        public void Setup(BattleController battle, BattleMonster prefab, Transform container)
        {
            _battle = battle;
            if (prefab != null) _monsterPrefab = prefab;
            if (container != null) _monsterContainer = container;
        }

        public void Reset()
        {
            for (int i = _monsters.Count - 1; i >= 0; i--)
            {
                if (_monsters[i] != null) Destroy(_monsters[i].gameObject);
            }
            _monsters.Clear();
            _stageIndex = -1;
        }

        public void SpawnStage(int stageIndex)
        {
            Reset();
            _stageIndex = stageIndex;
            var stage = Boot.Config.GetStage(stageIndex);
            if (stage == null)
            {
                Debug.LogWarning($"[Spawner] No stage data for index {stageIndex}");
                return;
            }
            SpawnKind("NORMAL", stage.normal);
            SpawnKind("ARCHER", stage.archer);    // Phase 1: archer falls back to melee chase
            SpawnKind("ELITE", stage.elite);
        }

        void SpawnKind(string kindId, int count)
        {
            if (count <= 0 || _monsterPrefab == null) return;
            var logical = Boot.Config.GetLogicalSize();
            float w = logical.x;
            float h = logical.y;
            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(60f, w - 60f);
                // Godot spawns monsters in upper portion (y 120-360). Unity Y is up so mirror.
                float y = h - Random.Range(120f, 360f);
                var inst = Instantiate(_monsterPrefab, _monsterContainer);
                inst.gameObject.SetActive(true);
                inst.Setup(kindId, _stageIndex, new Vector2(x, y));
                _monsters.Add(inst);
            }
        }

        public List<BattleMonster> GetActiveMonsters() => _monsters;

        public void UpdateSpawns(float delta)
        {
            // Phase 1: no time-staggered spawning, all spawn upfront in SpawnStage.
            // Cleanup deactivated monsters.
            for (int i = _monsters.Count - 1; i >= 0; i--)
            {
                if (_monsters[i] == null || !_monsters[i].gameObject.activeSelf)
                    _monsters.RemoveAt(i);
            }
        }

        public bool AllDead()
        {
            foreach (var m in _monsters)
                if (m != null && m.Alive && !m.Dying) return false;
            return true;
        }
    }
}
