using System;
using Sandbox.GameData;

namespace Sandbox.Triangulation;

[Category("Procedural Rendering")]
public sealed class ProceduralDelaunayRenderer : Component
{
	private bool _enabled = true;
	private SceneObject? _sceneObject;
    private ModelRenderer _modelRenderer;

    [Property] public GenerationSettings Settings { get; set;  }
    public Material LineMaterial { get; set; }

	[Property] public bool ShowDelaunay
	{
		get => this._enabled;
		set
        {
            if (this._enabled == value) return;
			this._enabled = value;

			if (this._modelRenderer != null)
			{
				this._modelRenderer.Enabled = this._enabled;
			}
		}
	}

	protected override void OnDestroy()
	{
		this.ClearMesh();
		if (this._modelRenderer != null && this._modelRenderer.IsValid) this._modelRenderer.Destroy();
	}

	/// <summary>
	/// Builds the debug Voronoi site mesh with supplied Delaunay triangulation data.
	/// </summary>
	public void RebuildMesh(Delaunator delaunayData)
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
		var worldCenter = Vector3.Zero;

		// Define our start and end points in Hue degrees (0.0 to 360.0)
		var startHue = 120f;
		var endHue = 0f;

		// 1. Build normal vertex structures (just single points, no quad math required)
		for (var i = 0; i < delaunayData.Points.Count; i++)
		{
			var p = delaunayData.Points[i];
			var vertexPosition = new Vector3(p.x, p.y, 1f);
			var distanceToCenter = Vector3.DistanceBetween(vertexPosition, worldCenter);
			var t = Math.Clamp(distanceToCenter / this.Settings.MaxRadius, 0f, 1f);
			var currentHue = MathX.Lerp(startHue, endHue, t);
			Color rainbowColor = new ColorHsv(currentHue, 1f, 1f, 0.6f);

			vertices.Add(
                new Vertex
			    {
				    Position = vertexPosition,
				    Normal = Vector3.Up,
				    Tangent = new Vector4(Vector3.Right, 1f),
				    TexCoord0 = new Vector2(0f, 0f),
				    Color = rainbowColor,
			    }
            );
		}

		// Track edges we've already drawn so we don't duplicate math
		var processedEdges = new HashSet<(int, int)>();
		void TryAddLineIndexPair(int aIdx, int bIdx)
		{
			var edgeKey = aIdx < bIdx ? (aIdx, bIdx) : (bIdx, aIdx);
			if (processedEdges.Contains(edgeKey)) return;
			processedEdges.Add(edgeKey);

			indices.Add( aIdx );
			indices.Add( bIdx );
		}

		for (var i = 0; i < delaunayData.Triangles.Length; i += 3)
		{
			var idxA = delaunayData.Triangles[i];
			var idxB = delaunayData.Triangles[i + 1];
			var idxC = delaunayData.Triangles[i + 2];

			TryAddLineIndexPair(idxA,idxB);
			TryAddLineIndexPair(idxB,idxC);

			TryAddLineIndexPair(idxC,idxA);
		}

		// create bounding box
		var localBounds = BBox.FromPoints(vertices.Select(v => v.Position));
		Log.Info(
            $"ProceduralDelaunayRenderer: Calculated BBox bounds" +
		    $" from x{(int)localBounds.Mins.x},y{(int)localBounds.Mins.y}" +
		    $" to x{(int)localBounds.Maxs.x},y{(int)localBounds.Maxs.y}" +
		    $" with a height of {(int)(localBounds.Maxs.z - localBounds.Mins.z)}"
        );

		// create mesh
		var mesh = new Mesh(this.LineMaterial);
		mesh.CreateVertexBuffer(vertices.Count, vertices.ToArray());
		mesh.CreateIndexBuffer(indices.Count, indices.ToArray());
		mesh.PrimitiveType = MeshPrimitiveType.Lines; // LineStrip?
		mesh.Bounds = localBounds;

		// compile its runtime model
		var model = new ModelBuilder()
			.AddMesh(mesh)
			.WithViewBounds(localBounds)
			.Create();

        this._modelRenderer.Model = model;

		Log.Info("ProceduralDelaunayRenderer: Mesh geometry created for voronoi information. Toggle 'Show Delaunay' to view");
	}

	public void ClearMesh()
	{
		if (this._modelRenderer == null) return;

		if (this._modelRenderer.IsValid())
		{
            this._modelRenderer.Model = null;
            this._modelRenderer.Enabled = false;
		}
	}
}
