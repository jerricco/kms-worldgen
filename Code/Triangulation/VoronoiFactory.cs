using System;
using Sandbox.Generator;
using Sandbox.Generation;

namespace Sandbox.Triangulation;

[Category("Procedural Generation")]
public sealed class VoronoiFactory : Component
{
	[Property] public GenerationSettings Settings { get; set;  }
	[Property, ReadOnly] private MapGenerator Generator { get; set;  }

    [Property] private float ContinentalFragmentationFactor { get; set; }
    [Property] private float MacroBayFrequency { get; set; }
    [Property] private float MacroBayIntensity { get; set; }

    public Delaunator Delaunay;
    public List<DelaunayChunk> DelaunayChunks = new();
    public List<VoronoiSite> VoronoiSites;
    
    private List<Vector2> _plateCenters;
    private List<float> _plateElevationBiases;
    
    protected override void OnStart()
    {
		Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
    }

    public void Generate()
    {
	    float startTime = RealTime.Now; // @DEBUG
	    
        BuildTectonicSpine();
        BuildVoronoiSites();
        BuildDelaunay(); // @DEBUG

        // create mesh for drawing the voronoi sites - @TODO: debug flag
        var pdRenderer = GetComponent<ProceduralDelaunayRenderer>();
        pdRenderer.RebuildMesh(Delaunay);
        
        Log.Info( $"Voronoi generation complete! Took {RealTime.Now - startTime} s" );
    }
    
    public DelaunayNeighbors GetVoronoiSiteCandidates( float x, float y )
	{
		// clear old candidates
		DelaunayNeighbors candidates = default;
		Vector2 globalPosition = new Vector2( x, y );
		
		int site0Id = FindClosestPointIndex( globalPosition );
		VoronoiSite site0 = Generator.Voronoi.VoronoiSites[site0Id];
		if ( site0 == null ) return candidates;
		
		// 1. Calculate Site 0
		float dx0 = x - site0.Position.x;
		float dy0 = y - site0.Position.y;
		candidates.Candidate0 = new VoronoiResult { Site = site0, DistanceSq = (dx0 * dx0) + (dy0 * dy0) };
		candidates.Count = 1;
		
		var neighborIds = GetNeighbors( site0Id );
		int neighborCount = neighborIds.Count;
		for ( int i = 0; i < neighborCount; i++ )
		{
			if ( candidates.Count >= 3 ) break;

			VoronoiSite neighbor = VoronoiSites[neighborIds[i]];
			if ( neighbor == null ) continue;

			float dx = x - neighbor.Position.x;
			float dy = y - neighbor.Position.y;
			float distSq = (dx * dx) + (dy * dy);

			if ( candidates.Count == 1 )
			{
				candidates.Candidate1 = new VoronoiResult(neighbor, distSq);
				candidates.Count = 2;
			}
			else if ( candidates.Count == 2 )
			{
				candidates.Candidate2 = new VoronoiResult(neighbor, distSq);
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
    
    /// <summary>
    /// Finds the index of the Delaunator input point closest to the specified target coordinates.
    /// </summary>
    public int FindClosestPointIndex( Vector2 target )
    {
	    if ( Delaunay == null || Delaunay.Points == null || Delaunay.Points.Length == 0 )
	    {
		    return -1;
	    }

	    int closestIndex = 0;
	    // Use DistanceSquared to completely bypass costly Math.Sqrt calculations on the CPU
	    float minDistanceSq = float.MaxValue;

	    for ( int i = 0; i < Delaunay.Points.Length; i++ )
	    {
		    var p = Delaunay.Points[i];
		
		    float dx = (float)p.X - target.x;
		    float dy = (float)p.Y - target.y;
		    float distSq = (dx * dx) + (dy * dy);

		    if ( distSq < minDistanceSq )
		    {
			    minDistanceSq = distSq;
			    closestIndex = i;
		    }
	    }

	    return closestIndex;
    }
    
    /// <summary>
    /// Returns a list of point indices that share a direct Delaunay edge with point index 'pointIndex'.
    /// </summary>
    public List<int> GetNeighbors( int pointIndex )
    {
	    var neighbors = new HashSet<int>();

	    if ( Delaunay == null || Delaunay.Triangles == null )
		    return new List<int>();

	    // Triangles are grouped in sets of 3 indices. Loop through every single triangle.
	    for ( int i = 0; i < Delaunay.Triangles.Length; i += 3 )
	    {
		    int a = (int)Delaunay.Triangles[i];
		    int b = (int)Delaunay.Triangles[i + 1];
		    int c = (int)Delaunay.Triangles[i + 2];

		    // If this triangle contains our target point, the other two vertices are neighbors
		    if ( a == pointIndex )
		    {
			    neighbors.Add( b );
			    neighbors.Add( c );
		    }
		    else if ( b == pointIndex )
		    {
			    neighbors.Add( a );
			    neighbors.Add( c );
		    }
		    else if ( c == pointIndex )
		    {
			    neighbors.Add( a );
			    neighbors.Add( b );
		    }
	    }

	    return new List<int>( neighbors );
    }

    /**
     * PASS 1: THE MACRO TECTIONIC SPINE
     * Generates a linear, curved skeletal structure across the map space
     * to group separate land masses into long continental systems like the Americas.
    */
    private void BuildTectonicSpine()
    {
	    _plateCenters = [];
	    _plateElevationBiases = [];

        ContinentalFragmentationFactor = Generator.Rng.NextRangeFloat(0.35f, 0.60f);
        MacroBayFrequency = Generator.Rng.NextRangeFloat(0.002f, 0.005f);
        MacroBayIntensity = Generator.Rng.NextRangeFloat(0.20f, 0.35f);

        Log.Info( $"Tectonic spine generating with settings..." );
        Log.Info( $"======== Continental Fragmentation Factor: {ContinentalFragmentationFactor.ToString( "F3" )}" );
		Log.Info( $"======== Bay Frequency: {MacroBayFrequency.ToString( "F3" )}" );
		Log.Info( $"======== Bay Intensity: {MacroBayIntensity.ToString("F3")}" );

        int tectonicPlateCount = Generator.Rng.NextRange(6, 9);
        float spineAngle = Generator.Rng.NextRangeFloat(0f, MathF.Tau);
        float spineDirectionX = MathF.Cos(spineAngle);
        float spineDirectionY = MathF.Sin(spineAngle);

        for (int p = 0; p < tectonicPlateCount; p++)
        {
	        float progress = (p / (tectonicPlateCount - 1f)) * 2.0f - 1.0f;
	        float bowIntensity = Settings.MaxDimension * 0.18f;
	        float bowNoise = MathF.Sin(progress * MathF.PI) * bowIntensity;

	        float px = (spineDirectionX * progress * Settings.HalfWidth * 0.6f) + (-spineDirectionY * bowNoise);
            float py = (spineDirectionY * progress * Settings.HalfHeight * 0.6f) + (spineDirectionX * bowNoise);
            
            Vector2 platePosition = new Vector2((float)px, (float)py);
            float plateElevationBias = Generator.Rng.NextRangeFloat(-0.15f, 0.45f);

            _plateCenters.Add(platePosition);
            _plateElevationBiases.Add(plateElevationBias);
        }

        Log.Info($"Tectonic spine with {_plateCenters.Count} tectonic plates generated.");
        Log.Info( $"======== Overall spine angle: {spineAngle.ToString("F3")} radian" );
        Log.Info( $"======== X spine direction: {spineDirectionX.ToString("F3")} radian" );
        Log.Info( $"======== Y spine direction: {spineDirectionY.ToString("F3")} radian" );
    }

    /**
     * PASS 2: GEOLOGICAL FIELD EVALUATION
     * Pure function that assesses a single coordinate and returns its total 
     * structural land chance value [0.0 - 1.0] and its closest plate tracking metadata.
    */
    private GeologicalField EvaluateGeologicalField(float x, float y)
    {
        Vector2 warpedSpace = Generator.SampleWarpedDomain(x, y);
        float macroShapeNoise = (float)((Generator.Noise.Evaluate( warpedSpace.x * 0.8d, warpedSpace.y * 0.8d ) + 1d) * 0.5d);
        float channelNoise = (float)((Generator.Noise.Evaluate(warpedSpace.y * 2.5d, warpedSpace.x * 2.5d) + 1d) * 0.5d);
    
        // macro erosion pass
        // creates large-feature coastal indentations that carve into the core spine.
        float bayNoise = (float)Generator.Noise.Evaluate(x * MacroBayFrequency, y * MacroBayFrequency);
        float gulfCarve = MathF.Pow((bayNoise + 1f) * 0.5f, 1.5f) * MacroBayIntensity;

        int closestPlateId = 0;
        float minPlateDistanceSq = float.PositiveInfinity;
        for (int p = 0; p < _plateCenters.Count; p++)
        {
	        float dx = x - _plateCenters[p].x;
            float dy = y - _plateCenters[p].y;
            float distSq = dx * dx + dy * dy; 
            if (distSq < minPlateDistanceSq)
            {
	            minPlateDistanceSq = distSq;
	            closestPlateId = p;
            }
        }

        float distanceToClosestPlate = MathF.Sqrt(minPlateDistanceSq);
        float plateInfluenceRadius = Settings.MaxDimension * 0.42f;
        float tectonicProximity = MathF.Max(0.0f, MathF.Min(1.0f, 1.0f - (distanceToClosestPlate / plateInfluenceRadius)));
        float continentalCoreMask = MathF.Pow(tectonicProximity, 1.2f);

        float globalLandChance = float.Lerp(macroShapeNoise * 0.4f, 0.46f + macroShapeNoise * 0.54f, continentalCoreMask);
        if (channelNoise < ContinentalFragmentationFactor)
        {
	        globalLandChance *= channelNoise / ContinentalFragmentationFactor;
        }

        // apply the bay/gulf carving pass to the land profile
        globalLandChance = MathF.Max(0.0f, globalLandChance - gulfCarve);

        float distanceToCenter = MathF.Sqrt(warpedSpace.x * warpedSpace.x + warpedSpace.y * warpedSpace.y);
        float maxAllowedRadius  = Settings.HalfWidth * Settings.OceanClamp;
        float boundaryBuffer = MathF.Max(0.0f, MathF.Min(1.0f, distanceToCenter / maxAllowedRadius));
        globalLandChance = MathF.Max(0.0f, globalLandChance - MathF.Pow(boundaryBuffer, 4.0f));
        
        return new GeologicalField( globalLandChance, closestPlateId );
    }

    /**
     * PASS 3: ASSEMBLY
     * Implements the randomized rejection loop, sampling positions across the 
     * world grid and registering valid nodes into the final site collections.
    */
    private void BuildVoronoiSites()
    {
	    VoronoiSites = new List<VoronoiSite>();
        // @TODO These two values should be a setting in GenerationSettings??
        // - 30 -> Settings.MinVoronoiGridSize
        // - Settings.ChunkGridSize ->(add) Settings.VoronoiGridSize
        int baseSpacing = Math.Max(30, Settings.ChunkGridSize);
        int targetPoints = Settings.WorldWidth * Settings.WorldHeight / (baseSpacing * baseSpacing);

        int siteIdCounter = 0;
        int attempts = 0;
        int maxAttempts = targetPoints * 12;

        Log.Info( $"Building voronoi site list..." );
        Log.Info( $"======== Site spacing: {baseSpacing}" );
        Log.Info( $"======== Target points: {targetPoints}" );
        Log.Warning( $"======== Builder will only attempt a max of {maxAttempts} times!" );
        
        while (VoronoiSites.Count < targetPoints && attempts < maxAttempts)
        {
	        attempts++;

            float rotX = -Settings.HalfWidth + (Generator.Rng.Next() * Settings.WorldWidth);
            float rotY = -Settings.HalfHeight + (Generator.Rng.Next() * Settings.WorldHeight);
            
            GeologicalField densityField = EvaluateGeologicalField(rotX, rotY);
            float acceptanceProbability = float.Lerp(0.012f, 1.0f, MathF.Pow(densityField.LandChance, 1.2f));

            if (Generator.Rng.Next() > acceptanceProbability) continue;

            // twist displacement
            float twistFrequency = 1.0f / (baseSpacing * 5.0f);
            float twistAngle = (float)
	            (Generator.Noise.Evaluate( rotX * twistFrequency, rotY * twistFrequency ) * Math.Tau);
            float twistIntensity = baseSpacing * 0.7f * (1.0f - densityField.LandChance);

            float finalX = rotX + MathF.Cos(twistAngle) * twistIntensity;
            float finalY = rotY + MathF.Sin(twistAngle) * twistIntensity;

            if (finalX < -Settings.HalfWidth || finalX > Settings.HalfWidth || finalY < -Settings.HalfHeight || finalY > Settings.HalfHeight)
            {
                continue;
            }

            GeologicalField finalField = EvaluateGeologicalField(finalX, finalY);
            bool isOceanic = finalField.LandChance < 0.42f; // @TODO: I should figure out how this lever relates to other values
            float baseElevation;

            if (isOceanic)
            {
                // @TODO: MaxAllowedRadius might do better as computed on GenerationSettings
                float maxAllowedRadius = Settings.HalfWidth * Settings.OceanClamp;
                float trueDist = MathF.Sqrt(finalX * finalX + finalY * finalY);
                float trueRatio = MathF.Max(0.0f, Math.Min(1.0f, trueDist / maxAllowedRadius));
                float trenchFactor = MathF.Pow(trueRatio, 1.8f);
                // grade the ocean smoothly to Settings.AbyssalLevel
                baseElevation = float.Lerp(Settings.SeaLevel - 0.05f, Settings.AbyssalLevel, trenchFactor)
                                + (_plateElevationBiases[finalField.ClosestPlateId] * 0.08f);
            } else
            {
                // force values to distribute smoothly up through Settings.HillLevel and Settigns.MountainLevel
                float landProgress = (finalField.LandChance - 0.42f) / 0.58f; // @TODO: ???? this feels off.
                float exponentialRise = MathF.Pow(landProgress, 1.6f);
                baseElevation = float.Lerp(Settings.SeaLevel + 0.02f, Settings.PeakLevel, exponentialRise)
                                + (_plateElevationBiases[finalField.ClosestPlateId]) * 0.15f;
            }

            VoronoiSite localSite = new VoronoiSite(
                siteIdCounter++,
                new Vector2(finalX, finalY),
                finalField.ClosestPlateId,
                isOceanic,
                Math.Max(-1.0, Math.Min(1.0, baseElevation))
            );

            VoronoiSites.Add(localSite);
        }
        
        
        Log.Info( $"Created {VoronoiSites.Count} voronoi sites." );
    }

    /**
     * PASS 3: TRIANGULATION
     * Create a Delaunay triangle array from the generated Vector2 points in Sites.
     * This is largely only useful currently for rendering a debug overlay of Voronoi sites.
     * Once I understand Delaunay more, I may be able to utilise it better for generation.
    */
    private void BuildDelaunay()
    {
	    IPoint[] delaunayPoints = VoronoiSites.Select(s => (IPoint)new Point(s.Position.x, s.Position.y)).ToArray();
	    
	    // Do triangulation
	    Log.Info( $"Delaunay triangulation for {delaunayPoints.Length} Voronoi sites." );
	    Delaunay = new Delaunator(delaunayPoints);
	    Log.Info( $"{Delaunay.Triangles.Length} triangles created." );
	    
	    // Chunk the Delaunay space
	    Log.Info( $"Dividing the Delaunay triangles into grid sections {Settings.CellGridSize} wide." );
	    BuildSpatialDelaunayGrid();
	    Log.Info( $"Spatial delaunay grid created with {DelaunayChunks.Count} chunks." );
    }

    // Divides the Delaunay triangle space into chunks so that we can ensure much faster checking of frustum bounds
    // when trying to do anything with rendering or scanning wide portions of the tesselation space.
    private void BuildSpatialDelaunayGrid()
    {
	    DelaunayChunks.Clear();
	    var grid = new Dictionary<Vector2, DelaunayChunk>();
	    var d = Delaunay;
	    var chunkSize = Settings.CellGridSize;
	    
	    for ( int i = 0; i < d.Triangles.Length; i += 3 )
	    {
		    IPoint pA = d.Points[d.Triangles[i]];
		    IPoint pB = d.Points[d.Triangles[i + 1]];
		    IPoint pC = d.Points[d.Triangles[i + 2]];
		    
		    Vector3 a3D = new Vector3( (float)pA.X, (float)pA.Y, 0 );
		    Vector3 b3D = new Vector3( (float)pB.X, (float)pB.Y, 0 );
		    Vector3 c3D = new Vector3( (float)pC.X, (float)pC.Y, 0 );
		    
		    // Calculate a center point for the triangle to determine its grid cell based on the original
		    // CellGridSize provided to generate the tectonic voronoi spine.
		    Vector3 center = (a3D + b3D + c3D) / 3f;
		    Vector2 gridPos = new Vector2( MathF.Floor(center.x / chunkSize), MathF.Floor(center.y / chunkSize) );
		    
		    if ( !grid.TryGetValue( gridPos, out var chunk ) )
		    {
			    chunk = new DelaunayChunk( BBox.FromPositionAndSize( a3D, 0f ), new List<int>() );
			    grid[gridPos] = chunk;
		    }
		    
		    chunk.ChunkBounds = chunk.ChunkBounds.AddPoint( a3D ).AddPoint( b3D ).AddPoint( c3D );
		    chunk.TriangleIndices.Add( i );
	    }
	    
	    DelaunayChunks = grid.Values.ToList();
    }
}
