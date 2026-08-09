using Sandbox.Generation;
using System;

namespace Sandbox.Triangulation;

[Category("Procedural Rendering")]
public sealed class ProceduralDelaunayRenderer : Component
{
	[Property] public GenerationSettings Settings { get; set;  }
	[Property] public Material BaseMaterial { get; set; }
	[Property] public Color DefaultLineColor { get; set; } = Color.White;
	
	private SceneObject _sceneObject;
	private Model _compiledModel;
	private bool _enabled = true;
	
	[Property] public bool ShowDelaunay 
	{ 
		get => _enabled; 
		set 
		{
			if (_enabled == value) return;
			_enabled = value;
            
			if (_sceneObject != null) _sceneObject.RenderingEnabled = _enabled;
		}
	}

	protected override void OnStart()
	{
		if ( BaseMaterial == null )
			Log.Warning( "No custom material found for ProceduralDelaunayRenderer. Creating a material from materials/default/default_line.vmat" );
		
		BaseMaterial = BaseMaterial ?? Material.FromShader( "materials/default/default_line.vmat" );
	}
	
	/// <summary>
	/// Builds the debug Voronoi site mesh with supplied Delaunay triangulation data.
	/// </summary>
	public void RebuildMesh( Delaunator delaunayData )
	{
		Log.Info( "ProceduralDelaunayRenderer: Cleaning up Delaunay scene information" );
		_sceneObject?.Delete();
		_sceneObject = null;

		if ( delaunayData == null || delaunayData.Triangles.Length == 0 ) return;

		var vertices = new List<Vertex>();
		var indices = new List<int>();
		Vector3 worldCenter = Vector3.Zero;
		// Define our start and end points in Hue degrees (0.0 to 360.0)
		// Cyan sits around 180 degrees, and Red sits at 0 (or 360) degrees
		float startHue = 120f; 
		float endHue = 0f; // Driving upward to 360 shifts Cyan -> Blue -> Magenta -> Red
		
		for ( int i = 0; i < delaunayData.Points.Count; i++ )
		{
			// Lerp between GREEN -> RED as we move away from WorldCenter (0,0,0)
			var p = delaunayData.Points[i];
			Vector3 vertexPosition = new Vector3( (float)p.x, (float)p.y, 0f );
			float distanceToCenter = Vector3.DistanceBetween( vertexPosition, worldCenter );
			float t = Math.Clamp( distanceToCenter / Settings.MaxRadius, 0f, 1f );
			float currentHue = MathX.Lerp( startHue, endHue, t );
			ColorHsv hsvStruct = new ColorHsv( currentHue, 1f, 1f, 0.6f );
			Color rainbowColor = hsvStruct; // turn it back implicitly to Color
			
			vertices.Add( new Vertex 
			{
				Position = vertexPosition,
				Normal = Vector3.Up,
				Tangent = new Vector4( Vector3.Right, 1f ),
				// correct UV mapping to shift the negative bounds back into a 0.0 -> 1.0 space
				TexCoord0 = new Vector2( 
					((float)p.x + Settings.HalfWidth) / Settings.WorldWidth, 
					((float)p.y + Settings.HalfHeight) / Settings.WorldHeight 
				),
				Color = rainbowColor
			});
		}
		
		Log.Info( $"ProceduralDelaunayRenderer: {vertices.Count} SimpleVertex objects created" );

		// get independent line pairs
		for ( int i = 0; i < delaunayData.Triangles.Length; i += 3 )
		{
			int idxA = (int)delaunayData.Triangles[i];
			int idxB = (int)delaunayData.Triangles[i + 1];
			int idxC = (int)delaunayData.Triangles[i + 2];

			// Edge 1: A to B
			indices.Add( idxA );
			indices.Add( idxB );

			// Edge 2: B to C
			indices.Add( idxB );
			indices.Add( idxC );

			// Edge 3: C to A
			indices.Add( idxC );
			indices.Add( idxA );
		}
		
		Log.Info( $"ProceduralDelaunayRenderer: {indices.Count} edge line pairs created" );
		
		// create an independent material per-frame
		var proceduralMesh = new Mesh( BaseMaterial );
		proceduralMesh.CreateVertexBuffer( vertices.Count, vertices.ToArray() );
		proceduralMesh.CreateIndexBuffer( indices.Count, indices );
		proceduralMesh.PrimitiveType = MeshPrimitiveType.Lines;
		
		Log.Info( $"ProceduralDelaunayRenderer: Procedural mesh created" );

		// Calculate total bounding volumes for hardware culling bounds protection
		BBox localBounds = BBox.FromPoints( vertices.Select( v => v.Position ) );
		proceduralMesh.Bounds = localBounds;
		
		Log.Info( $"ProceduralDelaunayRenderer: Calculated BBox bounds" +
		          $" from x{(int)localBounds.Mins.x},y{(int)localBounds.Mins.y}" +
		          $" to x{(int)localBounds.Maxs.x},y{(int)localBounds.Maxs.y}" );
		
		var modelBuilder = new ModelBuilder();
		modelBuilder.AddMesh( proceduralMesh );
		_compiledModel = modelBuilder.Create();

		// spawn the scene renderable
		_sceneObject = new SceneObject( Scene.SceneWorld, _compiledModel, new Transform( WorldPosition ) );
		_sceneObject.ColorTint = Color.White; // Base tint - individual vertex colours should override here
		_sceneObject.Attributes.Set( "Layer", "Overlay" ); 
		_sceneObject.Attributes.Set( "depthtest", "none" );
		_sceneObject.Attributes.Set( "depthwrite", false );
		_sceneObject.Attributes.Set( "renderwithouteffects", true );
		
		Log.Info( "Mesh geometry created for voronoi information. Toggle 'Show Delaunay' to view" );
	}
	
	protected override void OnPreRender()
	{
		if ( _sceneObject == null ) return;

		// @TODO: ensure this works with the panning script.
		// Upate the object to translate with the WorldPosition which is altered when panning the camera.
		_sceneObject.Transform = new Transform( WorldPosition );
	}

	protected override void OnDestroy()
	{
		// Safe teardown when wiping objects or changing maps
		_sceneObject?.Delete();
		_sceneObject = null;
	}
}
