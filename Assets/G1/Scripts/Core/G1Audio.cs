using System.Collections.Generic;
using UnityEngine;

/// Pooled one-shot SFX player. Clips are the procedurally generated WAVs in
/// Assets/Resources/Audio (see Tools/audio/generate_sfx.py). Zero setup: the
/// pool bootstraps on first use and survives scene reloads.
public static class G1Audio
{
    const int PoolSize = 14;

    static AudioSource[] pool;
    static int next;
    static readonly Dictionary<string, AudioClip> cache =
        new Dictionary<string, AudioClip>(16);

    static void EnsurePool()
    {
        if (pool != null && pool[0])
            return;
        var root = new GameObject("G1Audio");
        Object.DontDestroyOnLoad(root);
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var src = new GameObject("src" + i).AddComponent<AudioSource>();
            src.transform.SetParent(root.transform, false);
            src.playOnAwake = false;
            src.minDistance = 2f;
            src.maxDistance = 45f;
            src.rolloffMode = AudioRolloffMode.Linear;
            pool[i] = src;
        }
    }

    static AudioClip Clip(string name)
    {
        if (!cache.TryGetValue(name, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + name);
            if (!clip)
                Debug.LogWarning("G1Audio: missing clip " + name);
            cache[name] = clip;
        }
        return clip;
    }

    /// 3D positional one-shot.
    public static void Play(string name, Vector3 pos, float volume = 1f,
                            float pitch = 1f, float pitchJitter = 0.06f)
    {
        var clip = Clip(name);
        if (!clip)
            return;
        EnsurePool();
        var src = pool[next = (next + 1) % PoolSize];
        src.transform.position = pos;
        src.spatialBlend = 1f;
        src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        src.PlayOneShot(clip, volume);
    }

    /// 2D non-positional one-shot (player-owned sounds: own gunfire, hurt).
    public static void Play2D(string name, float volume = 1f,
                              float pitch = 1f, float pitchJitter = 0.04f)
    {
        var clip = Clip(name);
        if (!clip)
            return;
        EnsurePool();
        var src = pool[next = (next + 1) % PoolSize];
        src.spatialBlend = 0f;
        src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        src.PlayOneShot(clip, volume);
    }
}
