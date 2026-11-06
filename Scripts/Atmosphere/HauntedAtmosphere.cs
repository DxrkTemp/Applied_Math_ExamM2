using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HauntedAtmosphere : MonoBehaviour
{
    [Header("Moonlight (outside)")]
    [Tooltip("Leave empty to use the first Directional Light in the scene.")]
    public Light moonLight;
    public Color moonColor = new Color(0.55f, 0.65f, 1f);
    public float moonIntensity = 0.35f;
    public Vector3 moonRotation = new Vector3(25f, -40f, 0f);
    [Tooltip("Turn off extra directional lights (e.g. the one inside the hallway prefab).")]
    public bool disableOtherDirectionalLights = true;

    [Header("Night sky + ambient")]
    public bool removeSkybox = true;
    public Color skyColor = new Color(0.02f, 0.025f, 0.06f);
    public Color ambientSky = new Color(0.06f, 0.07f, 0.13f);
    public Color ambientEquator = new Color(0.04f, 0.04f, 0.07f);
    public Color ambientGround = new Color(0.01f, 0.01f, 0.02f);

    [Header("Fog")]
    public bool useFog = true;
    public Color fogColor = new Color(0.03f, 0.035f, 0.07f);
    [Range(0f, 0.2f)] public float fogDensity = 0.045f;

    [Header("Lightning (outside)")]
    public bool lightning = true;
    public float minLightningGap = 8f;
    public float maxLightningGap = 20f;
    public float lightningIntensity = 2.5f;
    public AudioSource thunderAudio;

    [Header("Indoor flicker")]
    public bool autoAddFlickerToLights = true;
    [Range(0f, 1f)] public float flickerChance = 0.7f;

    [Header("Post processing (URP)")]
    public bool applyPostProcessing = true;
    [Tooltip("Leave empty to use the 'Global Volume' in the scene.")]
    public Volume globalVolume;

    Camera cam;

    void Awake()
    {
        SetupMoon();
        SetupSkyAndAmbient();
        SetupFog();
    }

    void Start()
    {
        if (autoAddFlickerToLights) AddFlickers();
        if (applyPostProcessing) SetupPostProcessing();
        if (lightning && moonLight != null) StartCoroutine(LightningLoop());
    }

    void SetupMoon()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        if (moonLight == null)
        {
            foreach (Light l in lights) if (l.type == LightType.Directional) { moonLight = l; break; }
        }
        if (moonLight == null)
        {
            GameObject go = new GameObject("Moonlight");
            moonLight = go.AddComponent<Light>();
            moonLight.type = LightType.Directional;
        }

        moonLight.color = moonColor;
        moonLight.intensity = moonIntensity;
        moonLight.shadows = LightShadows.Soft;
        moonLight.shadowStrength = 0.9f;
        moonLight.transform.rotation = Quaternion.Euler(moonRotation);
        RenderSettings.sun = moonLight;

        if (disableOtherDirectionalLights)
            foreach (Light l in lights)
                if (l != moonLight && l.type == LightType.Directional) l.enabled = false;
    }

    void SetupSkyAndAmbient()
    {
        cam = Camera.main;
        if (removeSkybox)
        {
            RenderSettings.skybox = null;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColor;
            }
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.reflectionIntensity = 0.3f;
    }

    void SetupFog()
    {
        RenderSettings.fog = useFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    void AddFlickers()
    {
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            if (l.GetComponent<FlickerLight>() != null) continue;
            if (Random.value > flickerChance) continue;
            l.gameObject.AddComponent<FlickerLight>();
        }
    }

    void SetupPostProcessing()
    {
        if (cam != null)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }

        if (globalVolume == null) globalVolume = FindFirstObjectByType<Volume>();
        if (globalVolume == null)
        {
            GameObject go = new GameObject("Haunted Volume");
            globalVolume = go.AddComponent<Volume>();
            globalVolume.isGlobal = true;
        }
        VolumeProfile profile = globalVolume.profile;

        if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.Override(0.45f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(Color.black);

        if (!profile.TryGet(out FilmGrain grain)) grain = profile.Add<FilmGrain>(true);
        grain.active = true;
        grain.intensity.Override(0.35f);

        if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
        color.active = true;
        color.postExposure.Override(-0.3f);
        color.saturation.Override(-35f);
        color.contrast.Override(15f);
        color.colorFilter.Override(new Color(0.85f, 0.92f, 1f));

        if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(0.9f);
    }

    IEnumerator LightningLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minLightningGap, maxLightningGap));

            int flashes = Random.Range(1, 4);
            for (int i = 0; i < flashes; i++)
            {
                moonLight.intensity = lightningIntensity;
                moonLight.color = Color.white;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                moonLight.intensity = moonIntensity;
                moonLight.color = moonColor;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            if (thunderAudio != null)
            {
                yield return new WaitForSeconds(Random.Range(0.3f, 1.2f));
                thunderAudio.Play();
            }
        }
    }
}
