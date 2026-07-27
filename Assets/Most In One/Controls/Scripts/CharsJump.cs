using UnityEngine;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    public class CharsJump : MonoBehaviour
    {
        public GameObject[] Characters;
        public GameObject[] JoySticks;
        public Transform CameraT;
        public Text Current;
        Vector3 _startPos;
        int _counter = 0;

        private void Start()
        {
            _startPos = CameraT.position;
            MoveNext(0);
        }
        public void MoveNext(int jump)
        {
            _counter += jump;
            if (_counter < 0) _counter = Characters.Length - 1;
            if (_counter >= Characters.Length) _counter = 0;

            foreach (GameObject obj in Characters) obj.SetActive(false);
            foreach (GameObject obj in JoySticks) obj.SetActive(false);

            Characters[_counter].SetActive(true);
            JoySticks[_counter].SetActive(true);

            Characters[_counter].transform.position = CameraT.position - _startPos;
            Current.text = (_counter + 1).ToString();
        }
    }
}