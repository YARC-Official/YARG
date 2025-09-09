// CameraController.cs
using UnityEngine;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    [Header("Camera Setup")]
    public Camera mainCamera;
    public Transform[] bandMembers; // 0=vocals, 1=guitar, 2=bass, 3=drums, 4=keys
    
    [Header("Camera Positions")]
    public Transform[] cameraPositions;

    [Header("Transition Settings")]
    public float transitionDistanceThreshold = 5f;
    
    private Dictionary<string, CameraShot> cameraShots;
    private string currentShot = "";
    
    void Awake()
    {
        InitializeCameraShots();
    }
    
    void InitializeCameraShots()
    {
        cameraShots = new Dictionary<string, CameraShot>
        {
            // Generic shots
            {"coop_all_near", new CameraShot { positions = new int[]{0}, distance = 8f, height = 2f }},
            {"coop_all_far", new CameraShot { positions = new int[]{0}, distance = 12f, height = 3f }},
            {"coop_all_behind", new CameraShot { positions = new int[]{0}, distance = 6f, height = 1f, behind = true }},
            
            // Single character shots
            {"coop_v_near", new CameraShot { positions = new int[]{0}, distance = 4f, height = 1.5f }},
            {"coop_g_near", new CameraShot { positions = new int[]{1}, distance = 4f, height = 1.5f }},
            {"coop_b_near", new CameraShot { positions = new int[]{2}, distance = 4f, height = 1.5f }},
            {"coop_d_near", new CameraShot { positions = new int[]{3}, distance = 5f, height = 2f }},
            {"coop_k_near", new CameraShot { positions = new int[]{4}, distance = 4f, height = 1.5f }},
            
            // Two character shots
            {"coop_gv_near", new CameraShot { positions = new int[]{0,1}, distance = 6f, height = 1.8f }},
            {"coop_bv_near", new CameraShot { positions = new int[]{0,2}, distance = 6f, height = 1.8f }},
            {"coop_bg_near", new CameraShot { positions = new int[]{1,2}, distance = 6f, height = 1.8f }},
        };
    }
    
    public void SetCameraShot(string shotName)
    {
        if (cameraShots.ContainsKey(shotName))
        {
            currentShot = shotName;
            ApplyCameraShot(cameraShots[shotName]);
        }
    }
    
    public void SetDirectedCut(string cutName)
    {
        // Simplified directed cuts - just apply with slight variation
        string baseCut = cutName.Replace("directed_", "coop_");
        if (cutName.Contains("all"))
            SetCameraShot("coop_all_near");
        else if (cutName.Contains("vocals"))
            SetCameraShot("coop_v_near");
        else if (cutName.Contains("guitar"))
            SetCameraShot("coop_g_near");
        else if (cutName.Contains("bass"))
            SetCameraShot("coop_b_near");
        else if (cutName.Contains("drums"))
            SetCameraShot("coop_d_near");
    }
    
    void ApplyCameraShot(CameraShot shot)
    {
        if (bandMembers.Length == 0) return;
        
        Vector3 targetPos = Vector3.zero;
        int validMembers = 0;
        
        // Calculate center point of target band members
        foreach (int memberIndex in shot.positions)
        {
            if (memberIndex < bandMembers.Length && bandMembers[memberIndex] != null)
            {
                targetPos += bandMembers[memberIndex].position;
                validMembers++;
            }
        }
        
        if (validMembers > 0)
        {
            targetPos /= validMembers;
            
            // Apply camera positioning
            Vector3 cameraPos = targetPos;
            cameraPos += shot.behind ? Vector3.back * shot.distance : Vector3.forward * shot.distance;
            cameraPos.y += shot.height;
            
           float distanceToNewPos = Vector3.Distance(mainCamera.transform.position, cameraPos);

            if (distanceToNewPos <= transitionDistanceThreshold)
            {
                StartCoroutine(SmoothCameraTransition(cameraPos, targetPos));
            }
            else
            {
                mainCamera.transform.position = cameraPos;
                mainCamera.transform.rotation = Quaternion.LookRotation(targetPos - cameraPos);
            }

        }
    }
    
    System.Collections.IEnumerator SmoothCameraTransition(Vector3 newPos, Vector3 lookAtPos)
    {
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(lookAtPos - newPos);
        
        float duration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            mainCamera.transform.position = Vector3.Lerp(startPos, newPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            
            yield return null;
        }
    }
}

[System.Serializable]
public class CameraShot
{
    public int[] positions; // Band member indices
    public float distance = 5f;
    public float height = 2f;
    public bool behind = false;
}