// @Minionsart version
// credits  to  forkercat https://gist.github.com/junhaowww/fb6c030c17fe1e109a34f1c92571943f
// and  NedMakesGames https://gist.github.com/NedMakesGames/3e67fabe49e2e3363a657ef8a6a09838
// for the base setup for compute shaders
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[ExecuteInEditMode]
public class GrassComputeScript : MonoBehaviour
{
	[Header("Components")]
	[SerializeField] private GrassPainter grassPainter = default;
	[SerializeField] private Mesh sourceMesh = default;
	[SerializeField] private Material material = default;
	[SerializeField] private ComputeShader computeShader = default;

	// Blade
	[Header("Blade")]
	public float grassHeight = 1;
	public float grassWidth = 0.06f;
	public float grassRandomHeight = 0.25f;
	[Range(0, 1)] public float bladeRadius = 0.6f;
	[Range(0, 1)] public float bladeForwardAmount = 0.38f;
	[Range(1, 5)] public float bladeCurveAmount = 2;

	[SerializeField]
	ShaderInteractor interactor;

	// Wind
	[Header("Wind")]
	public float windSpeed = 10;
	public float windStrength = 0.05f;
	// Interactor
	[Header("Interactor")]
	public float affectRadius = 0.3f;
	public float affectStrength = 5;
	// LOD
	[Header("LOD")]
	public float minFadeDistance = 40;
	public float maxFadeDistance = 60;
	// Material
	[Header("Material")]
	public Color topTint = new Color(1, 1, 1);
	public Color bottomTint = new Color(0, 0, 1);
	public float ambientStrength = 0.1f;
	// Other
	[Header("Other")]
	public UnityEngine.Rendering.ShadowCastingMode castShadow;

	private Camera m_MainCamera;

	private readonly int m_AllowedBladesPerVertex = 6;
	private readonly int m_AllowedSegmentsPerBlade = 7;

	// The structure to send to the compute shader
	// This layout kind assures that the data is laid out sequentially
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind
		.Sequential)]
	private struct SourceVertex
	{
		public Vector3 position;
		public Vector3 normal;
		public Vector2 uv;
		public Vector3 color;
	}

	// A state variable to help keep track of whether compute buffers have been set up
	private bool m_Initialized;
	// A compute buffer to hold vertex data of the source mesh
	private ComputeBuffer m_SourceVertBuffer;
	// A compute buffer to hold vertex data of the generated mesh
	private ComputeBuffer m_DrawBuffer;
	// A compute buffer to hold indirect draw arguments
	private ComputeBuffer m_ArgsBuffer;
	// Instantiate the shaders so data belong to their unique compute buffers
	private ComputeShader m_InstantiatedComputeShader;
	private Material m_InstantiatedMaterial;
	// The id of the kernel in the grass compute shader
	private int m_IdGrassKernel;
	// The x dispatch size for the grass compute shader
	private int m_DispatchSize;
	// The local bounds of the generated mesh
	private Bounds m_LocalBounds;

	private Camera sceneCam;

	// The size of one entry in the various compute buffers
	private const int SOURCE_VERT_STRIDE = sizeof(float) * (3 + 3 + 2 + 3);
	private const int DRAW_STRIDE = sizeof(float) * (3 + (3 + 2 + 3) * 3);
	private const int INDIRECT_ARGS_STRIDE = sizeof(int) * 4;

	// The data to reset the args buffer with every frame
	// 0: vertex count per draw instance. We will only use one instance
	// 1: instance count. One
	// 2: start vertex location if using a Graphics Buffer
	// 3: and start instance location if using a Graphics Buffer
	private int[] argsBufferReset = new int[] { 0, 1, 0, 0 };

#if UNITY_EDITOR
	SceneView view;


	// get the scene camera in case of no maincam
	void OnFocus()
	{
		// Remove delegate listener if it has previously
		// been assigned.
		SceneView.duringSceneGui -= this.OnScene;
		// Add (or re-add) the delegate.
		SceneView.duringSceneGui += this.OnScene;
	}

	void OnDestroy()
	{
		// When the window is destroyed, remove the delegate
		// so that it will no longer do any drawing.
		SceneView.duringSceneGui -= this.OnScene;
	}

	void OnScene(SceneView scene)
	{
		view = scene;

	}

#endif
	private void OnValidate()
	{
		// Set up components
		m_MainCamera = Camera.main;
		grassPainter = GetComponent<GrassPainter>();
		sourceMesh = GetComponent<MeshFilter>().sharedMesh;  // generated by GeometryGrassPainter
	}

	private void OnEnable()
	{
		// If initialized, call on disable to clean things up
		if (m_Initialized)
		{
			OnDisable();
		}
#if UNITY_EDITOR
		SceneView.duringSceneGui += this.OnScene;
#endif
		m_MainCamera = Camera.main;

		// Setup compute shader and material manually

		// Don't do anything if resources are not found,
		// or no vertex is put on the mesh.
		if (grassPainter == null || sourceMesh == null || computeShader == null || material == null)
		{
			return;
		}
		sourceMesh = GetComponent<MeshFilter>().sharedMesh;

		if (sourceMesh.vertexCount == 0)
		{
			return;
		}

		m_Initialized = true;

		// Instantiate the shaders so they can point to their own buffers
		m_InstantiatedComputeShader = Instantiate(computeShader);
		m_InstantiatedMaterial = Instantiate(material);

		// Grab data from the source mesh
		Vector3[] positions = sourceMesh.vertices;
		Vector3[] normals = sourceMesh.normals;
		Vector2[] uvs = sourceMesh.uv;
		Color[] colors = sourceMesh.colors;

		// Create the data to upload to the source vert buffer
		SourceVertex[] vertices = new SourceVertex[positions.Length];
		for (int i = 0; i < vertices.Length; i++)
		{
			Color color = colors[i];
			vertices[i] = new SourceVertex()
			{
				position = positions[i],
				normal = normals[i],
				uv = uvs[i],
				color = new Vector3(color.r, color.g, color.b) // Color --> Vector3
			};
		}

		int numSourceVertices = vertices.Length;

		// Each segment has two points
		int maxBladesPerVertex = Mathf.Max(1, m_AllowedBladesPerVertex);
		int maxSegmentsPerBlade = Mathf.Max(1, m_AllowedSegmentsPerBlade);
		int maxBladeTriangles = maxBladesPerVertex * ((maxSegmentsPerBlade - 1) * 2 + 1);

		// Create compute buffers
		// The stride is the size, in bytes, each object in the buffer takes up
		m_SourceVertBuffer = new ComputeBuffer(vertices.Length, SOURCE_VERT_STRIDE,
			ComputeBufferType.Structured, ComputeBufferMode.Immutable);
		m_SourceVertBuffer.SetData(vertices);

		m_DrawBuffer = new ComputeBuffer(numSourceVertices * maxBladeTriangles, DRAW_STRIDE,
			ComputeBufferType.Append);
		m_DrawBuffer.SetCounterValue(0);

		m_ArgsBuffer =
			new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, ComputeBufferType.IndirectArguments);

		// Cache the kernel IDs we will be dispatching
		m_IdGrassKernel = m_InstantiatedComputeShader.FindKernel("Main");

		// Set buffer data
		m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_SourceVertices",
			m_SourceVertBuffer);
		m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_DrawTriangles", m_DrawBuffer);
		m_InstantiatedComputeShader.SetBuffer(m_IdGrassKernel, "_IndirectArgsBuffer",
			m_ArgsBuffer);
		// Set vertex data
		m_InstantiatedComputeShader.SetInt("_NumSourceVertices", numSourceVertices);
		m_InstantiatedComputeShader.SetInt("_MaxBladesPerVertex", maxBladesPerVertex);
		m_InstantiatedComputeShader.SetInt("_MaxSegmentsPerBlade", maxSegmentsPerBlade);

		m_InstantiatedMaterial.SetBuffer("_DrawTriangles", m_DrawBuffer);

		m_InstantiatedMaterial.SetColor("_TopTint", topTint);
		m_InstantiatedMaterial.SetColor("_BottomTint", bottomTint);
		m_InstantiatedMaterial.SetFloat("_AmbientStrength", ambientStrength);


		// Calculate the number of threads to use. Get the thread size from the kernel
		// Then, divide the number of triangles by that size
		m_InstantiatedComputeShader.GetKernelThreadGroupSizes(m_IdGrassKernel,
			out uint threadGroupSize, out _, out _);
		m_DispatchSize = Mathf.CeilToInt((float)numSourceVertices / threadGroupSize);

		// Get the bounds of the source mesh and then expand by the maximum blade width and height
		m_LocalBounds = sourceMesh.bounds;
		m_LocalBounds.Expand(Mathf.Max(grassHeight + grassRandomHeight, grassWidth));

		SetGrassDataBase();
	}

	private void OnDisable()
	{
		// Dispose of buffers and copied shaders here
		if (m_Initialized)
		{
			// If the application is not in play mode, we have to call DestroyImmediate
			if (Application.isPlaying)
			{
				Destroy(m_InstantiatedComputeShader);
				Destroy(m_InstantiatedMaterial);
			}
			else
			{
				DestroyImmediate(m_InstantiatedComputeShader);
				DestroyImmediate(m_InstantiatedMaterial);
			}

			// Release each buffer
			m_SourceVertBuffer?.Release();
			m_DrawBuffer?.Release();
			m_ArgsBuffer?.Release();
		}

		m_Initialized = false;
	}

	// LateUpdate is called after all Update calls
	private void LateUpdate()
	{
		// If in edit mode, we need to update the shaders each Update to make sure settings changes are applied
		// Don't worry, in edit mode, Update isn't called each frame
		if (Application.isPlaying == false)
		{
			OnDisable();
			OnEnable();
		}

		// If not initialized, do nothing (creating zero-length buffer will crash)
		if (!m_Initialized)
		{
			// Initialization is not done, please check if there are null components
			// or just because there is not vertex being painted.
			return;
		}

		// Clear the draw and indirect args buffers of last frame's data
		m_DrawBuffer.SetCounterValue(0);
		m_ArgsBuffer.SetData(argsBufferReset);

		// Transform the bounds to world space
		Bounds bounds = TransformBounds(m_LocalBounds);

		// Update the shader with frame specific data
		SetGrassDataUpdate();

		// Dispatch the grass shader. It will run on the GPU
		m_InstantiatedComputeShader.Dispatch(m_IdGrassKernel, m_DispatchSize, 1, 1);

		// DrawProceduralIndirect queues a draw call up for our generated mesh
		Graphics.DrawProceduralIndirect(m_InstantiatedMaterial, bounds, MeshTopology.Triangles,
			m_ArgsBuffer, 0, null, null, castShadow, true, gameObject.layer);
	}

	private void SetGrassDataBase()
	{
		// Send things to compute shader that dont need to be set every frame
		m_InstantiatedComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
		m_InstantiatedComputeShader.SetFloat("_Time", Time.time);

		m_InstantiatedComputeShader.SetFloat("_GrassHeight", grassHeight);
		m_InstantiatedComputeShader.SetFloat("_GrassWidth", grassWidth);
		m_InstantiatedComputeShader.SetFloat("_GrassRandomHeight", grassRandomHeight);

		m_InstantiatedComputeShader.SetFloat("_WindSpeed", windSpeed);
		m_InstantiatedComputeShader.SetFloat("_WindStrength", windStrength);

		m_InstantiatedComputeShader.SetFloat("_InteractorRadius", affectRadius);
		m_InstantiatedComputeShader.SetFloat("_InteractorStrength", affectStrength);

		m_InstantiatedComputeShader.SetFloat("_BladeRadius", bladeRadius);
		m_InstantiatedComputeShader.SetFloat("_BladeForward", bladeForwardAmount);
		m_InstantiatedComputeShader.SetFloat("_BladeCurve", Mathf.Max(0, bladeCurveAmount));

		m_InstantiatedComputeShader.SetFloat("_MinFadeDist", minFadeDistance);
		m_InstantiatedComputeShader.SetFloat("_MaxFadeDist", maxFadeDistance);

	}

	private void SetGrassDataUpdate()
	{
		// Compute Shader
		//  m_InstantiatedComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
		m_InstantiatedComputeShader.SetFloat("_Time", Time.time);
		if (interactor != null)
		{
			m_InstantiatedComputeShader.SetVector("_PositionMoving", interactor.transform.position);
		}
		else
		{
			m_InstantiatedComputeShader.SetVector("_PositionMoving", Vector3.zero);
		}

		if (m_MainCamera != null)
		{
			m_InstantiatedComputeShader.SetVector("_CameraPositionWS", m_MainCamera.transform.position);

		}
#if UNITY_EDITOR
		// if we dont have a main camera (it gets added during gameplay), use the scene camera
		else if (view != null)
		{
			m_InstantiatedComputeShader.SetVector("_CameraPositionWS", view.camera.transform.position);
		}
#endif

	}


	// This applies the game object's transform to the local bounds
	// Code by benblo from https://answers.unity.com/questions/361275/cant-convert-bounds-from-world-coordinates-to-loca.html
	private Bounds TransformBounds(Bounds boundsOS)
	{
		var center = transform.TransformPoint(boundsOS.center);

		// transform the local extents' axes
		var extents = boundsOS.extents;
		var axisX = transform.TransformVector(extents.x, 0, 0);
		var axisY = transform.TransformVector(0, extents.y, 0);
		var axisZ = transform.TransformVector(0, 0, extents.z);

		// sum their absolute value to get the world extents
		extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
		extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
		extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

		return new Bounds { center = center, extents = extents };
	}
}
