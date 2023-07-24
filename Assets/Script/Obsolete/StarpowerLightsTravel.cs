using UnityEngine;

public class StarpowerLightsTravel : MonoBehaviour
{
    private Vector3 spLightsStartPos;
    private Vector3 spLightsEndPos = new(0, 0f, 20f);
    private float spLightsDuration = 2f;
    private float spLightsElapsedTime = 0;

    private void Awake()
    {
        spLightsStartPos = transform.position;
    }

    private void Update()
    {
        spLightsElapsedTime += Time.deltaTime;
        float percentageComplete = spLightsElapsedTime / spLightsDuration;

        transform.position = Vector3.Lerp(spLightsStartPos, spLightsStartPos + spLightsEndPos, percentageComplete);

        if (gameObject.transform.position == spLightsStartPos + spLightsEndPos)
        {
            Destroy(gameObject);
        }
    }
}