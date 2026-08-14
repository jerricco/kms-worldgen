using System;
using Sandbox.Systems.Map;
using Sandbox.Generation;

namespace Sandbox.Gameplay;

public class TileInteractionManager : Component
{
	protected override void OnUpdate()
	{
		if (MapGeneratorSystem.Current == null)
		{
			return;
		}

		this.HoverForTileTooltip();
	}

	private void HoverForTileTooltip()
	{
		var mouseRay = this.Scene.Camera.ScreenPixelToRay(Mouse.Position);
		var rayOrigin = mouseRay.Position;
		var rayDirection = mouseRay.Forward;

		if (MathF.Abs(rayDirection.z) <= 0.0001f)
		{
			return;
		}

		// Distance along the ray to strike the flat ground plane
		var distanceToPlane = -rayOrigin.z / rayDirection.z;
		if (distanceToPlane < 0f)
		{
			return;
		}

		var intersectPoint = rayOrigin + rayDirection * distanceToPlane;

		// Floor global positions directly into grid indices (1x1 unit sizing)
		var globalTileX = (int)MathF.Floor(intersectPoint.x);
		var globalTileY = (int)MathF.Floor(intersectPoint.y);

		// 5. Chunk space indexing breakdowns (Assuming 50x50 chunk dimensions)
		var chunkSize = MapGeneratorSystem.Current.Settings.ChunkGridSize;
		var chunkX = (int)MathF.Floor((float)globalTileX / chunkSize);
		var chunkY = (int)MathF.Floor((float)globalTileY / chunkSize);

		// Isolate coordinates relative to the specific chunk's origin bound edge
		var localTileX = globalTileX - chunkX * chunkSize;
		var localTileY = globalTileY - chunkY * chunkSize;

		// check the chunk & draw its tooltip if we can find it
		var targetChunk = MapGeneratorSystem.Current.GetChunkAt(chunkX, chunkY);
		if (targetChunk != null)
		{
			var localIndex = targetChunk.LocalIndex(localTileX, localTileY);
			if (localIndex < targetChunk.Tiles.Length)
			{
				var hoveredTile = targetChunk.Tiles[localIndex];

				// Render immediate UI tooltip onto the viewport surface
				this.DrawTileTooltip(hoveredTile, globalTileX, globalTileY, chunkX, chunkY);
			}
		}
	}

	/// <summary>
	/// Renders a dynamic debug text block directly over the mouse cursor positions.
	/// </summary>
	private void DrawTileTooltip(TileData tile, int gx, int gy, int cx, int cy)
	{
		var tooltipText =
			$"[Tile Coordinates]\n" +
			$"Global: ({gx}, {gy})\n" +
			$"Chunk Space: ({cx}, {cy})\n\n" +
			$"[Tile Data]\n" +
			$"Elevation: {tile.Elevation:F2}\n" +
			$"Humidity: {tile.Humidity:F2}\n" +
			$"Temperature: {tile.Temperature:F2}\n" +
			$"Material ID: {tile.MaterialId}";

		// Draw immediate screen overlay text directly onto the active view window
		Gizmo.Draw.ScreenText(tooltipText, Mouse.Position + new Vector2(20, 20), size: 14f);
	}
}
