using UnityEngine;

/// Makes being inside a building sound and look like being inside a building.
///
/// The sprawl's interiors were, acoustically, outdoors: the same dry mix, the
/// same wind, the same 700m fog washing through a 12m room. That is the single
/// loudest tell that a space is fake — your ears know a concrete box before
/// your eyes do.
///
/// The room list comes from the map manifest (see G1MapManifest), so this
/// doesn't guess at "am I indoors" with raycasts that a catwalk or an overhang
/// would fool. It knows the actual boxes, and it knows how big each one is, so
/// a 40m aircraft hangar rings and a 4m sentry box goes dead and close.
public sealed class G1InteriorSpace : MonoBehaviour
{
    [System.Serializable]
    public struct Room
    {
        public string name;
        public Bounds bounds;      // world space, already inset to the inner faces
        public float size;         // largest floor dimension — drives the tail
    }

    public Room[] rooms = new Room[0];

    [Header("Outdoor baseline (captured at Start)")]
    public float outdoorFogEnd = 700f;
    public Color outdoorFog = new Color(0.4f, 0.43f, 0.48f);
    public Color outdoorAmbient = new Color(0.34f, 0.36f, 0.4f);

    [Header("Indoor look")]
    // Fog is a distance cue for a 800m battlefield. Indoors it only greys the
    // far wall of a room you are standing in, so it gets pushed back hard.
    public float indoorFogEnd = 2200f;
    public Color indoorFog = new Color(0.10f, 0.10f, 0.12f);
    public Color indoorAmbient = new Color(0.13f, 0.13f, 0.16f);
    public float blendSpeed = 3.5f;

    [Header("Indoor sound")]
    public float indoorLowpass = 2600f;    // the world outside, heard through walls
    public float outdoorLowpass = 22000f;  // i.e. off
    public float ambienceDuck = 0.25f;     // wind doesn't follow you inside

    /// Read by footsteps and anything else that wants to know. Static because
    /// there is exactly one player and one set of ears.
    public static bool PlayerIsIndoors { get; private set; }
    public static string PlayerRoom { get; private set; } = "";

    Transform player;
    AudioReverbFilter reverb;
    AudioLowPassFilter lowpass;
    G1Ambience ambience;
    float ambienceBase;
    float blend;              // 0 = outdoors, 1 = indoors
    float roomSize = 10f;
    float nextCheck;
    int lastIndex = -1;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p == null) { enabled = false; return; }
        player = p.transform;

        ambience = p.GetComponent<G1Ambience>();
        if (ambience != null) ambienceBase = ambience.volume;

        // both filters live on the listener so they colour the whole mix, not
        // one source at a time
        var listener = Object.FindObjectOfType<AudioListener>();
        var host = listener != null ? listener.gameObject : p;
        reverb = host.GetComponent<AudioReverbFilter>();
        if (reverb == null) reverb = host.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = AudioReverbPreset.User;
        lowpass = host.GetComponent<AudioLowPassFilter>();
        if (lowpass == null) lowpass = host.AddComponent<AudioLowPassFilter>();
        lowpass.cutoffFrequency = outdoorLowpass;

        outdoorFogEnd = RenderSettings.fogEndDistance;
        outdoorFog = RenderSettings.fogColor;
        outdoorAmbient = RenderSettings.ambientLight;
        ApplyDry();
    }

    void Update()
    {
        if (player == null) return;

        // 50 AABB tests is nothing, but there is no reason to pay for it every
        // frame — you cannot cross a wall in 120ms
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.12f;
            int idx = RoomAt(player.position);
            if (idx != lastIndex)
            {
                lastIndex = idx;
                PlayerIsIndoors = idx >= 0;
                PlayerRoom = idx >= 0 ? rooms[idx].name : "";
                if (idx >= 0) roomSize = rooms[idx].size;
            }
        }

        float want = PlayerIsIndoors ? 1f : 0f;
        if (!Mathf.Approximately(blend, want))
        {
            blend = Mathf.MoveTowards(blend, want, blendSpeed * Time.deltaTime);
            Apply();
        }
    }

    int RoomAt(Vector3 p)
    {
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i].bounds.Contains(p)) return i;
        return -1;
    }

    void ApplyDry()
    {
        blend = 0f;
        Apply();
    }

    void Apply()
    {
        RenderSettings.fogEndDistance = Mathf.Lerp(outdoorFogEnd, indoorFogEnd, blend);
        RenderSettings.fogColor = Color.Lerp(outdoorFog, indoorFog, blend);
        RenderSettings.ambientLight = Color.Lerp(outdoorAmbient, indoorAmbient, blend);

        if (lowpass != null)
            lowpass.cutoffFrequency = Mathf.Lerp(outdoorLowpass, indoorLowpass, blend);

        if (ambience != null)
            ambience.volume = ambienceBase * Mathf.Lerp(1f, ambienceDuck, blend);

        if (reverb != null)
        {
            // A big room rings; a small one slaps and dies. Tie the tail and the
            // reflection delay to the actual floor size rather than picking one
            // preset for every interior on the map.
            float t = Mathf.InverseLerp(4f, 40f, roomSize);
            reverb.dryLevel = Mathf.Lerp(0f, -300f, blend);
            reverb.room = Mathf.Lerp(-2000f, Mathf.Lerp(-800f, -200f, t), blend);
            reverb.decayTime = Mathf.Lerp(0.1f, Mathf.Lerp(0.5f, 2.6f, t), blend);
            reverb.reflectionsDelay = Mathf.Lerp(0f, Mathf.Lerp(0.005f, 0.03f, t), blend);
            reverb.reverbDelay = Mathf.Lerp(0f, Mathf.Lerp(0.01f, 0.05f, t), blend);
            reverb.reverbLevel = Mathf.Lerp(-2000f, Mathf.Lerp(200f, 900f, t), blend);
        }
    }
}
