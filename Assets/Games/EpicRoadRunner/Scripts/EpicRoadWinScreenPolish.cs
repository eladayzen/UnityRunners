using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RunnerPac.EpicRoadRunner
{
    // Lays out and animates the end screens: bigger type, one centred column,
    // blocks popping in one after another.
    //
    // AUTHORED, NOT PRESERVED. Earlier versions tried to keep each element's existing
    // arrangement and merely shift it - re-anchoring to centre while restoring world
    // position, then moving groups by a shared delta. That failed twice, because the
    // arrangement being preserved was itself wrong (gem pill far left, count far
    // right) and because re-anchoring a stretched rect displaces it, which is what
    // flung the "x3" into a corner. Every position here is now stated outright.
    //
    // Positions are also re-asserted for a couple of seconds, because the stock menu
    // has its own show-animation that moves these same elements after this runs and
    // would otherwise win.
    public class EpicRoadWinScreenPolish : MonoBehaviour
    {
        [Tooltip("Seconds each block takes to pop in.")]
        public float PopDuration = 0.5f;

        [Tooltip("Delay between one block starting and the next.")]
        public float Stagger = 0.4f;

        [Tooltip("How much bigger the text gets.")]
        public float TextScale = 1.4f;

        [Tooltip("Vertical position of the title and of the score row.")]
        public float TitleY = 250f, ScoreY = 20f;

        [Tooltip("X of the gem icon (and its pill) and of the count, so they read as " +
                 "one [gem] 50 unit either side of centre.")]
        public float ScoreGemX = -95f, ScoreCountX = 55f;

        [Tooltip("Offset of the 'x3' multiplier from the count - up and to the right, " +
                 "like a superscript.")]
        public Vector2 MultiplierOffset = new Vector2(70f, 45f);

        [Tooltip("Scale a block starts at before popping to full size.")]
        public float StartScale = 0.55f;

        [Tooltip("Leave the scene's own layout alone and only animate. ON, because the " +
                 "end screens are laid out by hand in the scene now - this script cannot " +
                 "see the result and should not overrule it.")]
        public bool RespectManualLayout = true;

        [Tooltip("Seconds to keep re-asserting positions, so the stock menu animation " +
                 "cannot drag elements back.")]
        public float HoldLayout = 2f;

        public static void Apply(GameObject screen, MonoBehaviour host)
        {
            if (screen == null || host == null) return;
            var polish = screen.GetComponent<EpicRoadWinScreenPolish>();
            if (polish != null) return;                  // already handled
            polish = screen.AddComponent<EpicRoadWinScreenPolish>();
            polish.StartCoroutine(polish.Run(screen));
        }

        RectTransform _title, _gem, _border, _count, _multi;
        readonly List<RectTransform> _titleGroup = new List<RectTransform>();
        readonly List<RectTransform> _scoreGroup = new List<RectTransform>();

        IEnumerator Run(GameObject screen)
        {
            // One frame so the stock show-animation has started and its own setup is
            // out of the way before anything here is applied.
            yield return null;

            Collect(screen);

            // The layout in the scene is now hand-made, and hand-made beats anything
            // guessed from here - especially since this script cannot see the result.
            // Three attempts at positioning these elements blind produced a gem pill
            // adrift from its counter and an "x3" in the corner. Only the animation is
            // kept; where things sit is the scene's business.
            if (!RespectManualLayout)
            {
                Enlarge();
                ApplyLayout();
                StartCoroutine(HoldPositions());
            }

            SetScale(_titleGroup, 0f);
            SetScale(_scoreGroup, 0f);

            yield return Pop(_titleGroup);
            yield return new WaitForSecondsRealtime(Stagger);
            yield return Pop(_scoreGroup);
        }

        void Collect(GameObject screen)
        {
            foreach (Transform child in screen.transform)
            {
                var rt = child as RectTransform;
                if (rt == null) continue;

                string n = child.name.ToLower();

                // Order matters: "Total Gems Text" also contains "gem", so the count is
                // matched on being text, and the icon on being an Image, before any
                // name-only guess.
                if (n.Contains("button")) continue;                       // hidden anyway
                else if (n.Contains("title")) { _title = rt; _titleGroup.Add(rt); }
                else if (n.Contains("multi")) { _multi = rt; _scoreGroup.Add(rt); }
                else if (n.Contains("border")) { _border = rt; _scoreGroup.Add(rt); }
                else if (rt.GetComponent<TMP_Text>() != null || rt.GetComponent<Text>() != null)
                { _count = rt; _scoreGroup.Add(rt); }
                else { _gem = rt; _scoreGroup.Add(rt); }                  // the icon
            }
        }

        void Enlarge()
        {
            foreach (var rt in _titleGroup) Scale(rt, TextScale);
            foreach (var rt in _scoreGroup) Scale(rt, TextScale);
        }

        static void Scale(RectTransform rt, float factor)
        {
            foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true)) t.fontSize *= factor;
            foreach (var t in rt.GetComponentsInChildren<Text>(true))
                t.fontSize = Mathf.RoundToInt(t.fontSize * factor);
        }

        void ApplyLayout()
        {
            Place(_title, new Vector2(0f, TitleY));
            Place(_border, new Vector2(ScoreGemX, ScoreY));
            Place(_gem, new Vector2(ScoreGemX, ScoreY));
            Place(_count, new Vector2(ScoreCountX, ScoreY));
            Place(_multi, new Vector2(ScoreCountX + MultiplierOffset.x, ScoreY + MultiplierOffset.y));

            // The count is right-aligned inside a rect far wider than its digits, so
            // centring the rect alone still draws the number at the rect's right edge.
            if (_count != null)
            {
                var t = _count.GetComponent<TMP_Text>();
                if (t != null) t.alignment = TextAlignmentOptions.Center;
                var legacy = _count.GetComponent<Text>();
                if (legacy != null) legacy.alignment = TextAnchor.MiddleCenter;
            }
        }

        // Anchors, pivot and position are all stated - nothing is inferred from what
        // the element used to be, which is where the previous attempts went wrong.
        static void Place(RectTransform rt, Vector2 pos)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
        }

        IEnumerator HoldPositions()
        {
            for (float t = 0f; t < HoldLayout; t += Time.unscaledDeltaTime)
            {
                ApplyLayout();
                yield return null;
            }
        }

        static void SetScale(List<RectTransform> group, float s)
        {
            foreach (var rt in group) if (rt != null) rt.localScale = Vector3.one * s;
        }

        IEnumerator Pop(List<RectTransform> group)
        {
            if (group.Count == 0) yield break;
            for (float t = 0f; t < PopDuration; t += Time.unscaledDeltaTime)
            {
                SetScale(group, Overshoot(StartScale, 1f, Mathf.Clamp01(t / PopDuration)));
                yield return null;
            }
            SetScale(group, 1f);
        }

        // Back-out easing: shoots past full size then settles, which is what reads as
        // a pop rather than a fade.
        static float Overshoot(float from, float to, float k)
        {
            const float s = 1.70158f;
            float e = 1f + (s + 1f) * Mathf.Pow(k - 1f, 3f) + s * Mathf.Pow(k - 1f, 2f);
            return Mathf.LerpUnclamped(from, to, e);
        }
    }
}
