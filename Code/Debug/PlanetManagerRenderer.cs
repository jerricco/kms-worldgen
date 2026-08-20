using Sandbox;
using System;
using Sandbox.GameData;

public sealed class PlanetManagerRenderer : Component
{
	// should always be attached
	[Property] private PlanetManagerComponent PlanetManager { get; set; }
	

	// planet models
	[Property] private ModelRenderer Sphere { get; set; }
	[Property] private GameObject SphereLineBucket { get; set; }
	private float _sphereRadius { get; set; }
	private int _sphereLinePointCount = 30;
	private float _totalRadius => _sphereRadius +_surfaceOffset;
	
	[Property] private ModelRenderer Plane { get; set; }
	[Property] private GameObject PlaneLineBucket { get; set; }
	
	// general
	private float _surfaceOffset = 0.5f;

	private struct LineConfig
	{
		public float Value { get; set; }
		public List<Vector3> Local3DPoints { get; set; } // list of points for the sphere
		public List<Vector3> LocalOrthoPoints { get; set; } // list of points for the plane
	}

	private Dictionary<string, LineConfig> ConfigLines = new();

	protected override void OnStart()
	{
		// get some useful stuff
		this._sphereRadius = this.Sphere.Bounds.Extents.Length;
		// add lines to the sphere
		foreach (var LineGameObject in this.SphereLineBucket.Children)
		{
			var Line =  LineGameObject.GetComponent<LineRenderer>();
			List<GameObject> localPoints = new List<GameObject>();
			for ( int i = 0; i < this._sphereLinePointCount; i++ )
			{
				float progress = (float)i / this._sphereLinePointCount;
				float angle = progress * MathF.PI * 2f;

				// Calculate local coordinates on the sphere surface
				float x = MathF.Cos( angle ) * _totalRadius;
				float y = MathF.Sin( angle ) * _totalRadius;
				float z = 0f;

				var point = new GameObject( $"Point {i}" );
				point.LocalPosition = new Vector3( x, y, z );
				point.SetParent(Line.GameObject);
			}
			
			Line.Points = localPoints;
		}
	}

	protected override void OnUpdate()
	{

	}
}
