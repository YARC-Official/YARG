using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class BendControllerRadial : MonoBehaviour 
{
	[SerializeField] bool m_bendOn = true;

	[SerializeField] Transform m_curveOrigin;
	[SerializeField] Transform m_referenceDirection;
	[SerializeField] float m_curvature = 0f;
	
	[Range(0.5f, 2f)]
	[SerializeField] float m_xScale = 1f;
	[Range(0.5f, 2f)]
	[SerializeField] float m_zScale = 1f;
	
	[SerializeField] float m_flatMargin = 0f;

	[SerializeField] bool m_horizonWaves = false;

	[Range(0f, 10f)]
	[SerializeField] float m_horizonWaveFrequency = 0f;
	
	private int m_curveOriginId;
	private int m_referenceDirectionId;
	private int m_curvatureId;
	private int m_scaleId;
	private int m_flatMarginId;
	private int m_horizonWaveFrequencyId;

	private Vector3 m_scale = Vector3.zero;
	
	
	void Start() 
	{
		m_curveOriginId = Shader.PropertyToID("_CurveOrigin");
		m_referenceDirectionId = Shader.PropertyToID("_ReferenceDirection");
		m_curvatureId = Shader.PropertyToID("_Curvature");
		m_scaleId = Shader.PropertyToID("_Scale");
		m_flatMarginId = Shader.PropertyToID("_FlatMargin");
		m_horizonWaveFrequencyId = Shader.PropertyToID("_HorizonWaveFrequency");
		
		if (m_curveOrigin == null)
			SetCurveOrigin();
	}
	
	
	void Update() 
	{
		m_scale.x = m_xScale;
		m_scale.z = m_zScale;

		if (m_horizonWaves)
			Shader.EnableKeyword("HORIZON_WAVES");
		else
			Shader.DisableKeyword("HORIZON_WAVES");

		if (m_bendOn)
			Shader.EnableKeyword("BEND_ON");
		else
			Shader.DisableKeyword("BEND_ON");

		Shader.SetGlobalVector(m_curveOriginId, m_curveOrigin.position);
		Shader.SetGlobalVector(m_referenceDirectionId, m_referenceDirection.forward);
		Shader.SetGlobalFloat(m_curvatureId, m_curvature * 0.00001f);
		Shader.SetGlobalVector(m_scaleId, m_scale);
		Shader.SetGlobalFloat(m_flatMarginId, m_flatMargin);
		Shader.SetGlobalFloat(m_horizonWaveFrequencyId, m_horizonWaveFrequency);
	}
	
	
	private void SetCurveOrigin()
	{
        m_curveOrigin = Camera.main.transform;
	}
	
	
	private void OnDisable()
	{
		Shader.SetGlobalVector(m_curveOriginId, Vector3.zero);
		Shader.SetGlobalFloat(m_curvatureId, 0);
	}
}
