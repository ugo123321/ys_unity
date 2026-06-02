using UnityEngine;
using Rzz.Core;
using Rzz.Entities;

namespace Rzz.Systems
{
    // Ports scripts/systems/path_input.gd. Stateless except for "drawing" flag.
    // Phase 1: BattleController feeds it screen positions via mouse/touch input dispatch.
    public class PathInput : MonoBehaviour
    {
        public bool Drawing { get; private set; }
        Rzz.Battle.BattleController _battle;

        public void Setup(Rzz.Battle.BattleController battle) => _battle = battle;

        public void HandleStart(Vector2 screenPos)
        {
            if (_battle == null || _battle.State != GameState.Playing) return;
            var player = _battle.Player;
            if (player == null || player.State != BattlePlayer.PlayerState.Idle) return;
            Vector2 worldPos = _battle.ScreenToWorld(screenPos);
            if (!_battle.IsInBounds(worldPos)) return;
            if (Vector2.Distance(worldPos, player.HomePosition) > player.GetTriggerRadius())
            {
                Debug.Log("[PathInput] start outside trigger radius");
                return;
            }
            if (!player.IsKiFull())
            {
                Debug.Log("[PathInput] KI not full");
                return;
            }
            Drawing = true;
            _battle.EnterBulletTime();
            player.StartBulletTime();
            player.AddPathPoint(worldPos);
        }

        public void HandleMove(Vector2 screenPos)
        {
            if (!Drawing) return;
            var player = _battle.Player;
            if (player == null || player.State != BattlePlayer.PlayerState.BulletTime) return;
            Vector2 worldPos = _battle.ScreenToWorld(screenPos);
            if (!_battle.IsInBounds(worldPos)) return;
            Vector2 last = player.AttackPath.Count > 0
                ? player.AttackPath[player.AttackPath.Count - 1]
                : player.HomePosition;
            float step = Vector2.Distance(last, worldPos);
            if (step < 2f) return;
            if (!player.ConsumeKiByDistance(step))
            {
                Drawing = false;
                if (player.AttackPath.Count >= 2) _battle.ExitBulletTime(false);
                else _battle.ExitBulletTime(true);
                return;
            }
            player.AddPathPoint(worldPos);
        }

        public void HandleEnd()
        {
            var player = _battle != null ? _battle.Player : null;
            if (player == null || player.State != BattlePlayer.PlayerState.BulletTime)
            {
                Drawing = false;
                return;
            }
            Drawing = false;
            if (player.AttackPath.Count < 2) _battle.ExitBulletTime(true);
            else _battle.ExitBulletTime(false);
        }

        public void CancelActive()
        {
            Drawing = false;
            var player = _battle != null ? _battle.Player : null;
            if (player != null && player.State == BattlePlayer.PlayerState.BulletTime)
                _battle.ExitBulletTime(true);
        }
    }
}
