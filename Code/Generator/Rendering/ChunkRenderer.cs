using System;
using Sandbox.Generation;

namespace Sandbox.Generator.Rendering;

public class ChunkRenderer : Component
{
	
	[Property] public GenerationSettings Settings { get; set; }
	[Property] public ChunkTheme Theme { get; set; }
	[Property] public Color DefaultTileColor { get; set; } = Color.Magenta;
	
	private ModelRenderer _modelRenderer;
	
	protected override void OnStart()
	{
		_modelRenderer = Components.GetOrCreate<ModelRenderer>();
	}
	
	// @TODO: Create multiple sceneObjects which each have a different visualisation of the tiles.
	// This should eventually become the gameview layer system
	
	/// <summary>
	/// Call this method whenever the chunk data is ready or changes.
	/// </summary>
	public void RegenerateMesh(Chunk chunk, Material baseMaterial, GenerationSettings settings, ChunkTheme theme)
	{
		if ( chunk.Tiles.Length < 1 )
		{
			Log.Error( $"Chunk_{chunk.Position.x}_{chunk.Position.y}: Not enough tiles for rendering! Exiting..." );
			return;
		}
		
		// @TODO get chunk container GO and fill that with the chunks. We'll use that GO to manage live chunk state
		if ( _modelRenderer == null )
			_modelRenderer = Components.GetOrCreate<ModelRenderer>();

		if ( Settings == null ) Settings = settings;
		if ( Theme == null ) Theme = theme;
		
		var meshMaterial = baseMaterial ?? Material.Load( "materials/tile_unlit.vmat" );
		var worldX = chunk.Position.x * chunk.Size;
		var worldY = chunk.Position.y * chunk.Size;
		// Put the chunk into it's place in the world
		WorldPosition = new Vector3( worldX, worldY, 0f );
		
		var vertices = new List<Vertex>( chunk.Size * chunk.Size * 4 );
		var indices = new List<int>( chunk.Size * chunk.Size * 6 );
		int vertexIndex = 0;
		bool[] visited = new bool[chunk.Size * chunk.Size];

		for ( int y = 0; y < chunk.Size; y++ )
		{
			for ( int x = 0; x < chunk.Size; x++ )
			{
				uint tileIndex = chunk.LocalIndex(x, y);
				if ( visited[tileIndex] ) continue;
			
				TileData tile = chunk.Tiles[tileIndex];
				Color tileColor = GetElevationColour( tile.Elevation );
				
				// get max width of all identical adjacent tiles
				int width = 1;
				while ( x + width < chunk.Size )
				{
					uint nextXIndex = chunk.LocalIndex(x + width, y);
					if ( visited[nextXIndex] ) break;

					TileData nextTile = chunk.Tiles[nextXIndex];
					if ( GetElevationColour( nextTile.Elevation ) != tileColor ) break;

					width++;
				}
				
				// get max height of all identical adjacent tiles
				int height = 1;
				while ( y + height < chunk.Size )
				{
					bool rowMatches = true;
					for ( int rX = 0; rX < width; rX++ )
					{
						uint nextYIndex = chunk.LocalIndex(x + rX, y + height);
						if ( visited[nextYIndex] )
						{
							rowMatches = false;
							break;
						}

						TileData nextTile = chunk.Tiles[nextYIndex];
						if ( GetElevationColour( nextTile.Elevation ) != tileColor )
						{
							rowMatches = false;
							break;
						}
					}

					if ( !rowMatches ) break;
					height++;
				}
				
				// mark visited tiles
				for ( int h = 0; h < height; h++ )
				{
					for ( int w = 0; w < width; w++ )
					{
						uint visitedIndex = chunk.LocalIndex(x + w, y + h);
						visited[visitedIndex] = true;
					}
				}
				
				// Calculate real edge stretches
				float endX = x + width;
				float endY = y + height;
				
				// Build a simple flat quad for each tile
				vertices.Add( new Vertex
				{
					Position = new Vector3( x, y, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 0f, 0f ),
					Color = tileColor
				});
				
				vertices.Add( new Vertex
				{
					Position = new Vector3( endX, y, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 1f, 0f ),
					Color = tileColor
				});
				
				vertices.Add( new Vertex
				{
					Position = new Vector3( endX, endY, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 1f, 1f ),
					Color = tileColor
				});
				
				vertices.Add( new Vertex
				{
					Position = new Vector3( x, endY, 0f ),
					Normal = Vector3.Up,
					Tangent = new Vector4(Vector3.Right, 1f),
					TexCoord0 = new Vector2( 0f, 1f ),
					Color = tileColor
				});
				
				// Triangle 1
				indices.Add( vertexIndex );
				indices.Add( vertexIndex + 1 );
				indices.Add( vertexIndex + 2 );

				// Triangle 2
				indices.Add( vertexIndex );
				indices.Add( vertexIndex + 2 );
				indices.Add( vertexIndex + 3 );

				vertexIndex += 4;
			}
		}

		// Create and build the S&Box Mesh object
		var mesh = new Mesh( meshMaterial );
		mesh.CreateVertexBuffer( vertices.Count, vertices.ToArray() );
		mesh.CreateIndexBuffer( indices.Count, indices.ToArray() );
		
		// create tall bounding box. though the chunks are currently 2D, they should contain
		// the vertical height of everything in them.
		Vector3 minBounds = new Vector3( 0f, 0f, -256f );
		Vector3 maxBounds = new Vector3( chunk.Size, chunk.Size, 256f );
		mesh.Bounds = new BBox( minBounds, maxBounds );

		// Package the mesh into a Model and assign it to the renderer
		var model = Model.Builder
			.AddMesh( mesh )
			.Create();

		_modelRenderer.Model = model;
		_modelRenderer.Enabled = true;
		
		Log.Info( $"Chunk_{chunk.Position.x}_{chunk.Position.y} has been attached to it's renderer!" );
	}

	private Color GetElevationColour( double elevation )
	{
		// If out of elevation bounds, express as the default Color
		if ( elevation < -1.0D || elevation > 1.0D ) return DefaultTileColor;
		
		// @TODO: fiddle here more
		if ( elevation == Settings.AbyssalLevel ) return Theme.Void;
		if ( elevation < Settings.TrenchLevel ) return Theme.CrustFloor;
		if ( elevation < Settings.DeepOceanLevel ) return Theme.AbyssalOcean;
		if ( elevation < Settings.OceanLevel ) return Theme.DeepOcean;
		if ( elevation < Settings.SeaLevel ) return Theme.Ocean;
		if ( elevation < Settings.BeachLevel ) return Theme.Sea;
		if ( elevation < Settings.PlainLevel ) return Theme.Beach;
		if ( elevation < Settings.HillLevel ) return Theme.Plain;
		if ( elevation < Settings.MountainLevel ) return Theme.Hill;
		if ( elevation < Settings.PeakLevel ) return Theme.Mountain;
		return Theme.Peak; 
	} 
}
