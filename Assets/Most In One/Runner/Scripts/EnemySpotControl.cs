using TMPro;
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class EnemyZoneControl : MonoBehaviour
    {
        [GUIColor("cyan")]public MOSTRangeInt NumberOfChildren;
        public TMP_Text CurrentChildCount;
        public GameObject EnemyPref;
        public GameObject CircleRange;
        [Range(.01f, 5)] public float CircleScaleFactor;
        [Range(0f, 1f)] public float DistanceFactor, Radius;

        GameObject _target;
        bool _attack;
        int _numOfChildren;

        void Start()
        {
            _numOfChildren = NumberOfChildren.GetRandomValue();
            SpawnChilds();
        }

        void SpawnChilds()
        {
            float CircleScale = 0;
            for (int i = 0; i < _numOfChildren; i++)
            {
                Instantiate(EnemyPref, transform.position, new Quaternion(0f, 180f, 0f, 1f), transform);
                float xPos = DistanceFactor * Mathf.Sqrt(i) * Mathf.Cos(i * Radius);
                float zPos = DistanceFactor * Mathf.Sqrt(i) * Mathf.Sin(i * Radius);
                transform.transform.GetChild(i).localPosition = new Vector3(xPos, .1f, zPos);
                CircleScale = Mathf.Max(CircleScale, xPos);
            }
            CircleRange.transform.localScale = Vector3.one * CircleScale / CircleScaleFactor;
            CurrentChildCount.text = transform.childCount.ToString();
        }

        void Update()
        {
            if (_attack && transform.childCount > 1) // if attacking and still has red childs
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Vector3 tpos = _target.transform.position; tpos.y = transform.GetChild(i).transform.position.y; // Ignore Y Axis
                    transform.GetChild(i).transform.position = Vector3.MoveTowards(transform.GetChild(i).transform.position, tpos, Time.deltaTime * 1.75f);
                }
                CurrentChildCount.text = transform.childCount.ToString();
            }
            else if (transform.childCount == 0) // if all red childs defeated
            {
                if (_attack)
                {
                    _target.GetComponent<CharacterControl_Count>().StopAttack(); // back to character and stop attacking
                    Destroy(transform.parent.gameObject, 1);
                    CurrentChildCount.text = "0";
                }
                _attack = false;
                transform.parent.localScale = Vector3.MoveTowards(transform.parent.localScale, Vector3.zero, Time.deltaTime * 4);
            }
        }

        public void StartAttacking(GameObject target) // when character triggered 
        {
            _attack = true;
            _target = target;
            GetComponent<Collider>().enabled = false;
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).transform.Find("Model").GetComponent<Animator>().SetBool("Run", true);
                transform.GetChild(i).LookAt(target.transform.position);
            }
        }

        public void StopAttacking() // when character run out of childs (character defeated
        {
            _attack = false;
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).transform.Find("Model").GetComponent<Animator>().SetBool("Run", false);
            }
        }
    }
}
