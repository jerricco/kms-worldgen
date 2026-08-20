using Sandbox;
using System;
using System.Reflection;
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
	private float _surfaceOffset = 0.1f;
	
	// storage for special values
	private float _horizontalClimateOffset { get; set; }

	private struct LineConfig
	{
		public string Name { get; set; }
		public object Value { get; set; }
		public List<Vector3> Local3DPoints { get; set; } // list of points for the sphere
		public List<Vector3> LocalOrthoPoints { get; set; } // list of points for the plane
	}

	private Dictionary<string, LineConfig> ConfigLines = new();

	protected override void OnStart()
	{
		// get some useful stuff
		this._sphereRadius = this.Sphere.Bounds.Extents.Length;
		this._horizontalClimateOffset = PlanetManager.HorizontalClimateOffset;
		
		// loop over the config items and ensure they are populated into ConfigLines.
		// this will need to ensure any specific rules other than equatorial distance are accounted for.
		var properties = TypeLibrary.GetPropertyDescriptions( PlanetManager );
		foreach ( PropertyDescription property in properties )
		{
			DisplayInfo display = property.GetDisplayInfo();
			Log.Info($"{property.Name}: {display.HasTag("Latitude Line")} - {display}"  );
			// filter only properties tagged with Latitude Line
			if ( !property.Tags.Contains("Latitude Line")) continue;
			
			Log.Info( property.Name );
			continue;
			// create lineconfig to populate fields with
			LineConfig lineConfig = new LineConfig
			{
				Name = property.Name,
				Value = property.GetValue(PlanetManager),
			};
			
			
			// compile a list of points for the sphere
			List<Vector3> localPoints = new List<Vector3>();
			for ( int i = 0; i < this._sphereLinePointCount; i++ )
			{
				float progress = (float)i / this._sphereLinePointCount;
				float angle = progress * MathF.PI * 2f;

				// Calculate local coordinates on the sphere surface
				float x = MathF.Cos( angle ) * _totalRadius;
				float y = MathF.Sin( angle ) * _totalRadius;
				float z = 0f;

				localPoints.Add(new Vector3( x, y, z ));
			}
		}
		
		
		/*foreach (var LineGameObject in this.SphereLineBucket.Children)
		{
			var Line =  LineGameObject.GetComponent<LineRenderer>();
			List<Vector3> localPoints = new List<Vector3>();
			for ( int i = 0; i < this._sphereLinePointCount; i++ )
			{
				float progress = (float)i / this._sphereLinePointCount;
				float angle = progress * MathF.PI * 2f;

				// Calculate local coordinates on the sphere surface
				float x = MathF.Cos( angle ) * _totalRadius;
				float y = MathF.Sin( angle ) * _totalRadius;
				float z = 0f;

				localPoints.Add(new Vector3( x, y, z ));
			}

			// Line.Points = localPoints;
		}*/
	}

	protected override void OnUpdate()
	{

	}
}
