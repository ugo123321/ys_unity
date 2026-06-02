using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Rzz.UI
{
    // Phase 2.1 minimal port of main_menu.gd. Original is 890 lines with gacha/equipment/dungeon/achievement tabs;
    // those land in Phase 3.2. For now just a title + Start button -> load Battle scene.
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] Button _startButton;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] string _battleSceneName = "Battle";

        void Awake()
        {
            if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
        }

        void OnStartClicked()
        {
            GameFlowController.LoadBattle(_battleSceneName);
        }
    }
}
