using UnityEngine;

/// Builds small synthesized audio clips at runtime so the game has gunshot and
/// nature sound without needing sound files. Real AudioClips assigned on Gun /
/// NatureAmbience replace these whenever you have recordings.
public static class ProceduralAudio
{
    const int SampleRate = 44100;

    static AudioClip gunshotCache;
    static AudioClip birdCache;
    static AudioClip leavesCache;

    // ---------------------------------------------------------------- gunshot

    /// A single rifle shot: sharp crack, mid body, low thump and a short tail.
    public static AudioClip Gunshot()
    {
        if (gunshotCache != null) return gunshotCache;

        const float duration = 0.35f;
        int count = (int)(duration * SampleRate);
        float[] data = new float[count];

        System.Random rng = new System.Random(918273);
        float smoothed = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            smoothed = smoothed * 0.35f + noise * 0.65f;   // one-pole low-pass, gives the shot body

            float crack = noise * Mathf.Exp(-t * 55f) * 0.85f;                              // muzzle report
            float body = smoothed * Mathf.Exp(-t * 16f) * 0.65f;                            // mid weight
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 24f) * 0.5f;  // low punch
            float tail = smoothed * Mathf.Exp(-t * 6f) * 0.12f;                             // short decay

            data[i] = Mathf.Clamp(crack + body + thump + tail, -1f, 1f);
        }

        FadeEnd(data, SampleRate / 100);   // no click when the clip stops

        gunshotCache = AudioClip.Create("Gunshot_Synth", count, 1, SampleRate, false);
        gunshotCache.SetData(data, 0);
        return gunshotCache;
    }

    // ---------------------------------------------------------------- nature

    /// A seamless loop of bird calls over soft leaf rustle.
    public static AudioClip BirdLoop(float seconds = 22f)
    {
        if (birdCache != null) return birdCache;
        birdCache = BuildLoop("Birds_Synth", seconds, true);
        return birdCache;
    }

    /// A seamless loop of soft wind/leaves (used only when no wind clip is assigned).
    public static AudioClip LeavesLoop(float seconds = 22f)
    {
        if (leavesCache != null) return leavesCache;
        leavesCache = BuildLoop("Leaves_Synth", seconds, false);
        return leavesCache;
    }

    static AudioClip BuildLoop(string name, float seconds, bool withBirds)
    {
        int count = (int)(seconds * SampleRate);
        int xf = SampleRate / 2;                 // half a second crossfade keeps the loop seamless
        float[] data = new float[count + xf];

        System.Random rng = new System.Random(withBirds ? 4711 : 1337);

        // soft leaf rustle: heavily smoothed noise with slow swells, like wind through foliage
        float smoothed = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            smoothed = smoothed * 0.92f + noise * 0.08f;
            float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / 5.1f);
            data[i] = smoothed * swell * (withBirds ? 0.16f : 0.5f);
        }

        if (withBirds)
        {
            // bird calls spread through the loop, kept away from the edges
            const int calls = 11;
            for (int c = 0; c < calls; c++)
            {
                float at = 1.5f + (c + (float)rng.NextDouble() * 0.7f) * (seconds - 3f) / calls;
                int start = (int)(at * SampleRate);
                int notes = rng.Next(2, 5);
                for (int n = 0; n < notes; n++)
                {
                    WriteChirp(data, start, rng, 2400f + (float)rng.NextDouble() * 1800f, 0.07f, 0.28f);
                    start += (int)((0.075f + (float)rng.NextDouble() * 0.06f) * SampleRate);
                }
            }
        }

        // fold the overrun back over the head so the loop point is seamless
        for (int i = 0; i < xf; i++)
        {
            float k = i / (float)xf;
            data[i] = data[i] * k + data[count + i] * (1f - k);
        }

        float[] loop = new float[count];
        System.Array.Copy(data, loop, count);

        AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(loop, 0);
        return clip;
    }

    /// One quick bird note: a frequency-bent sine with vibrato and a smooth up/down envelope.
    static void WriteChirp(float[] data, int start, System.Random rng, float freq, float duration, float gain)
    {
        int n = (int)(duration * SampleRate);
        float phase = 0f;
        float bend = 0.8f + (float)rng.NextDouble() * 0.7f;   // some chirps rise, some fall

        for (int i = 0; i < n; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= data.Length) return;

            float t = i / (float)n;
            float f = freq * Mathf.Lerp(1f, bend, t) * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 22f * t * duration));
            phase += 2f * Mathf.PI * f / SampleRate;
            float env = Mathf.Sin(Mathf.PI * t);
            data[idx] += Mathf.Sin(phase) * env * gain;
        }
    }

    static void FadeEnd(float[] data, int fadeSamples)
    {
        for (int i = 0; i < fadeSamples && i < data.Length; i++)
            data[data.Length - 1 - i] *= i / (float)fadeSamples;
    }
}
