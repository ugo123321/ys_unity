using System.Collections.Generic;
using UnityEngine;

namespace Rzz.UI
{
    // Phase 2.3b damage numbers. World-space TMP labels pooled & spawned on demand.
    // Crit = 28pt red, heal = 19pt green with '+', normal = 19pt white.
    public class DamageNumbers : MonoBehaviour
    {
        [SerializeField] DamageNumberLabel _prefab;
        [SerializeField] int _poolSize = 32;

        readonly List<DamageNumberLabel> _pool = new List<DamageNumberLabel>();

        void Awake()
        {
            if (_prefab == null) return;
            for (int i = 0; i < _poolSize; i++)
            {
                var inst = Instantiate(_prefab, transform);
                inst.gameObject.SetActive(false);
                _pool.Add(inst);
            }
        }

        public void Spawn(Vector3 worldPos, int damage, bool isCrit, bool isHeal)
        {
            if (_prefab == null) return;
            var label = Rent();
            if (label == null) return;
            string text;
            Color color;
            float size;
            if (isHeal)
            {
                text = "+" + damage;
                color = new Color(0.408f, 0.847f, 0.471f, 1f);
                size = 19f;
            }
            else if (isCrit)
            {
                text = damage.ToString();
                color = new Color(1f, 0.251f, 0.251f, 1f);
                size = 28f;
            }
            else
            {
                text = damage.ToString();
                color = Color.white;
                size = 19f;
            }
            label.Init(worldPos, text, color, size);
        }

        DamageNumberLabel Rent()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].IsAlive) return _pool[i];
            }
            var inst = Instantiate(_prefab, transform);
            _pool.Add(inst);
            return inst;
        }
    }
}
