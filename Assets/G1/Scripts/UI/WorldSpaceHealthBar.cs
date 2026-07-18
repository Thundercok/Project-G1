using UnityEngine;
using UnityEngine.UI;

/// Temporary debug health bar floating over anything with a HealthSystem.
/// Built entirely from code (world-space Canvas + non-interactable Slider),
/// billboards toward the player camera.
///
/// Disable globally with WorldSpaceHealthBar.GloballyEnabled = false (or the
/// per-instance flag): when disabled, no UI objects are created at all.
[RequireComponent(typeof(HealthSystem))]
public class WorldSpaceHealthBar : MonoBehaviour
{
    /// Flip to false for the final, immersive look — bars never get built.
    public static bool GloballyEnabled = true;

    public bool barEnabled = true;
    public float heightOffset = 2.1f;

    HealthSystem health;
    Canvas canvas;
    Slider slider;
    Camera cam;

    void Start()
    {
        if (!GloballyEnabled || !barEnabled)
        {
            enabled = false;
            return;
        }
        health = GetComponent<HealthSystem>();
        cam = Camera.main;
        BuildUI();
        health.OnHealthChanged += (current, max) => slider.value = current / max;
        health.OnDeath += (p, n) =>
        {
            if (canvas)
                Destroy(canvas.gameObject);
        };
    }

    void LateUpdate()
    {
        if (!canvas)
            return;
        if (!cam)
        {
            cam = Camera.main;
            if (!cam)
                return;
        }
        canvas.transform.rotation = Quaternion.LookRotation(
            canvas.transform.position - cam.transform.position);
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("HealthBarCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        canvasGo.transform.localScale = Vector3.one * 0.007f;
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 12f);

        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(canvasGo.transform, false);
        var srt = sliderGo.AddComponent<RectTransform>();
        Stretch(srt);

        Image bg = MakeImage(sliderGo.transform, "Background",
                             new Color(0.75f, 0.08f, 0.08f, 0.95f));
        Image fill = MakeImage(sliderGo.transform, "Fill",
                               new Color(0.15f, 0.85f, 0.20f, 0.95f));

        slider = sliderGo.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.interactable = false;
        slider.fillRect = fill.rectTransform;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
    }

    static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
