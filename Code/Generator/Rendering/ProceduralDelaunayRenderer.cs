using Sandbox.Generation;
using System;
using Sandbox.GameData;

namespace Sandbox.Triangulation;

[Category("Procedural Rendering")]
public sealed class ProceduralDelaunayRenderer : Component
{
	[Property] public GenerationSettings Settings { get; set;  }
	public Material LineMaterial { get; set; }
	
	private ModelRenderer _modelRenderer;
	private bool _enabled = true;
	
	[Property] public bool ShowDelaunay 
	{ 
		get => _enabled; 
		set 
		{
			if (_enabled == value) return;
			_enabled = value;
            
			if (_modelRenderer != null) _modelRenderer.Enabled = _enabled;
		}
	}
	
	protected override void OnDestroy()
	{
		ClearMesh();
		if (_modelRenderer != null && _modelRenderer.IsValid) _modelRenderer.Destroy();
	}
	
	/// <summary>
	/// Builds the debug Voronoi site mesh with supplied Delaunay triangulation data.
	/// </summary>
	public void RebuildMesh( Delaunator delaunayData )
	{
		Log.Info( "ProceduralDelaunayRenderer: Rebuilding Mesh..." );
		
		LineMaterial = LineMaterial ?? Material.FromShader( "materials/opaque_line.vmat" );
		_modelRenderer = GameObject.GetOrAddComponent<ModelRenderer>();
		// if our data is empty when this is called, disable the modelRenderer and leave
		if ( delaunayData == null || delaunayData.Triangles.Length == 0 )
		{
			ClearMesh();
			return;
		} 
		
		// enable if we still need it & in case it was turned off prior
		if ( !_modelRenderer.Enabled )  _modelRenderer.Enabled = true;

		var vertices = new List<Vertex>();
		var indices = new List<int>();
		Vector3 worldCenter = Vector3.Zero;
		// Define our start and end points in Hue degrees (0.0 to 360.0)
		float startHue = 120f; 
		float endHue = 0f;
		
		// 1. Build normal vertex structures (just single points, no quad math required)
		for ( int i = 0; i < delaunayData.Points.Count; i++ )
		{
			var p = delaunayData.Points[i];
			Vector3 vertexPosition = new Vector3( p.x, p.y, 1f );
			float distanceToCenter = Vector3.DistanceBetween( vertexPosition, worldCenter );
			float t = Math.Clamp( distanceToCenter / Settings.MaxRadius, 0f, 1f );
			float currentHue = MathX.Lerp( startHue, endHue, t );
			Color rainbowColor = new ColorHsv( currentHue, 1f, 1f, 0.6f );
		
			vertices.Add( new Vertex 
			{
				Position = vertexPosition,
				Normal = Vector3.Up,
				Tangent = new Vector4( Vector3.Right, 1f ),
				TexCoord0 = new Vector2( 0f, 0f ),
				Color = rainbowColor
			});
		}

		// Track edges we've already drawn so we don't duplicate math
		HashSet<(int, int)> processedEdges = new HashSet<(int, int)>();
		void TryAddLineIndexPair(int aIdx, int bIdx)
		{
			var edgeKey = aIdx < bIdx ? (aIdx, bIdx) : (bIdx, aIdx);
			if (processedEdges.Contains(edgeKey)) return;
			processedEdges.Add(edgeKey);

			indices.Add( aIdx );
			indices.Add( bIdx );
		}
		
		for ( int i = 0; i < delaunayData.Triangles.Length; i += 3 )
		{
			int idxA = delaunayData.Triangles[i];
			int idxB = delaunayData.Triangles[i + 1];
			int idxC = delaunayData.Triangles[i + 2];

			TryAddLineIndexPair(idxA, idxB);
			TryAddLineIndexPair(idxB, idxC);
			TryAddLineIndexPair(idxC, idxA);
		}
		
		// create bounding box
		BBox localBounds = BBox.FromPoints( vertices.Select( v => v.Position ) );
		Log.Info( $"ProceduralDelaunayRenderer: Calculated BBox bounds" +
		          $" from x{(int)localBounds.Mins.x},y{(int)localBounds.Mins.y}" +
		          $" to x{(int)localBounds.Maxs.x},y{(int)localBounds.Maxs.y}" +
		          $" with a height of {(int)(localBounds.Maxs.z - localBounds.Mins.z)}" );
		
		// create mesh
		Mesh mesh = new Mesh( LineMaterial );
		mesh.CreateVertexBuffer( vertices.Count, vertices.ToArray() );
		mesh.CreateIndexBuffer( indices.Count, indices.ToArray() );
		mesh.PrimitiveType = MeshPrimitiveType.Lines; // LineStrip?
		mesh.Bounds = localBounds;
		
		// compile its runtime model
		var model = new ModelBuilder()
			.AddMesh(mesh)
			.WithViewBounds(localBounds)
			.Create();

		_modelRenderer.Model = model;
		
		Log.Info( "ProceduralDelaunayRenderer: Mesh geometry created for voronoi information. Toggle 'Show Delaunay' to view" );
	}

	public void ClearMesh()
	{
		if (_modelRenderer == null) return;
		
		if ( _modelRenderer.IsValid() )
		{
			_modelRenderer.Model = null;
			_modelRenderer.Enabled = false;
		}
	}
}
