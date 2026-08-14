using System;
using Sandbox.GameData;

namespace Sandbox.Triangulation;

[Category("Procedural Rendering")]
public sealed class ProceduralDelaunayRenderer : Component
{
	private Model _compiledModel;
	private bool _enabled = true;

	private SceneObject? _sceneObject;
	[Property] public GenerationSettings Settings { get; set; }
	[Property] public Material? BaseMaterial { get; set; }
	[Property] public Color DefaultLineColor { get; set; } = Color.White;

	[Property] public bool ShowDelaunay
	{
		get => this._enabled;
		set
		{
			if (this._enabled == value)
			{
				return;
			}

			this._enabled = value;

			if (this._sceneObject != null)
			{
				this._sceneObject.RenderingEnabled = this._enabled;
			}
		}
	}

	protected override void OnStart()
	{
		if (this.BaseMaterial == null)
		{
			Log.Warning("No custom material found for ProceduralDelaunayRenderer. Creating a material from materials/default/default_line.vmat");
		}

		this.BaseMaterial = this.BaseMaterial ?? Material.FromShader("materials/default/default_line.vmat");
	}

	/// <summary>
	/// Builds the debug Voronoi site mesh with supplied Delaunay triangulation data.
	/// </summary>
	public void RebuildMesh(Delaunator delaunayData)
	{
		Log.Info("ProceduralDelaunayRenderer: Cleaning up Delaunay scene information");
		this._sceneObject?.Delete();
		this._sceneObject = null;

		if (delaunayData == null || delaunayData.Triangles.Length == 0)
		{
			return;
		}

		var vertices = new List<Vertex>();
		var indices = new List<int>();
		var worldCenter = Vector3.Zero;
		// Define our start and end points in Hue degrees (0.0 to 360.0)
		// Cyan sits around 180 degrees, and Red sits at 0 (or 360) degrees
		var startHue = 120f;
		var endHue = 0f;// Driving upward to 360 shifts Cyan -> Blue -> Magenta -> Red

		for (var i = 0; i < delaunayData.Points.Count; i++)
		{
			// Lerp between GREEN -> RED as we move away from WorldCenter (0,0,0)
			var p = delaunayData.Points[i];
			var vertexPosition = new Vector3(p.x, p.y, 0f);
			var distanceToCenter = Vector3.DistanceBetween(vertexPosition, worldCenter);
			var t = Math.Clamp(distanceToCenter / this.Settings.MaxRadius, 0f, 1f);
			var currentHue = MathX.Lerp(startHue, endHue, t);
			var hsvStruct = new ColorHsv(currentHue, 1f, 1f, 0.6f);
			Color rainbowColor = hsvStruct;// turn it back implicitly to Color

			vertices.Add(
				new Vertex
				{
					Position = vertexPosition,
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					// correct UV mapping to shift the negative bounds back into a 0.0 -> 1.0 space
					TexCoord0 = new Vector2(
						(p.x + this.Settings.HalfWidth) / this.Settings.WorldWidth,
						(p.y + this.Settings.HalfHeight) / this.Settings.WorldHeight
					),
					Color = rainbowColor,
				}
			);
		}

		Log.Info($"ProceduralDelaunayRenderer: {vertices.Count} SimpleVertex objects created");

		// get independent line pairs
		for (var i = 0; i < delaunayData.Triangles.Length; i += 3)
		{
			var idxA = delaunayData.Triangles[i];
			var idxB = delaunayData.Triangles[i + 1];
			var idxC = delaunayData.Triangles[i + 2];

			// Edge 1: A to B
			indices.Add(idxA);
			indices.Add(idxB);

			// Edge 2: B to C
			indices.Add(idxB);
			indices.Add(idxC);

			// Edge 3: C to A
			indices.Add(idxC);
			indices.Add(idxA);
		}

		Log.Info($"ProceduralDelaunayRenderer: {indices.Count} edge line pairs created");

		// create an independent material per-frame
		var proceduralMesh = new Mesh(this.BaseMaterial);
		proceduralMesh.CreateVertexBuffer(vertices.Count, vertices.ToArray());
		proceduralMesh.CreateIndexBuffer(indices.Count, indices);
		proceduralMesh.PrimitiveType = MeshPrimitiveType.Lines;

		Log.Info($"ProceduralDelaunayRenderer: Procedural mesh created");

		// Calculate total bounding volumes for hardware culling bounds protection
		var localBounds = BBox.FromPoints(vertices.Select(v => v.Position));
		proceduralMesh.Bounds = localBounds;

		Log.Info(
			$"ProceduralDelaunayRenderer: Calculated BBox bounds" +
			$" from x{(int)localBounds.Mins.x},y{(int)localBounds.Mins.y}" +
			$" to x{(int)localBounds.Maxs.x},y{(int)localBounds.Maxs.y}"
		);

		var modelBuilder = new ModelBuilder();
		modelBuilder.AddMesh(proceduralMesh);
		this._compiledModel = modelBuilder.Create();

		// spawn the scene renderable
		if (this._sceneObject?.IsValid() != true)
		{
			this._sceneObject = new SceneObject(this.Scene.SceneWorld, this._compiledModel, new Transform(this.WorldPosition));
			this._sceneObject.ColorTint = Color.White;// Base tint - individual vertex colours should override here
			this._sceneObject.Attributes.Set("Layer", "Overlay");
			this._sceneObject.Attributes.Set("depthtest", "none");
			this._sceneObject.Attributes.Set("depthwrite", false);
			this._sceneObject.Attributes.Set("renderwithouteffects", true);
		}

		Log.Info("Mesh geometry created for voronoi information. Toggle 'Show Delaunay' to view");
	}

	protected override void OnPreRender()
	{
		if (this._sceneObject == null)
		{
			return;
		}

		// Upate the object to translate with the WorldPosition which is altered when panning the camera.
		this._sceneObject.Transform = new Transform(this.WorldPosition);
	}

	protected override void OnDestroy()
	{
		// Safe teardown when wiping objects or changing maps
		this._sceneObject?.Delete();
		this._sceneObject = null;
	}
}
