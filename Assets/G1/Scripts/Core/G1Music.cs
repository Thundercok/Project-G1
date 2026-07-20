using UnityEngine;

/// Dynamic music driven by ThreatDirector intensity:
/// silence → tension (>= 0.4) → action (>= 0.75), 1.5 s crossfades.
public sealed class G1Music : MonoBehaviour
{
    public float volume = 0.4f;
    public float fadeTime = 1.5f;

    AudioSource a, b;
    string current = "";

    void Awake()
    {
        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        foreach (var src in new[] { a, b })
        {
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
        }
    }

    void Update()
    {
        float intensity = ThreatDirector.Instance ? ThreatDirector.Instance.Intensity : 0f;
        string want = intensity >= 0.75f ? "music_action"
                    : intensity >= 0.4f ? "music_tension" : "";

        if (want != current)
        {
            current = want;
            (a, b) = (b, a);
            if (want == "")
            {
                a.clip = null;
            }
            else
            {
                a.clip = Resources.Load<AudioClip>("Audio/Music/" + want);
                a.volume = 0f;
                if (a.clip)
                    a.Play();
            }
        }

        float step = volume / fadeTime * Time.deltaTime;
        a.volume = Mathf.MoveTowards(a.volume, a.clip ? volume : 0f, step);
        b.volume = Mathf.MoveTowards(b.volume, 0f, step);
        if (b.volume <= 0f && b.isPlaying)
            b.Stop();
    }
}
