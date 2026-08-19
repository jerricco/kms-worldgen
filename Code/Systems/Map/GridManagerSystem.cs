using System;
using Sandbox.GameData;
using Sandbox.Generation;

namespace Sandbox.Systems.Map;

public class GridManagerSystem: GameObjectSystem<GridManagerSystem>
{
	[Property] private PlanetManagerComponent PlanetManager { get; set; }

	// Using a power-of-two scale, we can define the exponential power value for each
	// of the available buckets and derive the rest of our Grid values from there.
	// fixed Steps.
	private int _tilePower     = 0;
	private int _chunkPower    = 4;
	private int _precinctPower = 6;
	private int _sectorPower   = 8;
	private int _cellPower     = 10;
	private int _segmentPower  = 11;
	// we only need these if the map is large enough to subdivide one.
	// dynamic Continental Steps -> steps which can govern a single continental structure.
	// @NOTE: This is all we'll test for now, since I should come up with a reverse order naming before generating upward.
	private int _octantPower = 12;
	private int _quadrantPower = 13;
	// the maximum available exponential power the game can handle. Technically, the game
	// can probably handle a lot less, but this is the maximum side-length of a map that can
	// fit into 64-bit unsigned integer space. Am I insane? Yes.
	// we'll at least bound it for now, but if I want to make things infinite (I probably will),
	// 9,223,372,036,854,775,808 should ultimately be the max allowable side-length
	private int _maxAllowedPower = 63;
	
	
	public GridManagerSystem( Scene scene ) : base( scene )
	{
	}
}
