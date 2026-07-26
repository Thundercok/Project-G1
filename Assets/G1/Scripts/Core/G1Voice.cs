using System.Text;
using UnityEngine;

/// Gives a character a speaking voice.
///
/// Every line of dialogue in the game is synthesized ahead of time by
/// Tools/audio/generate_voice.py, which walks the C# that defines the script
/// and hands each line to eSpeak NG with that character's voice settings. The
/// clip is named after a hash of the line's text, so this can find the right
/// recording knowing nothing but the words it is about to say — reword a line
/// and it gets a new clip rather than quietly keeping the old audio.
///
/// When a line has no clip — one added in the editor since the last generate,
/// or typed at runtime — it falls back to blipping six synthesized vowels in
/// time with the typewriter. That was the whole system before, and it is worth
/// keeping as the floor: an unvoiced line should sound like someone talking
/// too fast to hear, not like a bug.
public sealed class G1Voice : MonoBehaviour
{
    static readonly string[] Bank = { "voice_a", "voice_e", "voice_i", "voice_o", "voice_u", "voice_m" };

    [Header("Voice profile (fallback blips only — real speech is pre-synthesized)")]
    [Range(0.55f, 1.8f)] public float pitch = 1f;
    [Range(0f, 0.4f)] public float wobble = 0.07f;
    [Tooltip("Letters to skip between blips — higher is a slower talker.")]
    public int lettersPerBlip = 4;
    [Tooltip("Floor on the gap between blips, so fast text doesn't machine-gun.")]
    public float minInterval = 0.055f;

    [Header("Levels")]
    public float speechVolume = 0.9f;
    public float blipVolume = 0.55f;

    AudioSource src;
    int sinceBlip;
    float nextAllowed;
    bool speaking;              // a real clip is playing; don't blip over it

    /// Length of the line being spoken, or 0 when falling back to blips. The
    /// dialogue typewriter reads this to reveal the text at the speed the
    /// voice actually says it.
    public float SpokenLength { get; private set; }

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;              // it comes from their mouth, not the HUD
        src.minDistance = 3f;
        src.maxDistance = 34f;
        src.rolloffMode = AudioRolloffMode.Linear;
    }

    /// Start a line. Plays the pre-synthesized recording if there is one.
    public void Begin(string line)
    {
        sinceBlip = 0;
        nextAllowed = 0f;
        speaking = false;
        SpokenLength = 0f;
        if (src == null) return;
        src.Stop();

        if (string.IsNullOrEmpty(line)) return;
        var clip = Resources.Load<AudioClip>("Audio/Voice/" + Key(line));
        if (clip == null) return;           // no recording — blip it instead

        src.pitch = 1f;                     // the character's pitch is baked in
        src.PlayOneShot(clip, speechVolume);
        speaking = true;
        SpokenLength = clip.length;
    }

    /// Call once per character the typewriter reveals. Silent while a real
    /// recording is playing.
    public void Letter(char c)
    {
        if (speaking || src == null) return;
        // silence on the gaps — this is what makes it phrase instead of buzz
        if (c == ' ' || c == '\n' || c == '\t') { sinceBlip = lettersPerBlip; return; }
        if (++sinceBlip < lettersPerBlip) return;
        sinceBlip = 0;
        if (Time.time < nextAllowed) return;
        nextAllowed = Time.time + minInterval;

        var clip = Resources.Load<AudioClip>("Audio/" + Bank[Syllable(c)]);
        if (clip == null) return;
        src.pitch = pitch * (1f + Random.Range(-wobble, wobble));
        src.PlayOneShot(clip, blipVolume);
    }

    public void Stop()
    {
        speaking = false;
        if (src != null) src.Stop();
    }

    /// Must match Tools/audio/generate_voice.py: first 16 hex of SHA-1 over the
    /// line's UTF-8 bytes.
    public static string Key(string text)
    {
        using (var sha = System.Security.Cryptography.SHA1.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    /// Vowels speak themselves; consonants borrow one deterministically, so a
    /// fallback line always sounds identical on replay.
    static int Syllable(char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 'a': return 0;
            case 'e': return 1;
            case 'i': case 'y': return 2;
            case 'o': return 3;
            case 'u': return 4;
            case 'm': case 'n': return 5;
            default: return (c * 7) % 5;     // stable per letter
        }
    }
}
