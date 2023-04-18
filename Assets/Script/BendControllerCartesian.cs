using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class BendControllerCartesian : MonoBehaviour 
{
	[SerializeField] Transform m_curveOrigin;
	[SerializeField] float m_curvatureScale = 1f;

	[Range(-1, 1)]
	[SerializeField] float m_xCurvature = 1f;
	[Range(-1, 1)]
	[SerializeField] float m_zCurvature = 1f;

	[SerializeField] float m_xFlatMargin = 0f;
	[SerializeField] float m_zFlatMargin = 0f;
	
	private int m_curveOriginId;
	private int m_curvatureScaleId;
	private int m_xCurvatureId;
	private int m_zCurvatureId;
	private int m_xFlatMarginId;
	private int m_zFlatMarginId;
	
	
	void Start() 
	{
		m_curveOriginId = Shader.PropertyToID("_CurveOrigin");
		m_curvatureScaleId = Shader.PropertyToID("_CurvatureScale");
		m_xCurvatureId = Shader.PropertyToID("_XCurvature");
		m_zCurvatureId = Shader.PropertyToID("_ZCurvature");
		m_xFlatMarginId = Shader.PropertyToID("_XFlatMargin");
		m_zFlatMarginId = Shader.PropertyToID("_ZFlatMargin");
		
		if (m_curveOrigin == null)
			SetCurveOrigin();
	}
	
	
	void Update() 
	{
		//Shader.EnableKeyword("BEND_ON");
		Shader.SetGlobalVector(m_curveOriginId, m_curveOrigin.position);
		Shader.SetGlobalFloat(m_curvatureScaleId, m_curvatureScale);
		Shader.SetGlobalFloat(m_xCurvatureId, m_xCurvature);
		Shader.SetGlobalFloat(m_zCurvatureId, m_zCurvature);
		Shader.SetGlobalFloat(m_xFlatMarginId, m_xFlatMargin);
		Shader.SetGlobalFloat(m_zFlatMarginId, m_zFlatMargin);
	}
	
	
	private void SetCurveOrigin()
	{
        m_curveOrigin = Camera.main.transform;
	}
	
	
	private void OnEnable()
	{

	}
	
	
	private void OnDisable()
	{

		//Shader.DisableKeyword("BEND_ON");
		Shader.SetGlobalVector(m_curveOriginId, Vector3.zero);
		Shader.SetGlobalFloat(m_curvatureScaleId, 0);
	}
}
