using UnityEngine;
using TMPro;

namespace Rzz.UI
{
    // One floating damage label, lives ~0.85s, drifts up, fades out.
    // Pooled by DamageNumbers — Init() resets state, OnFinished signal via _alive flag.
    public class DamageNumberLabel : MonoBehaviour
    {
        [SerializeField] TMP_Text _text;
        [SerializeField] CanvasGroup _group;

        const float Lifetime = 0.85f;
        const float UpSpeed = 68f;

        float _age;
        bool _alive;

        public bool IsAlive => _alive;

        void Reset()
        {
            _text = GetComponentInChildren<TMP_Text>();
            _group = GetComponent<CanvasGroup>();
        }

        public void Init(Vector3 worldPos, string textValue, Color color, float fontSize)
        {
            transform.position = worldPos;
            if (_text != null)
            {
                _text.text = textValue;
                _text.color = color;
                _text.fontSize = fontSize;
            }
            if (_group != null) _group.alpha = 1f;
            _age = 0f;
            _alive = true;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_alive) return;
            float dt = Time.deltaTime;
            _age += dt;
            transform.position += Vector3.up * (UpSpeed * dt);
            float k = 1f - Mathf.Clamp01(_age / Lifetime);
            if (_group != null) _group.alpha = k;
            if (_age >= Lifetime)
            {
                _alive = false;
                gameObject.SetActive(false);
            }
        }
    }
}
