using UnityEngine;

/// Plays a looping nature soundscape: soft leaf texture plus synthesized bird calls.
/// The weather system already provides wind, so this only adds wildlife/foliage.
/// Assign real recordings to the clip fields to replace the synthesized versions.
public class NatureAmbience : MonoBehaviour
{
    [Header("Clip overrides (synthesized when empty)")]
    public AudioClip windClip;
    public AudioClip birdClip;

    [Header("Mix")]
    [Range(0f, 1f)] public float windVolume = 0.12f;
    [Range(0f, 1f)] public float birdVolume = 0.5f;

    void Awake()
    {
        AddLoop(windClip != null ? windClip : ProceduralAudio.LeavesLoop(), windVolume);
        AddLoop(birdClip != null ? birdClip : ProceduralAudio.BirdLoop(), birdVolume);
    }

    void AddLoop(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;

        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;      // ambient beds are 2D, heard everywhere
        src.volume = volume;
        src.playOnAwake = false;
        src.Play();
    }
}
