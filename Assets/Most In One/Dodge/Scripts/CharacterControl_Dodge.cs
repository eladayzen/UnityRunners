using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_Dodge : MonoBehaviour
    {
        public MOST_Database ScoreHolder;
        public string ScoreDataName;

        [Tooltip("the model that will be used in rotation contain the animator")]
        public GameObject CharacterModel; // rotated model

        [BigHeader("Score")]
        [Tooltip("Score level animations paired with dodge levels")]
        public GameObject[] ScoreLevelPopup;

        [Tooltip("the score for each dodge level paired with Bull dodge levels")]
        public float[] ScoreForLevel;

        [BigHeader("Events")]
        public UnityEvent OnDefeat;
        [HideInInspector] public bool Defeat;

        class DodgedEnemies {public GameObject enemy; public bool dodged; public int scoreLevel; }
        List<DodgedEnemies> _enemiesData = new();

        private void Start()
        {
            ScoreHolder.Get<IntData>(ScoreDataName).Set(0);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Defeat) return;
            if (other.CompareTag("Score Object"))
            {
                if (!_enemiesData.Any(item => item.enemy == other.transform.parent.gameObject)) // stepped into score range (first time)
                {
                    // add this enemy to a list to store and record how close the character will become
                    _enemiesData.Add(new() { enemy = other.transform.parent.gameObject, dodged = false, scoreLevel = 1 });
                }
                else // if the score level upgraded
                {
                    DodgedEnemies enemy = _enemiesData.Find(item => item.enemy == other.transform.parent.gameObject);
                    if (enemy.dodged) return;
                    if (other.gameObject.layer == 8 && enemy.scoreLevel < 2) enemy.scoreLevel = 2; // level score == 1 upgrade to level 2
                    else if (other.gameObject.layer == 9 && enemy.scoreLevel < 3) enemy.scoreLevel = 3; // level score == 2 upgrade to level 3
                }
            }
            else if (other.CompareTag("Enemy"))
            {
                Defeat = true;
                // Animations and Render
                CharacterModel.transform.LookAt(other.transform.position - Vector3.up * other.transform.position.y); // make character look at the enemy and ignore hight
                CharacterModel.GetComponent<Animator>().SetBool("Defeated", true); // make character look at the enemy // Hit point

                GetComponent<Rigidbody>().AddForce(   // push back the character 
                    CharacterModel.transform.TransformDirection(Vector3.back) * 18 + Vector3.up, ForceMode.Impulse);
                                                                                                         //......................//
                GetComponent<MOST_FreeMovement>().EnableState(false); // Deactivate movement system
                OnDefeat?.Invoke();
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if (Defeat) return;
            if (other.gameObject.layer == 7) // out of Level 1 means out of enemy range
            {
                DodgedEnemies enemy = _enemiesData.Find(item => item.enemy == other.transform.parent.gameObject);
                if (enemy.dodged == true || enemy.scoreLevel == 0) return; // if this enemy was dodged before 

                enemy.dodged = true; // enemy was dodged
                Destroy(Instantiate(ScoreLevelPopup[enemy.scoreLevel - 1], transform.position + new Vector3(1, 2, 2), Quaternion.identity), 3);
                ScoreHolder.Get<IntData>(ScoreDataName).Increment(); // send message to game settings and update the score
                _enemiesData.RemoveAll(item => item.enemy == null); // clear all destroyed in hirearchy enemies
            }
        }
    }
}