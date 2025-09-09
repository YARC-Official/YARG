// LightingController.cs
using UnityEngine;
using System.Collections.Generic;

public class LightingController : MonoBehaviour
{
    [Header("Lighting Setup")]
    public Light[] stageLights;
    public Light[] spotlights;
    public Material skyboxMaterial;
    
    private Dictionary<string, LightingPreset> lightingPresets;
    private string currentLighting = "";
    
    void Awake()
    {
        InitializeLightingPresets();
    }
    
    void InitializeLightingPresets()
    {
        lightingPresets = new Dictionary<string, LightingPreset>
        {
            {"verse", new LightingPreset 
                { 
                    stageColor = new Color(0.8f, 0.6f, 0.4f), 
                    intensity = 0.7f, 
                    ambient = new Color(0.2f, 0.2f, 0.3f) 
                }},
            {"chorus", new LightingPreset 
                { 
                    stageColor = new Color(1f, 0.2f, 0.2f), 
                    intensity = 1.2f, 
                    ambient = new Color(0.3f, 0.1f, 0.1f) 
                }},
            {"manual_cool", new LightingPreset 
                { 
                    stageColor = new Color(0.2f, 0.4f, 1f), 
                    intensity = 0.8f, 
                    ambient = new Color(0.1f, 0.2f, 0.3f) 
                }},
            {"manual_warm", new LightingPreset 
                { 
                    stageColor = new Color(1f, 0.6f, 0.2f), 
                    intensity = 0.9f, 
                    ambient = new Color(0.3f, 0.2f, 0.1f) 
                }},
            {"blackout_fast", new LightingPreset 
                { 
                    stageColor = Color.black, 
                    intensity = 0f, 
                    ambient = new Color(0.05f, 0.05f, 0.05f) 
                }},
            {"strobe_fast", new LightingPreset 
                { 
                    stageColor = Color.white, 
                    intensity = 2f, 
                    ambient = Color.white,
                    isStrobe = true 
                }}
        };
    }
    
    public void SetLighting(string lightingName)
    {
        if (lightingPresets.ContainsKey(lightingName))
        {
            currentLighting = lightingName;
            ApplyLightingPreset(lightingPresets[lightingName]);
        }
    }
    
    void ApplyLightingPreset(LightingPreset preset)
    {
        // Apply to stage lights
        foreach (Light light in stageLights)
        {
            if (light != null)
            {
                light.color = preset.stageColor;
                light.intensity = preset.intensity;
            }
        }
        
        // Set ambient lighting
        RenderSettings.ambientLight = preset.ambient;
        
        // Handle strobe effect
        if (preset.isStrobe)
        {
            StartCoroutine(StrobeEffect());
        }
    }
    
    System.Collections.IEnumerator StrobeEffect()
    {
        float strobeRate = 0.1f; // 60 ticks = fast strobe
        
        for (int i = 0; i < 10; i++) // Strobe 10 times
        {
            foreach (Light light in stageLights)
            {
                if (light != null) light.intensity = 0f;
            }
            yield return new WaitForSeconds(strobeRate);
            
            foreach (Light light in stageLights)
            {
                if (light != null) light.intensity = 2f;
            }
            yield return new WaitForSeconds(strobeRate);
        }
    }
}

[System.Serializable]
public class LightingPreset
{
    public Color stageColor = Color.white;
    public float intensity = 1f;
    public Color ambient = Color.gray;
    public bool isStrobe = false;
}