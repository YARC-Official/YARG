// PostProcessController.cs
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class PostProcessController : MonoBehaviour
{
    [Header("Post Process Volume")]
    public PostProcessVolume volume;

    private ColorGrading colorGrading;
    private Bloom bloom;
    private Vignette vignette;

    private string currentEffect = "";

void Awake()
{
    if (volume == null)
    {
        Debug.LogError("PostProcessVolume not assigned!");
        return;
    }

    bool cgFound = volume.profile.TryGetSettings(out colorGrading);
    bool bloomFound = volume.profile.TryGetSettings(out bloom);
    bool vignetteFound = volume.profile.TryGetSettings(out vignette);

    Debug.Log($"ColorGrading found: {cgFound}");
    Debug.Log($"Bloom found: {bloomFound}");
    Debug.Log($"Vignette found: {vignetteFound}");

    if (cgFound)
        Debug.LogError("BIG PP");
}

    public void SetPostProcess(string effectName)
    {
        currentEffect = effectName;

        ResetEffects();

        switch (effectName)
        {
            case "ProFilm_a.pp":
                Debug.LogWarning("cg no effect");
                // Default – no effect
                break;
            case "film_sepia_ink.pp":
                ApplySepiaEffect();
                break;
            case "film_b+w.pp":
                ApplyBlackWhiteEffect();
                Debug.LogWarning("Cg bnw");
                break;
            case "bloom.pp":
                ApplyBloomEffect();
                break;
            case "photocopy.pp":
                ApplyPhotocopyEffect();
                break;
        }
    }

    void ResetEffects()
    {
        if (colorGrading != null)
        {
            colorGrading.saturation.value = 0;
            colorGrading.colorFilter.value = Color.white;
        }

        if (bloom != null)
            bloom.intensity.value = 0;

        if (vignette != null)
            vignette.intensity.value = 0;
    }

    void ApplySepiaEffect()
    {
        if (colorGrading != null)
        {
            colorGrading.saturation.value = -20;
            colorGrading.colorFilter.value = new Color(0.9f, 0.7f, 0.4f);
        }
    }

    void ApplyBlackWhiteEffect()
    {
        if (colorGrading != null)
            colorGrading.saturation.value = -100;
            Debug.LogWarning("Cg bnw is on");
    }

    void ApplyBloomEffect()
    {
        if (bloom != null)
            bloom.intensity.value = 5f;
    }

    void ApplyPhotocopyEffect()
    {
        StartCoroutine(LowFrameRateEffect());
    }

    System.Collections.IEnumerator LowFrameRateEffect()
    {
        float originalTimeScale = Time.timeScale;

        for (int i = 0; i < 20; i++)
        {
            Time.timeScale = 0.5f;
            yield return new WaitForSecondsRealtime(0.1f);
            Time.timeScale = originalTimeScale;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        Time.timeScale = originalTimeScale;
    }
}
