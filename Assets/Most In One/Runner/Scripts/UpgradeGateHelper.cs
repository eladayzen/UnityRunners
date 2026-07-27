using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    public class UpgradeGateHelper : MonoBehaviour
    {
        public string CharacterName;
        public MOST_Gate Gate;
        public MOST_Gate.GateType TypeAfterMaxLevel;
        public GameObject OnCollectSpawnAfterMaxLevel;
        public GameObject UpgradeAfterMaxLevel;
        public GameObject[] Upgrades;

        GameObject _character;
        bool _autoDetect = true;
        int _currentLevel;

        void Start()
        {
            _character = GameObject.Find(CharacterName);
        }

        void Update()
        {
            if (_autoDetect && _currentLevel != _character.GetComponent<CharacterControl_ShootRunner>().CurrentLevel)
            {
                _currentLevel = _character.GetComponent<CharacterControl_ShootRunner>().CurrentLevel;
                foreach (GameObject up in Upgrades) up.SetActive(false);
                if (_currentLevel > Upgrades.Length)
                {
                    Gate.Type = MOST_Gate.GateType.Currency;
                    Gate.OnCollectSpawn = OnCollectSpawnAfterMaxLevel;
                    UpgradeAfterMaxLevel.SetActive(true);
                }
                else
                {
                    Upgrades[_currentLevel - 1].SetActive(true);
                }
            }
        }

        public void AutoDetection(bool enabled)
        {
            _autoDetect = enabled;
        }
    }
}