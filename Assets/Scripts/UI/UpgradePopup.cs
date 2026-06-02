using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rzz.Battle;
using Rzz.Core;
using Rzz.Core.Data;

namespace Rzz.UI
{
    // Phase 2.2 minimal port of upgrade_popup.gd. 3 vertically stacked cards. No icon art yet
    // (just rarity-tinted background + name + desc). Phase 3 will swap in real icons + DOTween polish.
    public class UpgradePopup : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _rarityText;
        [SerializeField] List<UpgradeCard> _cards = new List<UpgradeCard>();

        BattleController _battle;
        UpgradeManager _manager;

        public void Setup(BattleController battle, UpgradeManager manager)
        {
            _battle = battle;
            _manager = manager;
            if (_root != null) _root.SetActive(false);
            for (int i = 0; i < _cards.Count; i++)
            {
                int captured = i;
                if (_cards[i] != null && _cards[i].Button != null)
                    _cards[i].Button.onClick.AddListener(() => OnCardClicked(captured));
            }
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
            if (_titleText != null) _titleText.text = "升级!";
            if (_rarityText != null) _rarityText.text = $"品质: {RarityLabel(_manager.RolledRarity)}";
            var tint = RarityColor(_manager.RolledRarity);
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null) continue;
                if (i < _manager.Choices.Count)
                {
                    var def = _manager.Choices[i];
                    card.SetData(def, tint);
                    card.gameObject.SetActive(true);
                }
                else
                {
                    card.gameObject.SetActive(false);
                }
            }
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        void OnCardClicked(int index)
        {
            if (_manager == null || _battle == null) return;
            var def = _manager.Select(index, _battle.Player);
            if (def == null) return;
            Hide();
            _battle.ExitLevelUp();
        }

        static string RarityLabel(string r)
        {
            switch (r)
            {
                case "blue": return "稀有";
                case "purple": return "史诗";
                case "orange": return "传说";
                default: return "普通";
            }
        }

        public static Color RarityColor(string r)
        {
            switch (r)
            {
                case "blue":   return new Color(0.30f, 0.55f, 0.95f, 1f);
                case "purple": return new Color(0.66f, 0.40f, 0.92f, 1f);
                case "orange": return new Color(0.98f, 0.62f, 0.22f, 1f);
                default:       return new Color(0.85f, 0.85f, 0.85f, 1f);
            }
        }
    }

    [System.Serializable]
    public class UpgradeCard
    {
        public Button Button;
        public Image Background;
        public TMP_Text NameText;
        public TMP_Text DescText;
        public TMP_Text IconText; // emoji glyph from upgrade.icon

        public GameObject gameObject => Button != null ? Button.gameObject : null;

        public void SetData(UpgradeData def, Color rarityTint)
        {
            if (NameText != null) NameText.text = def.name_cn;
            if (DescText != null) DescText.text = def.desc_cn;
            if (IconText != null) IconText.text = def.icon;
            if (Background != null) Background.color = rarityTint;
        }
    }
}
