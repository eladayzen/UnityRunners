using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    public class HealthBar : MonoBehaviour
    {
        public bool AutomaticallyLookAtCamera;
        public float MaxHealth;
        public float Health;
        public float ZeroPoint;

        public Transform MovingBar;
        public Transform DynamicBar;
        public float DynamicBarSpeed = 5;
        public TMP_Text HealthText;

        public List<Animation> OnUpdateAnimations;
        public GameObject SpawndDamageText;
        public GameObject SpawndHealingText;
        public Vector3 SpawndTextOffsetA;
        public Vector3 SpawndTextOffsetB;

        public Animation AnimationOnLowHealth;
        [Range(0, 100)] public float LowHealthRange;

        [Header("Animation Settings")]
        public bool EnableHide;
        public float displayTime = 1f;        // Base time to stay fully visible
        public float fadeInSpeed = 2f;       // Speed of fade-in
        public float fadeOutSpeed = 2f;      // Speed of fade-out
        public bool resetAlphaOnStart = true; // Start from alpha=0 if true

        List<Image> _images = new();
        List<float> _originalAlphas = new();
        bool _isAnimating = false;
        Coroutine _currentAnimation;
        float _remainingDisplayTime;

        Vector3 _startBarPos;

        void Start()
        {
            if(MovingBar) _startBarPos = MovingBar.transform.localPosition;

            _images = new List<Image>(GetComponentsInChildren<Image>(true));
            foreach (Image img in _images)
            {
                _originalAlphas.Add(img.color.a);
                if (EnableHide) img.color = new Color(img.color.r, img.color.g, img.color.b,0);
            }

            OnValidate();
        }

        void Update()
        {
            if (DynamicBar && MovingBar) DynamicBar.transform.localPosition =
                    Vector3.MoveTowards(DynamicBar.transform.localPosition, MovingBar.transform.localPosition, Time.deltaTime * DynamicBarSpeed * ZeroPoint / 30);
        }

        void OnValidate()
        {
            if (AutomaticallyLookAtCamera && Camera.main) transform.rotation = Camera.main.transform.rotation;
        }

        public void UpdateHealth(float newValue)
        {
            newValue = Mathf.Max(0, newValue);
            GameObject spText = null;
            Vector3 spPoint = new(Random.Range(SpawndTextOffsetA.x, SpawndTextOffsetB.x),
                        Random.Range(SpawndTextOffsetA.y, SpawndTextOffsetB.y), Random.Range(SpawndTextOffsetA.z, SpawndTextOffsetB.z));

            if (Health > newValue) // Damage
            {
                if (newValue / MaxHealth < LowHealthRange / 100) AnimationOnLowHealth.Play();
                if(SpawndDamageText) spText = Instantiate(SpawndDamageText, transform.position + new Vector3(0, spPoint.y, 0), Camera.main.transform.rotation);
            }
            else if (Health < newValue) // Healing
            {
                if (newValue / MaxHealth > LowHealthRange / 100) AnimationOnLowHealth.Stop();
                if (SpawndHealingText) spText = Instantiate(SpawndHealingText, transform.position + new Vector3(0, spPoint.y, 0), Camera.main.transform.rotation);
            }

            if (spText != null)
            {
                spText.transform.position += spText.transform.TransformDirection(Vector3.right) * spPoint.x
                    + spText.transform.TransformDirection(Vector3.forward) * spPoint.z;

                TMP_Text text = spText.transform.GetComponentInChildren<TMP_Text>();
                text.text = Mathf.Abs(Health - newValue).ToString();
                Destroy(spText, 5);
            }
            if(EnableHide) PlayFadeAnimation();
            Health = newValue;
            if (MovingBar) MovingBar.localPosition = new Vector3(-ZeroPoint * (MaxHealth - Health) / MaxHealth, MovingBar.localPosition.y, MovingBar.localPosition.z);
            if(HealthText) HealthText.text = Health.ToString();
            foreach (Animation anim in OnUpdateAnimations) anim.Play();
        }

        public void ResetMaxHealth(float newValue)
        {
            Health = MaxHealth = newValue;
            if (HealthText) HealthText.text = Health.ToString();
            if (DynamicBar) DynamicBar.transform.localPosition = _startBarPos;
            if (MovingBar) MovingBar.transform.localPosition = _startBarPos;
        }

        public void PlayFadeAnimation()
        {
            // If already visible, extend display time instead of restarting
            if (_isAnimating && GetCurrentAlpha() >= 0.99f)
            {
                _remainingDisplayTime = displayTime; // Reset display timer
                return;
            }

            // If already animating but not fully visible, stop and restart
            if (_isAnimating)
            {
                StopCoroutine(_currentAnimation);
            }

            _currentAnimation = StartCoroutine(FadeAnimation());
        }

        IEnumerator FadeAnimation()
        {
            _isAnimating = true;
            _remainingDisplayTime = displayTime;

            // Reset alpha to 0 if needed
            if (resetAlphaOnStart)
            {
                SetImagesAlpha(0f);
            }

            // Fade In (only if not already visible)
            float currentAlpha = GetCurrentAlpha();
            while (currentAlpha < 1f)
            {
                currentAlpha += fadeInSpeed * Time.deltaTime;
                currentAlpha = Mathf.Clamp01(currentAlpha);
                SetImagesAlpha(currentAlpha);
                yield return null;
            }

            // Hold visibility (can be extended by repeated calls)
            while (_remainingDisplayTime > 0f)
            {
                _remainingDisplayTime -= Time.deltaTime;
                yield return null;
            }

            // Fade Out
            while (currentAlpha > 0f)
            {
                currentAlpha -= fadeOutSpeed * Time.deltaTime;
                currentAlpha = Mathf.Clamp01(currentAlpha);
                SetImagesAlpha(currentAlpha);
                yield return null;
            }

            _isAnimating = false;
        }

        // Get the current alpha (average of all images)
        float GetCurrentAlpha()
        {
            if (_images.Count == 0) return 0f;
            float totalAlpha = 0f;
            foreach (Image img in _images)
            {
                if (img != null) totalAlpha += img.color.a;
            }
            return totalAlpha / _images.Count;
        }

        // Set alpha for all images
        void SetImagesAlpha(float alpha)
        {
            for (int i = 0; i < _images.Count; i++)
            {
                if (_images[i] != null)
                {
                    Color newColor = _images[i].color;
                    newColor.a = alpha;
                    _images[i].color = newColor;
                }
            }
        }

        // Force-stop the animation early
        public void StopAnimation(bool resetAlpha = false)
        {
            if (_isAnimating)
            {
                StopCoroutine(_currentAnimation);
                _isAnimating = false;
            }
            if (resetAlpha) ResetToOriginalAlpha();
        }

        // Restore original alpha values
        public void ResetToOriginalAlpha()
        {
            for (int i = 0; i < _images.Count; i++)
            {
                if (_images[i] != null)
                {
                    Color newColor = _images[i].color;
                    newColor.a = _originalAlphas[i];
                    _images[i].color = newColor;
                }
            }
        }
    }
}