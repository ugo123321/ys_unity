using UnityEngine;
using Rzz.Entities;

namespace Rzz.UI
{
    // Draws a faint circle ring around the player at GetTriggerRadius distance, so the
    // player can see where they can "grab" to start drawing the attack path. Fades in
    // when KI is full (ready to start) and out otherwise.
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerTriggerRing : MonoBehaviour
    {
        [SerializeField] BattlePlayer _player;
        [SerializeField] int _segments = 48;
        [SerializeField] float _fadeInSpeed = 4f;
        [SerializeField] float _fadeOutSpeed = 6f;
        [SerializeField] float _radiusScale = 0.5f;
        [SerializeField] Vector2 _centerOffset = Vector2.zero;

        LineRenderer _line;
        float _alpha;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            if (_player == null) _player = GetComponentInParent<BattlePlayer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = _segments;
            _line.startWidth = 1.5f;
            _line.endWidth = 1.5f;
            _line.numCapVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.material = new Material(Shader.Find("Sprites/Default"));
        }

        void LateUpdate()
        {
            if (_player == null) { _line.enabled = false; return; }
            bool ready = _player.State == BattlePlayer.PlayerState.Idle && _player.IsKiFull();
            float target = ready ? 1f : 0f;
            float speed = ready ? _fadeInSpeed : _fadeOutSpeed;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * speed);
            if (_alpha <= 0.001f) { _line.enabled = false; return; }
            _line.enabled = true;
            float r = _player.GetTriggerRadius() * _radiusScale;
            Vector2 home = _player.HomePosition;
            Vector3 center = new Vector3(home.x + _centerOffset.x, home.y + _centerOffset.y, _player.transform.position.z);
            for (int i = 0; i < _segments; i++)
            {
                float t = (float)i / _segments * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(center.x + Mathf.Cos(t) * r, center.y + Mathf.Sin(t) * r, center.z));
            }
            var c = new Color(1f, 0.95f, 0.5f, 0.6f * _alpha);
            _line.startColor = c;
            _line.endColor = c;
        }
    }
}

