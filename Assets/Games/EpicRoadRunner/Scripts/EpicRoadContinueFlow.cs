using System.Collections;
using UnityEngine;

using TMPro;

namespace RunnerPac.EpicRoadRunner
{
    // Replaces "tap to continue" with a visible countdown on both end screens.
    //
    // IT DOES NOT LOAD ANYTHING. An earlier version did: it set AutoAdvanceOnWin to 0
    // and drove the next level itself with LoadSceneAsync, to hide the freeze caused
    // by the manager's synchronous SceneManager.LoadScene. That was a bad trade. The
    // manager's path - GameOverState -> AutoAdvanceOnWin -> RestartCurrent() ->
    // SetAndLoad() - has driven every level transition since progression was wired,
    // and taking it out made this component the single point of failure for the game
    // ever continuing. Worse, Begin() attaches to the end screen GameObject, and
    // ShowOnly() can deactivate that object mid-countdown, killing the coroutine and
    // stranding the player on a dead screen.
    //
    // So the manager advances the win, exactly as it always did, and this only draws
    // the countdown over the top of it - timed to NextLevelDelay. The lose screen has
    // no auto-advance of its own, so there the countdown finishes by pressing that
    // screen's own Continue button, which is the same path a tap takes.
    //
    // The coroutine is started on the host, not on this component, so deactivating
    // the end screen cannot kill it.
    //
    // The synchronous-load freeze is therefore still present and still unfixed.
    public class EpicRoadContinueFlow : MonoBehaviour
    {
        // pressButtonAtEnd: the lose screen has no auto-advance of its own, so the
        // countdown finishes by pressing its Continue button. The WIN screen must not
        // do this - UniversalGameManager.GameOverState already advances it via
        // AutoAdvanceOnWin -> RestartCurrent(), and that path has driven every level
        // transition since progression was first wired.
        public static void Begin(GameObject endScreen, MonoBehaviour host, float seconds,
                                 string caption = null, bool pressButtonAtEnd = false)
        {
            if (endScreen == null || host == null) return;
            var flow = endScreen.GetComponent<EpicRoadContinueFlow>();
            if (flow != null) return;                    // already running for this screen
            flow = endScreen.AddComponent<EpicRoadContinueFlow>();

            // Driven by the host, not by the end screen. ShowOnly() can deactivate the
            // screen this component sits on, which would silently kill a coroutine
            // started here - and with it the only thing left to continue the game.
            host.StartCoroutine(flow.Run(endScreen, seconds, caption, pressButtonAtEnd));
        }

        IEnumerator Run(GameObject endScreen, float seconds, string caption, bool pressButtonAtEnd)
        {
            // "GAME OVER" says the run stopped; "GAME RESTARTS IN" says what the
            // number underneath is counting towards.
            if (!string.IsNullOrEmpty(caption)) SetTitle(endScreen, caption);

            var button = FindButton(endScreen);
            HideButtons(endScreen);
            var label = MakeCountdownLabel(endScreen);

            for (float left = seconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                if (label != null) label.text = Mathf.CeilToInt(left).ToString();
                yield return null;
            }
            if (label != null) label.text = "";

            // Losing restarts through the screen's own button, so the restart path is
            // identical to a tap. Winning is already handled by the manager.
            if (pressButtonAtEnd && button != null) button.onClick.Invoke();
        }

        static UnityEngine.UI.Button FindButton(GameObject endScreen)
        {
            foreach (var b in endScreen.GetComponentsInChildren<UnityEngine.UI.Button>(true)) return b;
            return null;
        }

        // Rewrites the screen's headline. Prefers a child actually called "Title";
        // otherwise takes the largest piece of text on the screen, which is the
        // headline by construction.
        static void SetTitle(GameObject endScreen, string caption)
        {
            TMP_Text best = null;
            foreach (var t in endScreen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name.ToLower().Contains("title")) { best = t; break; }
                if (best == null || t.fontSize > best.fontSize) best = t;
            }
            if (best != null) best.text = caption;

            foreach (var t in endScreen.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (t.name.ToLower().Contains("title")) t.text = caption;
        }

        // The tap-to-continue button is gone - the countdown replaces it entirely.
        static void HideButtons(GameObject endScreen)
        {
            foreach (var b in endScreen.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                b.gameObject.SetActive(false);
        }

        // A big number where the button used to be.
        //
        // The font is copied from text already on the screen. A TextMeshProUGUI added
        // to a bare GameObject has no font asset unless TMP_Settings supplies a default
        // - and when it does not, the component exists, reports no error, and draws
        // absolutely nothing. That is why the first countdown was invisible while the
        // button had already been hidden, leaving the screen with no visible driver at
        // all.
        static TMP_Text MakeCountdownLabel(GameObject endScreen)
        {
            TMP_FontAsset font = null;
            Material fontMaterial = null;
            foreach (var existing in endScreen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (existing.font == null) continue;
                font = existing.font;
                fontMaterial = existing.fontSharedMaterial;
                break;
            }

            var go = new GameObject("Countdown", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(endScreen.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -240f);
            rt.sizeDelta = new Vector2(600f, 220f);
            rt.localScale = Vector3.one;

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
                if (fontMaterial != null) text.fontSharedMaterial = fontMaterial;
            }
            text.fontSize = 120f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
