using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class EndGameUIHandler : MonoBehaviour
    {
        public MOST_Database DataHolder;
        public string ScoreMultiplyerDataName;
        public string CurrentScoreDataName;
        [Line]
        public Text ScoreText;
        public Text MultiplyerText;
        public GameObject ContinueButton;
        public float DelayBeforeStart = 3;

        int _currentScore, _totalScore;

        public void RecordStartScore()
        {
            _currentScore = DataHolder.Get<IntData>(CurrentScoreDataName).Value;
            if(!string.IsNullOrEmpty(ScoreMultiplyerDataName)) DataHolder.Get<FloatData>(ScoreMultiplyerDataName).Value = .8f;
        }

        public void StartCount()
        {
            StartCoroutine(ScoreCount());
        }

        IEnumerator ScoreCount()
        {
            yield return new WaitForSeconds(Time.deltaTime);
            if (MultiplyerText && !string.IsNullOrEmpty(ScoreMultiplyerDataName))
            {
                _totalScore = DataHolder.Get<IntData>(CurrentScoreDataName).Value - _currentScore;
                ScoreText.text = _totalScore.ToString();
                MultiplyerText.text = DataHolder.Get<FloatData>(ScoreMultiplyerDataName).Value.ToString("0.0");
                _currentScore = (int)(DataHolder.Get<FloatData>(ScoreMultiplyerDataName).Value * _totalScore) + 1;
                DataHolder.Get<IntData>(CurrentScoreDataName).Add(_currentScore);
            }
            else
            {
                _currentScore = DataHolder.Get<IntData>(CurrentScoreDataName).Value - _currentScore;
                _totalScore = 0;
                ScoreText.text = _totalScore.ToString();

            }
            ContinueButton.SetActive(false);
            yield return new WaitForSeconds(DelayBeforeStart);
            while(_totalScore < _currentScore)
            {
                yield return new WaitForSeconds(.01f);
                _totalScore += 1;
                ScoreText.text = _totalScore.ToString();
            }
            ContinueButton.SetActive(true);
        }
    }
}