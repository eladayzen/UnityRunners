using UnityEngine;
using Solo.MOST_IN_ONE;

namespace RunnerPac.EpicRoadRunner
{
    // Wires CharacterControl_ShootRunner's OnWinEvent/OnLoseEvent to the scene's
    // already-built (but previously unconnected) Win UI / Lose UI panels.
    public class EpicRoadEndScreen : MonoBehaviour
    {
        [SerializeField] CharacterControl_ShootRunner character;
        [SerializeField] GameObject winUI;
        [SerializeField] GameObject loseUI;

        void Awake()
        {
            character.OnWinEvent.AddListener(ShowWin);
            character.OnLoseEvent.AddListener(ShowLose);
        }

        void ShowWin()
        {
            winUI.SetActive(true);
            loseUI.SetActive(false);
        }

        void ShowLose()
        {
            loseUI.SetActive(true);
            winUI.SetActive(false);
        }
    }
}
