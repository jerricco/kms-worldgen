using System;

namespace Sandbox.Unorganised;

public enum WorldDivision // The divisions of the world. Determined by overall World width
{
	World, Quadrant, Octant, Cell
}

public enum MapDivision
{
	Sector, Precinct, Chunk, Tile
}

public struct MapDivisionSides // struct holding every side division of a map based on its given size.
{
	// dynamic (based on given WorldSize)
	public readonly int World { get; }
	public readonly int Quadrant { get; }
	public readonly int Octant { get; }
	public readonly int Cell { get; }
	// fixed divisions - cannot go smaller than a Sector.
	public readonly int Sector = 256;
	public readonly int Precinct = 64;
	public readonly int Chunk = 16;
	public readonly int Tile = 1;

	public MapDivisionSides( int worldSize )
	{
		World = worldSize;
		Quadrant = worldSize / 2; // 2 quadrants per side for 4 total a side.
		Octant = worldSize / 4; // 4 octants per side for 16 total a sid.
		Cell = worldSize / 16; // 16 per side for 1024 total a side.

		if ( Cell <= Sector )
		{
			throw new ArgumentOutOfRangeException( nameof(worldSize), "World Size too small! Cells and Sectors intersect!" );
		}
	}
}


