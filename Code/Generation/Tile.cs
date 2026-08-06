using System;

namespace Sandbox.Generation;

public struct VoronoiResult
{
	public VoronoiSite Site;
	public float DistanceSq;
}

public struct DelaunayNeighbors
{
	public VoronoiResult Candidate0;
	public VoronoiResult Candidate1;
	public VoronoiResult Candidate2;
	public int Count;
}

[Category("Procedural Generation")]
public class Tile
{
	public GenerationSettings Settings;
	
	public (int x, int y) Position;
	
	// speedy immediate-y chunky data
	public double Elevation;
	public RegionId RegionId;
	public double Humidity;
	public double Temperature;
	public int MaterialId; // @TODO: Type as MaterialId
	// tile data for later generation passes
	public SubterraneanLayer Geology;
	
	private MapGenerator _generator;
	private DelaunayNeighbors _neighbors;
	
	public Tile( int globalX, int globalY, GenerationSettings settings, MapGenerator generator)
	{
		_generator = generator;
		Settings = settings; 
		Position = (globalX, globalY);
		Elevation = GetElevation(globalX, globalY);
		RegionId = GetRegion( Elevation );
		Humidity = 0; // @TODO: Does nothing for now
		MaterialId = 0; // @TODO: Does nothing for now
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

		double elevation; // init the elevation -> should never reach the end null
		
		// get neighboring candidates to determine what the tectonic structure looks like for this Tile
		// since we want to be aware of neighboring cells to avoid cell-pillowing in the generated areas.
		_neighbors = GetVoronoiSiteCandidates( globalX, globalY );

		// Select from the appropriate site structure
		if ( _neighbors.Count == 0 )
			elevation = Settings.AbyssalLevel; // fallback
		else if ( _neighbors.Count < 2 )
			elevation = _neighbors.Candidate2.Site.BaseElevation;
		else
			elevation = BuildTectonicSuperstructure(globalX, globalY);

		// add noisy detail
		elevation = BuildNoisyGeologicalDetail(elevation);
		
		// return clamped elevation
		return Math.Max( -1.0d, Math.Min( 1.0d, elevation ) );
	}

	private DelaunayNeighbors GetVoronoiSiteCandidates( int x, int y )
	{
		// clear old candidates
		DelaunayNeighbors candidates = default;
		Vector2 globalPosition = new Vector2( x, y );
		
		int site0Id = _generator.Voronoi.FindClosestPointIndex( globalPosition );
		VoronoiSite site0 = _generator.Voronoi.VoronoiSites[site0Id];
		if ( site0 == null ) return candidates;
		
		// 1. Calculate Site 0
		float dx0 = x - site0.Position.x;
		float dy0 = y - site0.Position.y;
		candidates.Candidate0 = new VoronoiResult { Site = site0, DistanceSq = (dx0 * dx0) + (dy0 * dy0) };
		candidates.Count = 1;
		
		var neighborIds = _generator.Voronoi.GetNeighbors( site0Id );
		int neighborCount = neighborIds.Count;
		for ( int i = 0; i < neighborCount; i++ )
		{
			if ( candidates.Count >= 3 ) break;

			VoronoiSite neighbor = _generator.Voronoi.VoronoiSites[neighborIds[i]];
			if ( neighbor == null ) continue;

			float dx = x - neighbor.Position.x;
			float dy = y - neighbor.Position.y;
			float distSq = (dx * dx) + (dy * dy);

			if ( candidates.Count == 1 )
			{
				candidates.Candidate1 = new VoronoiResult { Site = neighbor, DistanceSq = distSq };
				candidates.Count = 2;
			}
			else if ( candidates.Count == 2 )
			{
				candidates.Candidate2 = new VoronoiResult { Site = neighbor, DistanceSq = distSq };
				candidates.Count = 3;
			}
		}

		// sort candidates
		if ( candidates.Count == 2 )
		{
			if ( candidates.Candidate0.DistanceSq > candidates.Candidate1.DistanceSq )
			{
				var temp = candidates.Candidate0;
				candidates.Candidate0 = candidates.Candidate1;
				candidates.Candidate1 = temp;
			}
		}
		else if ( candidates.Count == 3 )
		{
			if ( candidates.Candidate0.DistanceSq > candidates.Candidate1.DistanceSq )
			{
				var temp = candidates.Candidate0;
				candidates.Candidate0 = candidates.Candidate1;
				candidates.Candidate1 = temp;
			}
			
			if ( candidates.Candidate1.DistanceSq > candidates.Candidate2.DistanceSq )
			{
				var temp = candidates.Candidate1;
				candidates.Candidate1 = candidates.Candidate2;
				candidates.Candidate2 = temp;
			}
			
			if ( candidates.Candidate0.DistanceSq > candidates.Candidate1.DistanceSq )
			{
				var temp = candidates.Candidate0;
				candidates.Candidate0 = candidates.Candidate1;
				candidates.Candidate1 = temp;
			}
		}
			
		return candidates;
	}

	private double BuildTectonicSuperstructure( int x, int y )
	{
		// pick out plate weight candidates
		int plate0Id = _neighbors.Candidate0.Site.PlateId;
		float plate0Weight = 0f;

		int plate1Id = _neighbors.Candidate1.Site.PlateId;
		float plate1Weight = 0f;

		int plate2Id = -1;
		float plate2Weight = 0f;
		bool hasThirdNeighbor = _neighbors.Count >= 3;
		if ( hasThirdNeighbor )
			plate2Id = _neighbors.Candidate2.Site.PlateId;

		float totalWeight = 0f;
		double baseInterpolatedElevation = 0d;
		
		// do mathy math
		float d0Max = Math.Max( 1.0f, _neighbors.Candidate0.DistanceSq );
		float w1 = 1.0f / ( d0Max * d0Max );
		totalWeight += w1;
		baseInterpolatedElevation += _neighbors.Candidate0.Site.BaseElevation * w1;
		plate0Weight += w1;

		float d1Max = Math.Max( 1.0f, _neighbors.Candidate1.DistanceSq );
		float w2 = 1.0f / ( d1Max * d1Max );
		totalWeight += w2;
		baseInterpolatedElevation += _neighbors.Candidate1.Site.BaseElevation * w2;
		
		// accumulate weights
		if ( plate1Id == plate0Id )
			plate0Weight += w2;
		else
			plate1Weight += w2;
		
		if ( hasThirdNeighbor )
		{
			float d2Max = Math.Max( 1.0f, _neighbors.Candidate2.DistanceSq );
			float w3 = 1.0f / ( d2Max * d2Max );
			totalWeight += w3;
			baseInterpolatedElevation += _neighbors.Candidate2.Site.BaseElevation * w3;
		
			if ( plate2Id == plate0Id )
				plate0Weight += w3;
			else if ( plate2Id == plate1Id )
				plate1Weight += w3;
			else
				plate2Weight += w3;
		}
		
		// begin final elevation evaluation
		double elevation = baseInterpolatedElevation / totalWeight;
		// Determine how many unique plates actually collected weights
		int uniquePlateCount = 1;
		if ( plate1Weight > 0f ) uniquePlateCount++;
		if ( plate2Weight > 0f ) uniquePlateCount++;

		// Find the two highest weights with branch sorting
		if ( uniquePlateCount > 1 )
		{
			float primaryInfluence;
			float secondaryInfluence;

			if ( plate0Weight >= plate1Weight )
			{
				if ( plate0Weight >= plate2Weight )
				{
					primaryInfluence = plate0Weight;
					secondaryInfluence = Math.Max( plate1Weight, plate2Weight );
				}
				else
				{
					primaryInfluence = plate2Weight;
					secondaryInfluence = plate0Weight;
				}
			}
			else
			{
				if ( plate1Weight >= plate2Weight )
				{
					primaryInfluence = plate1Weight;
					secondaryInfluence = Math.Max( plate0Weight, plate2Weight );
				}
				else
				{
					primaryInfluence = plate2Weight;
					secondaryInfluence = plate1Weight;
				}
			}

			primaryInfluence /= totalWeight;
			secondaryInfluence /= totalWeight;
			
			float boundaryFriction = Math.Min( primaryInfluence, secondaryInfluence ) * 2.0f;
			if ( boundaryFriction > 0.05f && !_neighbors.Candidate0.Site.IsOceanic && !_neighbors.Candidate1.Site.IsOceanic )
			{
				var boundaryShape = boundaryFriction * boundaryFriction * (3.0 - 2.0 * boundaryFriction);
				var baseMountainHeight = Math.Max(_neighbors.Candidate0.Site.BaseElevation, _neighbors.Candidate1.Site.BaseElevation );
				var targetSpineHeight = MathX.Lerp( baseMountainHeight, Settings.PeakLevel - 0.02f, boundaryShape * 0.7f );
				elevation = Math.Max( elevation, targetSpineHeight );
			}
		}

		if ( !_neighbors.Candidate0.Site.IsOceanic )
		{
			var landCoreFactor = Math.Max( 0.0d, _neighbors.Candidate0.Site.BaseElevation - Settings.SeaLevel );
			elevation += landCoreFactor * 0.32;
		}
		
		return Math.Max(-1.0d, Math.Min(1.0d, elevation));
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
	private RegionId GetRegion( double elevation )
	{
		return Generation.RegionId.Unassigned; // @TODO: Does nothing for now.
	}
}
