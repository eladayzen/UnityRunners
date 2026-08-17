using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RunnerPac.EpicRoadRunner
{
    // Restyles and animates the stock win screen: bigger type, everything stacked
    // down the centre, and each block popping in after the one above it.
    //
    // Done at runtime rather than by editing the scene's UI, so it needs no wiring
    // and cannot be undone by a rebuild. It runs once, when the win screen appears.
    //
    // The three blocks are moved as groups, not as individual elements: the score
    // readout is really four overlapping objects (border, gem icon, multiplier,
    // text) whose relative arrangement is deliberate. Shifting them by a shared
    // delta keeps that intact while still stacking the screen as a whole.
    public class EpicRoadWinScreenPolish : MonoBehaviour
    {
        [Tooltip("Seconds each block takes to pop in.")]
        public float PopDuration = 0.5f;

        [Tooltip("Delay between one block starting and the next.")]
        public float Stagger = 0.4f;

        [Tooltip("How much bigger the text gets.")]
        public float TextScale = 1.4f;

        [Tooltip("Vertical positions for title / score / button, in canvas units.")]
        public float TitleY = 320f, ScoreY = 40f, ButtonY = -340f;

        [Tooltip("Scale a block starts at before popping to full size.")]
        public float StartScale = 0.55f;

        public static void Apply(GameObject winScreen, MonoBehaviour host)
        {
            if (winScreen == null || host == null) return;
            var polish = winScreen.GetComponent<EpicRoadWinScreenPolish>();
            if (polish == null) polish = winScreen.AddComponent<EpicRoadWinScreenPolish>();
            polish.StartCoroutine(polish.Run(winScreen));
        }

        bool _done;

        IEnumerator Run(GameObject winScreen)
        {
            if (_done) yield break;
            _done = true;

            // One frame, so the stock show-animation has set up its own transforms
            // before anything here overwrites them.
            yield return null;

            var title = new List<RectTransform>();
            var score = new List<RectTransform>();
            var button = new List<RectTransform>();

            foreach (Transform child in winScreen.transform)
            {
                var rt = child as RectTransform;
                if (rt == null) continue;

                string n = child.name.ToLower();
                if (n.Contains("title")) title.Add(rt);
                else if (n.Contains("button")) button.Add(rt);
                else score.Add(rt);          // border, gems text, multiplier, gem icon
            }

            Enlarge(title, TextScale);
            Enlarge(score, TextScale);
            Enlarge(button, TextScale * 0.85f);   // the button needs less

            Stack(title, TitleY);
            Stack(score, ScoreY);
            Stack(button, ButtonY);

            // Hide everything, then bring the blocks in one after another.
            SetScale(title, 0f);
            SetScale(score, 0f);
            SetScale(button, 0f);

            yield return Pop(title);
            yield return new WaitForSecondsRealtime(Stagger);
            yield return Pop(score);
            yield return new WaitForSecondsRealtime(Stagger);
            yield return Pop(button);
        }

        void Enlarge(List<RectTransform> group, float factor)
        {
            foreach (var rt in group)
            {
                foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true)) t.fontSize *= factor;
                foreach (var t in rt.GetComponentsInChildren<Text>(true)) t.fontSize = Mathf.RoundToInt(t.fontSize * factor);
            }
        }

        // Centre the group horizontally and move it, as a unit, to targetY.
        //
        // Both axes move by a SHARED delta. Setting each element's x to zero
        // individually is what threw the gem counter off to one side: these elements
        // do not share anchors, so an element anchored to the right edge stays pinned
        // there no matter what its anchoredPosition says. Anchors are normalised to
        // the centre first (preserving on-screen position and size), which makes
        // anchoredPosition mean the same thing for every element, and only then is the
        // group shifted - so the gem icon, its counter and the border keep their
        // relative arrangement.
        void Stack(List<RectTransform> group, float targetY)
        {
            if (group.Count == 0) return;

            foreach (var rt in group) CentreAnchors(rt);

            Vector2 sum = Vector2.zero;
            foreach (var rt in group) sum += rt.anchoredPosition;
            Vector2 centroid = sum / group.Count;

            Vector2 delta = new Vector2(-centroid.x, targetY - centroid.y);
            foreach (var rt in group) rt.anchoredPosition += delta;
        }

        // Re-anchor to the parent's centre without moving or resizing the element.
        static void CentreAnchors(RectTransform rt)
        {
            Vector2 size = rt.rect.size;
            Vector3 world = rt.position;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.position = world;
        }

        static void SetScale(List<RectTransform> group, float s)
        {
            foreach (var rt in group) rt.localScale = Vector3.one * s;
        }

        IEnumerator Pop(List<RectTransform> group)
        {
            if (group.Count == 0) yield break;

            for (float t = 0f; t < PopDuration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / PopDuration);
                SetScale(group, Overshoot(StartScale, 1f, k));
                yield return null;
            }
            SetScale(group, 1f);
        }

        // Back-out easing: shoots past full size, then settles. This is what makes it
        // read as a pop rather than a fade.
        static float Overshoot(float from, float to, float k)
        {
            const float s = 1.70158f;
            float e = 1f + (s + 1f) * Mathf.Pow(k - 1f, 3f) + s * Mathf.Pow(k - 1f, 2f);
            return Mathf.LerpUnclamped(from, to, e);
        }
    }
}
