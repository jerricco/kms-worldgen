using Sandbox.Generator;
using System;
using Sandbox.Generation;

namespace Sandbox.Gameplay;

// @TODO: Orchestrate this to activating on-demand when tiles are geneated. 
public class TileInteractionManager : Component
{
	// @TODO: split this into the MapManager so that a dedicated component tracks live chunks
	[Property] public MapGenerator ActiveMapManager { get; set; }

	protected override void OnStart()
	{
		ActiveMapManager = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( ActiveMapManager == null ) return;
		HoverForTileTooltip();
	}

	private void HoverForTileTooltip()
	{
		Ray mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		Vector3 rayOrigin = mouseRay.Position;
		Vector3 rayDirection = mouseRay.Forward;
		
		if ( MathF.Abs( rayDirection.z ) <= 0.0001f ) return;
		
		// Distance along the ray to strike the flat ground plane
		float distanceToPlane = -rayOrigin.z / rayDirection.z;
		if ( distanceToPlane < 0f ) return;
		
			Vector3 intersectPoint = rayOrigin + (rayDirection * distanceToPlane);

			// Floor global positions directly into grid indices (1x1 unit sizing)
			int globalTileX = (int)MathF.Floor( intersectPoint.x );
			int globalTileY = (int)MathF.Floor( intersectPoint.y );

			// 5. Chunk space indexing breakdowns (Assuming 50x50 chunk dimensions)
			int chunkSize = ActiveMapManager.Settings.ChunkGridSize; 
			int chunkX = (int)MathF.Floor( (float)globalTileX / chunkSize );
			int chunkY = (int)MathF.Floor( (float)globalTileY / chunkSize );

			// Isolate coordinates relative to the specific chunk's origin bound edge
			int localTileX = globalTileX - (chunkX * chunkSize);
			int localTileY = globalTileY - (chunkY * chunkSize);

			// check the chunk & draw its tooltip if we can find it
			Chunk targetChunk = ActiveMapManager.GetChunkAt( chunkX, chunkY );
			if ( targetChunk != null && targetChunk.Tiles != null )
			{
				uint localIndex = targetChunk.LocalIndex( localTileX, localTileY );
				if ( localIndex < targetChunk.Tiles.Length )
				{
					TileData hoveredTile = targetChunk.Tiles[localIndex];
					
					// Render immediate UI tooltip onto the viewport surface
					DrawTileTooltip( hoveredTile, globalTileX, globalTileY, chunkX, chunkY );
				}
			}
	}
	
	/// <summary>
	/// Renders a dynamic debug text block directly over the mouse cursor positions.
	/// </summary>
	private void DrawTileTooltip( TileData tile, int gx, int gy, int cx, int cy )
	{
		string tooltipText = 
			$"[Tile Coordinates]\n" +
			$"Global: ({gx}, {gy})\n" +
			$"Chunk Space: ({cx}, {cy})\n\n" +
			$"[Tile Data]\n" +
			$"Elevation: {tile.Elevation:F2}\n" +
			$"Humidity: {tile.Humidity:F2}\n" +
			$"Temperature: {tile.Temperature:F2}\n" +
			$"Material ID: {tile.MaterialId}";

		// Draw immediate screen overlay text directly onto the active view window
		Gizmo.Draw.ScreenText( tooltipText, Mouse.Position + new Vector2( 20, 20 ), size: 14f );
	}
}
