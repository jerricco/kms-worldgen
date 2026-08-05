using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Triangulation;

[Category("Procedural Rendering")]
public sealed class ProceduralDelaunayRenderer : Component
{
	[Property] public Material MeshMaterial { get; set; }
	
	private SceneObject _sceneObject;
	
	protected override void OnStart()
	{
		if ( MeshMaterial == null )
		{
			MeshMaterial = Material.Load( "materials/dev/dev_unlit.vmat" );
		}
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

		var vertices = new List<SimpleVertex>();
		var indices = new List<int>();

		// get vertex buffers
		for ( int i = 0; i < delaunayData.Points.Length; i++ )
		{
			var p = delaunayData.Points[i];
			vertices.Add( new SimpleVertex 
			{
				position = new Vector3( (float)p.X, (float)p.Y, 0f ),
				normal = Vector3.Up,
				tangent = Vector3.Right,
				texcoord = new Vector2( (float)p.X / 512f, (float)p.Y / 512f ) // basic mapping uv coordinates
			});
		}
		Log.Info( $"ProceduralDelaunayRenderer: {vertices.Count} vertex buffers arranged." );
		// populate indicies
		for ( int i = 0; i < delaunayData.Triangles.Length; i++ ) indices.Add( (int)delaunayData.Triangles[i] );
		Log.Info( $"ProceduralDelaunayRenderer: {indices.Count} indices populated." );
		
		// get structural mesh layout
		var proceduralMesh = new Mesh( MeshMaterial );
		proceduralMesh.CreateVertexBuffer( vertices.Count, vertices.ToArray() );
		proceduralMesh.CreateIndexBuffer( indices.Count, indices.ToArray() );

		// Calculate total bounding volumes for hardware culling bounds protection
		BBox localBounds = BBox.FromPoints( vertices.Select( v => v.position ) );
		proceduralMesh.Bounds = localBounds;
		
		Log.Info( $"ProceduralDelaunayRenderer: Calculated BBox bounds" +
		          $" from x{(int)localBounds.Mins.x},y{(int)localBounds.Mins.y}" +
		          $" to x{(int)localBounds.Maxs.x},y{(int)localBounds.Maxs.y}" );
		
		var modelBuilder = new ModelBuilder();
		modelBuilder.AddMesh( proceduralMesh );
		Model compiledModel = modelBuilder.Create();

		// 5. Spawn the scene renderable object inside your SceneWorld context
		_sceneObject = new SceneObject( Scene.SceneWorld, compiledModel, new Transform( WorldPosition ) );
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
