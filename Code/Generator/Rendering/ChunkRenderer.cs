using System;
using Sandbox.Generation;

namespace Sandbox.Generator.Rendering;

public class ChunkRenderer : Component
{
	[Property] public Material BaseMaterial { get; set; }
	[Property] public Color DefaultTileColor { get; set; } = Color.Magenta;
	
	private ModelRenderer _modelRenderer;
	
	protected override void OnStart()
	{
		_modelRenderer = Components.GetOrCreate<ModelRenderer>();
		BaseMaterial = BaseMaterial ?? Material.FromShader( "materials/dev/reflectivity_30.vmat" );
	}
	
	// @TODO: Create multiple sceneObjects which each have a different visualisation of the tiles.
	// This should eventually become the gameview layer system
	
	/// <summary>
	/// Call this method whenever the chunk data is ready or changes.
	/// </summary>
	public void RegenerateMesh(Chunk chunk, Material baseMaterial)
	{
		if ( chunk.Tiles.Length < 1 )
		{
			Log.Error( $"Chunk_{chunk.ChunkX}_{chunk.ChunkY}: Not enough tiles for rendering! Exiting..." );
			return;
		}
		
		if ( _modelRenderer == null )
			_modelRenderer = Components.GetOrCreate<ModelRenderer>();

		BaseMaterial = BaseMaterial ?? baseMaterial;
		var worldX = chunk.ChunkX * chunk.Size;
		var worldY = chunk.ChunkY * chunk.Size;
		// Put the chunk into it's place in the world
		WorldPosition = new Vector3( worldX, worldY, 0f );
		
		var vertices = new List<Vertex>( chunk.Size * chunk.Size * 4 );
		var indices = new List<int>( chunk.Size * chunk.Size * 6 );
		int vertexIndex = 0;

		for ( int y = 0; y < chunk.Size; y++ )
		{
			for ( int x = 0; x < chunk.Size; x++ )
			{
				uint tileIndex = chunk.LocalIndex(x, y);
				TileData tile = chunk.Tiles[tileIndex];

				// Calculate position based on grid coordinates and Elevation data
				float posX = x * chunk.Size + worldX;
				float posY = y * chunk.Size +  worldY;
				float posZ = 0f;
				Color tileColor = GetElevationColour( tile.Elevation );

				// Build a simple flat quad for each tile
				vertices.Add( new Vertex
				{
					Position = new Vector3( posX, posY, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 0f, 0f ),
					Color = tileColor
				});
				
				vertices.Add( new Vertex
				{
					Position = new Vector3( posX + chunk.Size, posY, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 1f, 0f ),
					Color = tileColor
				});
				
				vertices.Add( new Vertex
				{
					Position = new Vector3( posX + chunk.Size, posY + chunk.Size, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 1f, 1f ),
					Color = tileColor
				});
				
				vertices.Add( new Vertex
				{
					Position = new Vector3( posX, posY + chunk.Size, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 0f, 1f ),
					Color = tileColor
				});
				
				// Triangle 1
				indices.Add( vertexIndex );
				indices.Add( vertexIndex + 2 );
				indices.Add( vertexIndex + 1 );

				// Triangle 2
				indices.Add( vertexIndex );
				indices.Add( vertexIndex + 3 );
				indices.Add( vertexIndex + 2 );

				vertexIndex += 4;
			}
		}

		// Create and build the S&Box Mesh object
		var mesh = new Mesh( BaseMaterial );
		mesh.CreateVertexBuffer( vertices.Count, vertices.ToArray() );
		mesh.CreateIndexBuffer( indices.Count, indices.ToArray() );
		
		Vector3 minBounds = new Vector3( 0, 0, -10f );
		Vector3 maxBounds = new Vector3( chunk.Size, chunk.Size, 10f );
		mesh.Bounds = new BBox( minBounds, maxBounds );

		// Package the mesh into a Model and assign it to the renderer
		var model = Model.Builder
			.AddMesh( mesh )
			.Create();

		_modelRenderer.Model = model;
		Log.Info( $"Chunk_{chunk.ChunkX}_{chunk.ChunkY} has been attached to it's renderer!" );
	}

	private Color GetElevationColour( double elevation)
	{
		// If out of elevation bounds, express as the default Color
		if ( elevation < -1.0D || elevation > 1.0D ) return DefaultTileColor;
		
		// @TODO: fiddle here properly
		if ( elevation < 0.0 ) return Color.Blue;       // Water
		if ( elevation < 0.2 ) return Color.Yellow;     // Sand
		if ( elevation < 0.7 ) return Color.Green;      // Grass
		return Color.Gray; 
	} 
}
