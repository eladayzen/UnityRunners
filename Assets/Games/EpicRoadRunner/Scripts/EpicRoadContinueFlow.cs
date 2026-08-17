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
                                 string caption = null, bool pressButtonAtEnd = false,
                                 Sprite spinner = null)
        {
            if (endScreen == null || host == null) return;
            var flow = endScreen.GetComponent<EpicRoadContinueFlow>();
            if (flow != null) return;                    // already running for this screen
            flow = endScreen.AddComponent<EpicRoadContinueFlow>();

            // Driven by the host, not by the end screen. ShowOnly() can deactivate the
            // screen this component sits on, which would silently kill a coroutine
            // started here - and with it the only thing left to continue the game.
            host.StartCoroutine(flow.Run(endScreen, seconds, caption, pressButtonAtEnd, spinner));
        }

        IEnumerator Run(GameObject endScreen, float seconds, string caption, bool pressButtonAtEnd, Sprite spinner)
        {
            // "GAME OVER" says the run stopped; "GAME RESTARTS IN" says what the
            // number underneath is counting towards.
            if (!string.IsNullOrEmpty(caption)) SetTitle(endScreen, caption);

            var button = FindButton(endScreen);
            HideButtons(endScreen);

            // "GET READY" and a turning ring, so the screen says something is happening
            // during the load rather than sitting still. The ring keeps turning until the
            // scene swaps and takes this whole screen with it.
            MakeLabel(endScreen, "GetReady", "GET READY", 64f, -120f);
            var ring = MakeSpinner(endScreen, spinner, -260f);

            var manager = FindFirstObjectByType<Solo.MOST_IN_ONE.UniversalGameManager>();

            // Losing has no auto-advance of its own, so start the manager's sequence
            // here. Winning has already started it. Either way the manager owns the
            // timing: screen settles, then it loads, then it counts down.
            if (pressButtonAtEnd)
            {
                if (manager != null) manager.RestartCurrent();
                else if (button != null) { button.onClick.Invoke(); yield break; }
            }

            // No label until there is something true to put in it. The countdown only
            // exists once the next level has finished loading, so it never shows a
            // number that the load might overrun.
            TMP_Text label = null;
            while (manager != null)
            {
                float left = manager.CountdownRemaining;

                if (left >= 0f)
                {
                    if (label == null) label = MakeCountdownLabel(endScreen);
                    label.text = Mathf.CeilToInt(left).ToString();
                }
                else if (label != null)
                {
                    // Reached zero - remove it rather than leaving it frozen on "1"
                    // while the scene swaps.
                    Destroy(label.gameObject);
                    yield break;
                }

                if (ring != null) ring.Rotate(0f, 0f, -SpinDegreesPerSecond * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        const float SpinDegreesPerSecond = 220f;

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

        // Plain text on the screen, using a font already present so it actually draws.
        static TMP_Text MakeLabel(GameObject endScreen, string name, string content,
                                  float size, float y)
        {
            var text = NewText(endScreen, name, size, y);
            if (text != null) text.text = content;
            return text;
        }

        // A turning ring. The sprite is whatever the scene component was given; if that
        // is empty, any circular sprite already in the UI is reused, and failing that the
        // spinner is simply skipped rather than drawing an untextured white box.
        static Transform MakeSpinner(GameObject endScreen, Sprite sprite, float y)
        {
            if (sprite == null) sprite = FindCircleSprite();
            if (sprite == null) return null;

            var go = new GameObject("Spinner", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(endScreen.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(110f, 110f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return rt;
        }

        static Sprite FindCircleSprite()
        {
            foreach (var img in FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (img.sprite == null) continue;
                string n = img.sprite.name.ToLower();
                if (n.Contains("circle") || n.Contains("ring") || n.Contains("charge"))
                    return img.sprite;
            }
            return null;
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

            return NewText(endScreen, "Countdown", 120f, -420f);
        }

        // Shared text factory. The font is copied from text already on the screen: a
        // TextMeshProUGUI on a bare GameObject has no font asset unless TMP_Settings
        // supplies a default, and when it does not the component exists, reports no
        // error, and draws absolutely nothing.
        static TMP_Text NewText(GameObject endScreen, string name, float size, float y)
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

            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(endScreen.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(700f, 200f);
            rt.localScale = Vector3.one;

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
                if (fontMaterial != null) text.fontSharedMaterial = fontMaterial;
            }
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
