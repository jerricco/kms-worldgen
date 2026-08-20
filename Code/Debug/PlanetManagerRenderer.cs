using Sandbox;
using System;
using System.Reflection;
using Sandbox.GameData;
using Sandbox.Systems.Map;

public sealed class PlanetManagerRenderer : Component
{
	// should always be attached
	[Property] private PlanetManagerComponent PlanetManager { get; set; }
	

	// planet models
	[Property] private ModelRenderer Sphere { get; set; }
	[Property] private GameObject SphereLineBucket { get; set; }
	private float _sphereRadius { get; set; }
	private int _sphereLinePointCount = 50;
	private float _totalRadius => _sphereRadius + _surfaceOffset;
	
	[Property] private ModelRenderer Plane { get; set; }
	[Property] private GameObject PlaneLineBucket { get; set; }
	
	// general
	private float _surfaceOffset = 5f;
	
	// storage for special values
	private float _horizontalClimateOffset { get; set; }
	
	// comparation 
	private string[] _allowedSingleLatitudeLines = ["Equator", "TropicLine", "ArcticLine", "ArcticLine", "BorealHellLine", "ScorchLine"];

	private struct LineConfig
	{
		public string Name { get; set; }
		public object Value { get; set; }
		public Color LineColor { get; set; }
		public GameObject SphereLineGo { get; set; }
		public GameObject PlaneLineGo { get; set; }
	}

	private Dictionary<string, LineConfig> ConfigLines = new();
	
	protected override void OnStart()
	{
		// get some useful stuff
		this._sphereRadius = this.Sphere.Bounds.Size.x / 2;
		this._horizontalClimateOffset = PlanetManager.HorizontalClimateOffset;
		
		//populate the equator
		PopulateConfigLines(new  LineConfig
		{
			Name = "Equator",
			LineColor = Color.White,
			Value = 0f,
		});
		
		// loop over the config items and ensure they are populated into ConfigLines.
		// this will need to ensure any specific rules other than equatorial distance are accounted for.
		var properties = TypeLibrary.GetPropertyDescriptions( PlanetManager );
		foreach ( PropertyDescription property in properties )
		{
			LineConfig lineConfig = new LineConfig
			{
				Name = property.Name,
				Value = property.GetValue(PlanetManager),
				LineColor = new Color(
					MapGeneratorSystem.Current.Rng.NextRange( 0, 255 ),
					MapGeneratorSystem.Current.Rng.NextRange( 0, 255 ),
					MapGeneratorSystem.Current.Rng.NextRange( 0, 255 )),
			};
			
			PopulateConfigLines( lineConfig );
		}
	}

	protected override void OnUpdate()
	{

	}

	private void PopulateConfigLines(LineConfig lineConfig)
	{
		if ( !_allowedSingleLatitudeLines.Contains( lineConfig.Name ) ) return;
		
		// create gameobject
		lineConfig.SphereLineGo = new GameObject(lineConfig.Name);
		lineConfig.SphereLineGo.SetParent(SphereLineBucket);
		lineConfig.SphereLineGo.LocalPosition = Vector3.Zero;
		
		// get the height (and resulting new radius) of the configured value
		Log.Info($"{lineConfig.Name}: {lineConfig.Value}"  );
		float latitudeAngle = (float)lineConfig.Value * (MathF.PI / 2f);
		float z = _totalRadius * (float)lineConfig.Value;
		float circleRadius = MathF.Sqrt((_totalRadius * _totalRadius) - (z * z));
		
		// create lineconfig to populate fields with
		// compile a list of points for the sphere
		List<Vector3> localPoints = new List<Vector3>();
		for ( int i = 0; i < this._sphereLinePointCount; i++ )
		{
			float progress = (float)i / this._sphereLinePointCount;
			float angle = progress * MathF.PI * 2f;

			// Calculate local coordinates on the sphere surface
			float x = MathF.Cos( angle ) * circleRadius;
			float y = MathF.Sin( angle ) * circleRadius;
			
			localPoints.Add(lineConfig.SphereLineGo.Transform.World.PointToWorld(new Vector3( x, y, z )));
		}
		
		// attach points
		LineRenderer line = lineConfig.SphereLineGo.AddComponent<LineRenderer>();
		line.CastShadows = false;
		line.Width = 1;
		line.Color = new Gradient(lineConfig.LineColor);
		line.UseVectorPoints = true;
		line.VectorPoints = localPoints;
	}
}
