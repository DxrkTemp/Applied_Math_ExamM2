using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Header("Base")]
    [Tooltip("Leave 0 to use the light's current intensity.")]
    public float baseIntensity = 0f;
    [Range(0f, 1f)] public float buzzAmount = 0.15f;
    public float buzzSpeed = 8f;

    [Header("Dropouts (lights cut out)")]
    public float minTimeBetweenDropouts = 1.5f;
    public float maxTimeBetweenDropouts = 6f;
    [Tooltip("How many quick blinks happen in one dropout.")]
    public int minBlinks = 1;
    public int maxBlinks = 5;
    [Tooltip("Chance the light stays dead for a while after a dropout.")]
    [Range(0f, 1f)] public float longBlackoutChance = 0.15f;
    public float longBlackoutDuration = 2f;

    [Header("Player proximity")]
    public bool reactToPlayer = true;
    public float scareRadius = 5f;
    [Tooltip("Dropouts happen this many times more often near the player.")]
    public float scareMultiplier = 3f;

    [Header("Fixture glow (optional)")]
    public Renderer[] emissiveRenderers;
    public Color emissionColor = new Color(0.8f, 0.9f, 1f);
    public float emissionStrength = 2f;

    [Header("Buzz sound (optional)")]
    public AudioSource buzzAudio;

    Light lightSource;
    float noiseSeed;
    float dropoutMultiplier = 1f;
    Transform player;
    Material[] emissiveMats;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = lightSource.intensity;
        noiseSeed = Random.value * 100f;

        if (emissiveRenderers != null && emissiveRenderers.Length > 0)
        {
            emissiveMats = new Material[emissiveRenderers.Length];
            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                if (emissiveRenderers[i] == null) continue;
                emissiveMats[i] = emissiveRenderers[i].material;
                emissiveMats[i].EnableKeyword("_EMISSION");
            }
        }
    }

    void OnEnable()
    {
        StartCoroutine(DropoutLoop());
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        float buzz = 1f - buzzAmount * Mathf.PerlinNoise(noiseSeed, Time.time * buzzSpeed);
        float k = buzz * dropoutMultiplier;
        lightSource.intensity = baseIntensity * k;

        if (emissiveMats != null)
        {
            Color c = emissionColor * (emissionStrength * k);
            foreach (Material m in emissiveMats) if (m != null) m.SetColor("_EmissionColor", c);
        }
        if (buzzAudio != null) buzzAudio.volume = 0.4f * k;
    }

    bool PlayerNear()
    {
        if (!reactToPlayer || player == null) return false;
        return (player.position - transform.position).sqrMagnitude < scareRadius * scareRadius;
    }

    IEnumerator DropoutLoop()
    {
        yield return new WaitForSeconds(Random.Range(0f, maxTimeBetweenDropouts));
        while (true)
        {
            float wait = Random.Range(minTimeBetweenDropouts, maxTimeBetweenDropouts);
            if (PlayerNear()) wait /= Mathf.Max(1f, scareMultiplier);
            yield return new WaitForSeconds(wait);

            int blinks = Random.Range(minBlinks, maxBlinks + 1);
            for (int i = 0; i < blinks; i++)
            {
                dropoutMultiplier = Random.Range(0f, 0.15f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
                dropoutMultiplier = Random.Range(0.6f, 1.1f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.10f));
            }

            if (Random.value < longBlackoutChance)
            {
                dropoutMultiplier = 0f;
                yield return new WaitForSeconds(longBlackoutDuration * Random.Range(0.6f, 1.4f));

                for (int i = 0; i < 3; i++)
                {
                    dropoutMultiplier = 0.8f; yield return new WaitForSeconds(0.05f);
                    dropoutMultiplier = 0f;   yield return new WaitForSeconds(0.08f);
                }
            }
            dropoutMultiplier = 1f;
        }
    }
}
