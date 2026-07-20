using UnityEngine;

/// Zone-aware ambience: Level 1 is linear along +Z, so zones are defined by
/// their far Z boundary. Crossfades between two looping sources over 2 s.
public sealed class G1Ambience : MonoBehaviour
{
    [System.Serializable]
    public struct Zone
    {
        public string clip;
        public float zMax;
    }

    public Zone[] zones;          // ordered by zMax ascending; set by builder
    public float volume = 0.32f;
    public float fadeTime = 2f;

    AudioSource a, b;             // a = active, b = fading out
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
        if (zones == null || zones.Length == 0)
            return;
        string want = zones[zones.Length - 1].clip;
        for (int i = 0; i < zones.Length; i++)
        {
            if (transform.position.z < zones[i].zMax)
            {
                want = zones[i].clip;
                break;
            }
        }

        if (want != current)
        {
            current = want;
            (a, b) = (b, a);                       // reuse the faded-out source
            a.clip = Resources.Load<AudioClip>("Audio/" + want);
            a.volume = 0f;
            if (a.clip)
                a.Play();
        }

        float step = volume / fadeTime * Time.deltaTime;
        a.volume = Mathf.MoveTowards(a.volume, volume, step);
        b.volume = Mathf.MoveTowards(b.volume, 0f, step);
        if (b.volume <= 0f && b.isPlaying)
            b.Stop();
    }
}
