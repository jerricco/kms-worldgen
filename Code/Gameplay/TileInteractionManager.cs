using Sandbox.Generator;
using System;
using Sandbox.Systems.Map;
using Sandbox.Generation;

namespace Sandbox.Gameplay;

public class TileInteractionManager : Component
{
	protected override void OnUpdate()
	{
		if ( MapGeneratorSystem.Current == null ) return;
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

			// get global tile space
			int globalTileX = (int)MathF.Floor( intersectPoint.x );
			int globalTileY = (int)MathF.Floor( intersectPoint.y );

			// get chunk space
			int chunkSize = MapGeneratorSystem.Current.Settings.ChunkGridSize; 
			int chunkX = (int)MathF.Floor( (float)globalTileX / chunkSize );
			int chunkY = (int)MathF.Floor( (float)globalTileY / chunkSize );

			// isolate coordinates relative to the specific chunk's origin bound edge
			int localTileX = globalTileX - (chunkX * chunkSize);
			int localTileY = globalTileY - (chunkY * chunkSize);

			// check the chunk & draw its tooltip if we can find it
			Chunk targetChunk = MapGeneratorSystem.Current.GetChunkAt( chunkX, chunkY );
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
	
	// @TODO: stop using Gizmo you goon 
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
