using System;

namespace Sandbox.Generation;

public class Tile
{
	public GenerationSettings Settings;
	
	public (int x, int y) Position;
	
	// speedy immediate-y chunk data
	public double Elevation;
	public int RegionId;
	public double Humidity;
	public double Temperature;
	public int MaterialId;
	
	private MapGenerator _generator;

	// tile data for later generation passes
	public SubterraneanLayer Geology;
	
	public Tile( int globalX, int globalY, GenerationSettings settings, MapGenerator generator)
	{
		_generator = generator;
		Settings = settings; 
		Position = (globalX, globalY);
		Elevation = GetElevation(globalX, globalY);
		Geology = BuildGeology(globalX, globalY, Elevation);
	}

	/**
     * PASS 1: ELEVATION EVALUATION
     * Determine the seeded elevation value for the tile using the provided
	 * Voronoi field structure along with given OpenSimplexNoise
    */
	private double GetElevation( int x, int y )
	{
		// clamp inside world
		int globalX = Math.Max( -Settings.HalfWidth, Math.Min(Settings.HalfWidth, x) );
		int globalY = Math.Max( -Settings.HalfHeight, Math.Min(Settings.HalfHeight, y) );

		double elevation;
		
		List<( VoronoiSite site, double distance)> candidates = GetVoronoiSiteCandidates( globalX, globalY );

		elevation = BuildTectonicSuperstructure(globalX, globalY);
		elevation = BuildNoisyGeologicalDetail(elevation);
		
		// return clamped elevation
		return Math.Max( -1.0d, Math.Min( 1.0d, elevation ) );
	}

	private List<( VoronoiSite site, double distance)> GetVoronoiSiteCandidates( int x, int y )
	{
		Vector2 globalPosition = new Vector2( x, y );

		Delaunay.Triangle site0 = _generator.Voronoi.DelaunayMesh.Find( triangle
			=> triangle.ContainsPoint( globalPosition ) );
		
		// @TODO: continue from here
		Log.Info(site0  );

		/*if ( site0 == null ) // Give a floored out voronoi site
		{
			site0 = new VoronoiSite( 0, );
			return (, 0.0d)
		}*/

		return new List<( VoronoiSite site, double distance)>();

		/*List<Delaunay.Triangle> neighbors = _generator.Voronoi.DelaunayMesh.FindAll( t
			=> t.IsNeighborOf( site0 ));*/
	}

	private double BuildTectonicSuperstructure( int x, int y )
	{
		return 0.0d; // @TODO: Does nothing for now.
	}

	private double BuildNoisyGeologicalDetail( double elevation )
	{
		return elevation; // @TODO: Does nothing for now.
	}

	/**
     * PASS 2: GEOLOGICAL EVALUATION
     * Find the geological structure of the space underneath the tile, so we know
	 * how to later fill it with biome data.
    */
	private SubterraneanLayer BuildGeology( int x, int y, double elevation )
	{
		return new SubterraneanLayer( 0, 0, BasementRockType.Basalt ); // @TODO: Does nothing for now
	}

	/**
     * PASS X: DETERMINE TILE REGION
     * Use the determinate data of a tile to find what region it belongs to.
	 * @NOTE: currently only accepts elevation data.
    */
	private RegionId GetRegion( int elevation )
	{
		return Generation.RegionId.Unassigned; // @TODO: Does nothing for now.
	}
}
