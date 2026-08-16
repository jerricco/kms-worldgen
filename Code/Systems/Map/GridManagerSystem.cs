using System;
using Sandbox.Generation;

namespace Sandbox.Systems.Map;

/*
 * Since the map is a large number of integers
 */
public class GridManagerSystem: GameObjectSystem<GridManagerSystem>
{
	private const int MapEdgeSize = 16384;
	private const int HalfMapEdge = MapEdgeSize / 2;
	private const int TotalTiles = MapEdgeSize * MapEdgeSize; 
	
	private const int OctantPerRow = 4;      // 16384 / 4096
	private const int CellsPerRow = 16;      // 16384 / 1024
	private const int SectorsPerRow = 64;    // 16384 / 256
	private const int PrecinctsPerRow = 256; // 16384 / 64
	private const int ChunksPerRow = 1024;   // 16384 / 16
	
	private TileData[] _tileArray;
	
	public GridManagerSystem( Scene scene ) : base( scene )
	{
		// _tileArray = new TileData[TotalTiles];
		Listen( Stage.StartUpdate, 0, OnSystemUpdate, "Spatial System Update" );
	}

	private void OnSystemUpdate()
	{
		// Place global batch updates or spatial management ticks here if required
	}
	
	/// <summary>
	/// Maps a signed (-8192 to 8191) coordinate to a positive index space.
	/// </summary>
	private bool TryGetUnsignedCoords(int signedX, int signedY, out int unsignedX, out int unsignedY)
	{
		unsignedX = signedX + HalfMapEdge;
		unsignedY = signedY + HalfMapEdge;

		return unsignedX >= 0 && unsignedX < MapEdgeSize && unsignedY >= 0 && unsignedY < MapEdgeSize;
	}
	
	/// <summary>
	/// Instantly updates a tile's data using flat 1D indexing.
	/// </summary>
	public void SetTile( int signedX, int signedY, TileData data )
	{
		if ( !TryGetUnsignedCoords( signedX, signedY, out int x, out int y ) ) return;

		int index = ( y << 14 ) + x;
		_tileArray[index] = data;
	}
	
	public TileData GetTile( int signedX, int signedY )
	{
		if ( !TryGetUnsignedCoords( signedX, signedY, out int x, out int y ) ) return default;

		return _tileArray[( y << 14 ) + x];
	}
	
	public TileTaxonomy GetTaxonomy( int signedX, int signedY )
	{
		if ( !TryGetUnsignedCoords( signedX, signedY, out int x, out int y ) ) return default;

		return new TileTaxonomy
		{
			Quadrant  = ((y >> 13) << 1) + (x >> 13),
			Octant    = ((y >> 12) * OctantPerRow) + (x >> 12),
			Cell      = ((y >> 10) * CellsPerRow) + (x >> 10),
			Sector    = ((y >> 8)  * SectorsPerRow) + (x >> 8),
			Precinct  = ((y >> 6)  * PrecinctsPerRow) + (x >> 6),
			Chunk     = ((y >> 4)  * ChunksPerRow) + (x >> 4),
			TileId    = (y << 14)  + x
		};
	}
	
	/// <summary>
	/// Returns the inclusive face bounds (min/max signed coordinates) for any container index at a given taxonomy tier.
	/// </summary>
	public bool GetTaxonomyBounds(TaxonomyLevel level, int containerIndex, out int minSignedX, out int minSignedY, out int maxSignedX, out int maxSignedY)
	{
		minSignedX = minSignedY = maxSignedX = maxSignedY = 0;
		int size = level switch
		{
			TaxonomyLevel.Quadrant => 8192,
			TaxonomyLevel.Octant => 4096,
			TaxonomyLevel.Cell => 1024,
			TaxonomyLevel.Sector => 256,
			TaxonomyLevel.Precinct => 64,
			TaxonomyLevel.Chunk => 16,
			_ => throw new ArgumentOutOfRangeException(nameof(level))
		};

		int perRow = MapEdgeSize / size;
		int cx = containerIndex % perRow;
		int cy = containerIndex / perRow;

		int unSignedMinX = cx * size;
		int unSignedMinY = cy * size;
		int unSignedMaxX = unSignedMinX + size - 1;
		int unSignedMaxY = unSignedMinY + size - 1;

		minSignedX = unSignedMinX - HalfMapEdge;
		minSignedY = unSignedMinY - HalfMapEdge;
		maxSignedX = unSignedMaxX - HalfMapEdge;
		maxSignedY = unSignedMaxY - HalfMapEdge;

		return true;
	}
	
	/// <summary>
	/// Iterates through and applies an action to all tiles within a specified taxonomic container bounds.
	/// </summary>
	public void ForEachInContainer(TaxonomyLevel level, int containerIndex, Action<int, int, TileData> action)
	{
		if (!GetTaxonomyBounds(level, containerIndex, out int minX, out int minY, out int maxX, out int maxY))
			return;

		for (int sy = minY; sy <= maxY; sy++)
		{
			for (int sx = minX; sx <= maxX; sx++)
			{
				if (TryGetUnsignedCoords(sx, sy, out int ux, out int uy))
				{
					int index = (uy << 14) + ux;
					action(sx, sy, _tileArray[index]);
				}
			}
		}
	}
	
	public struct TileTaxonomy
	{
		public int Quadrant;
		public int Octant;
		public int Cell;
		public int Sector;
		public int Precinct;
		public int Chunk;
		public int TileId;
	}
	
	public enum TaxonomyLevel
	{
		Quadrant,
		Octant,
		Cell,
		Sector,
		Precinct,
		Chunk
	}
}
