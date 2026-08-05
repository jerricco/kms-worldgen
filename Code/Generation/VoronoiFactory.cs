using System;
using Sandbox.Gameplay;
using Sandbox.Triangulation;

namespace Sandbox.Generation;

[Category("Procedural Generation")]
public sealed class VoronoiFactory : Component
{
	[Property] public GenerationSettings Settings { get; set;  }
	[Property, ReadOnly] private MapGenerator Generator { get; set;  }

    [Property] private double ContinentalFragmentationFactor { get; set; }
    [Property] private double MacroBayFrequency { get; set; }
    [Property] private double MacroBayIntensity { get; set; }

    public Delaunator Delaunay;
    public List<DelaunayChunk> DelaunayChunks = new();
    
    private List<VoronoiSite> _voronoiSites;
    private List<Vector2> _plateCenters;
    private List<double> _plateElevationBiases;
    
    [Property, ReadOnly] private bool _drawDelaunay = true; // @DEBUG: normally false;

    protected override void OnStart()
    {
		Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
    }
    
    /*protected override void OnUpdate()
    {
	    if ( _drawDelaunay )
	    {
		    DrawDelaunay();
	    }
    }*/

    public void Generate()
    {
        BuildTectonicSpine();
        BuildVoronoiSites();
        BuildDelaunay(); // @DEBUG
        
        Log.Info( "Voronoi generation complete!" );
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

        ContinentalFragmentationFactor = Generator.Rng.NextRangeDouble(0.35d, 0.60d);
        MacroBayFrequency = Generator.Rng.NextRangeDouble(0.002d, 0.005d);
        MacroBayIntensity = Generator.Rng.NextRangeDouble(0.20d, 0.35d);

        Log.Info( $"Tectonic spine generating with settings..." );
        Log.Info( $"======== Continental Fragmentation Factor: {ContinentalFragmentationFactor.ToString( "F3" )}" );
		Log.Info( $"======== Bay Frequency: {MacroBayFrequency.ToString( "F3" )}" );
		Log.Info( $"======== Bay Intensity: {MacroBayIntensity.ToString("F3")}" );

        int tectonicPlateCount = Generator.Rng.NextRange(6, 9);
        double spineAngle = Generator.Rng.NextRangeDouble(0d, Math.Tau);
        double spineDirectionX = Math.Cos(spineAngle);
        double spineDirectionY = Math.Sin(spineAngle);

        for (int p = 0; p < tectonicPlateCount; p++)
        {
	        double progress = (p / (tectonicPlateCount - 1d)) * 2.0d - 1.0d;
	        double bowIntensity = Settings.MaxDimension * 0.18d;
	        double bowNoise = Math.Sin(progress * Math.PI) * bowIntensity;

            double px = (spineDirectionX * progress * Settings.HalfWidth * 0.6d) + (-spineDirectionY * bowNoise);
            double py = (spineDirectionY * progress * Settings.HalfHeight * 0.6d) + (spineDirectionX * bowNoise);
            
            Vector2 platePosition = new Vector2((float)px, (float)py);
            double plateElevationBias = Generator.Rng.NextRangeDouble(-0.15d, 0.45d);

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

    private (double LandChance, int ClosestPlateId) EvaluateGeologicalField(double x, double y)
    {
        (double sampleX, double sampleY) warpedSpace = Generator.SampleWarpedDomain(x, y);
        double macroShapeNoise = (Generator.Noise.Evaluate(warpedSpace.sampleX * 0.8d, warpedSpace.sampleY * 0.8d) + 1d) * 0.5d;
        double channelNoise = (Generator.Noise.Evaluate(warpedSpace.sampleY * 2.5d, warpedSpace.sampleX * 2.5d) + 1d) * 0.5d;
    
        // macro erosion pass
        // creates large-feature coastal indentations that carve into the core spine.
        double bayNoise = Generator.Noise.Evaluate(x * MacroBayFrequency, y * MacroBayFrequency);
        double gulfCarve = Math.Pow((bayNoise + 1d) * 0.5d, 1.5d) * MacroBayIntensity;

        int closestPlateId = 0;
        double minPlateDistanceSq = double.PositiveInfinity;
        for (int p = 0; p < _plateCenters.Count; p++)
        {
            double dx = x - _plateCenters[p].x;
            double dy = y - _plateCenters[p].y;
            double distSq = dx * dx + dy * dy; 
            if (distSq < minPlateDistanceSq)
            {
	            minPlateDistanceSq = distSq;
	            closestPlateId = p;
            }
        }

        double distanceToClosestPlate = Math.Sqrt(minPlateDistanceSq);
        double plateInfluenceRadius = Settings.MaxDimension * 0.42d;
        double tectonicProximity = Math.Max(0.0d, Math.Min(1.0d, 1.0d - (distanceToClosestPlate / plateInfluenceRadius)));
        double continentalCoreMask = Math.Pow(tectonicProximity, 1.2d);

        double globalLandChance = double.Lerp(macroShapeNoise * 0.4d, 0.46d + macroShapeNoise * 0.54d, continentalCoreMask);
        if (channelNoise < ContinentalFragmentationFactor)
        {
	        globalLandChance *= channelNoise / ContinentalFragmentationFactor;
        }

        // apply the bay/gulf carving pass to the land profile
        globalLandChance = Math.Max(0.0d, globalLandChance - gulfCarve);

        double distanceToCenter = Math.Sqrt(warpedSpace.sampleX * warpedSpace.sampleX + warpedSpace.sampleY * warpedSpace.sampleY);
        double maxAllowedRadius  = Settings.HalfWidth * Settings.OceanClamp;
        double boundaryBuffer = Math.Max(0.0d, Math.Min(1.0d, distanceToCenter / maxAllowedRadius));
        globalLandChance = Math.Max(0.0d, globalLandChance - Math.Pow(boundaryBuffer, 4.0d));
        
        return ( globalLandChance, closestPlateId );
    }

    /**
     * PASS 3: ASSEMBLY
     * Implements the randomized rejection loop, sampling positions across the 
     * world grid and registering valid nodes into the final site collections.
    */
    private void BuildVoronoiSites()
    {
	    _voronoiSites = new List<VoronoiSite>();
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
        
        while (_voronoiSites.Count < targetPoints && attempts < maxAttempts)
        {
	        attempts++;

            float rotX = -Settings.HalfWidth + (Generator.Rng.Next() * Settings.WorldWidth);
            float rotY = -Settings.HalfHeight + (Generator.Rng.Next() * Settings.WorldHeight);
            
            (double landChance, int closestPlateId) densityField = EvaluateGeologicalField(rotX, rotY);
            double acceptanceProbability = double.Lerp(0.012d, 1.0d, Math.Pow(densityField.landChance, 1.2d));

            if (Generator.Rng.Next() > acceptanceProbability) continue;

            // twist displacement
            double twistFrequency = 1.0 / (baseSpacing * 5.0);
            double twistAngle = Generator.Noise.Evaluate(rotX * twistFrequency, rotY * twistFrequency) * Math.Tau;
            double twistIntensity = baseSpacing * 0.7d * (1.0d - densityField.landChance);

            double finalX = rotX + Math.Cos(twistAngle) * twistIntensity;
            double finalY = rotY + Math.Sin(twistAngle) * twistIntensity;

            if (finalX < -Settings.HalfWidth || finalX > Settings.HalfWidth || finalY < -Settings.HalfHeight || finalY > Settings.HalfHeight)
            {
                continue;
            }

            (double landChance, int closestPlateId) finalField = EvaluateGeologicalField(finalX, finalY);
            bool isOceanic = finalField.landChance < 0.42d; // @TODO: I should figure out how this lever relates to other values
            double baseElevation;

            if (isOceanic)
            {
                // @TODO: MaxAllowedRadius might do better as computed on GenerationSettings
                double maxAllowedRadius = Settings.HalfWidth * Settings.OceanClamp;
                double trueDist = Math.Sqrt(finalX * finalX + finalY * finalY);
                double trueRatio = Math.Max(0.0d, Math.Min(1.0d, trueDist / maxAllowedRadius));
                double trenchFactor = Math.Pow(trueRatio, 1.8d);
                // grade the ocean smoothly to Settings.AbyssalLevel
                baseElevation = double.Lerp(Settings.SeaLevel - 0.05d, Settings.AbyssalLevel, trenchFactor)
                                + (_plateElevationBiases[finalField.closestPlateId] * 0.08d);
            } else
            {
                // force values to distribute smoothly up through Settings.HillLevel and Settigns.MountainLevel
                double landProgress = (finalField.landChance - 0.42d) / 0.58d; // @TODO: ???? this feels off.
                double exponentialRise = Math.Pow(landProgress, 1.6d);
                baseElevation = double.Lerp(Settings.SeaLevel + 0.02d, Settings.PeakLevel, exponentialRise)
                                + (_plateElevationBiases[finalField.closestPlateId]) * 0.15d;
            }

            VoronoiSite localSite = new VoronoiSite()
            {
                Id = siteIdCounter++,
                Position = new Vector2((float)finalX, (float)finalY),
                PlateId = finalField.closestPlateId,
                IsOceanic = isOceanic,
                BaseElevation = Math.Max(-1.0, Math.Min(1.0, baseElevation))
            };

            _voronoiSites.Add(localSite);
        }
        
        
        Log.Info( $"Created {_voronoiSites.Count} voronoi sites." );
    }

    /**
     * PASS 3: TRIANGULATION
     * Create a Delaunay triangle array from the generated Vector2 points in Sites.
     * This is largely only useful currently for rendering a debug overlay of Voronoi sites.
     * Once I understand Delaunay more, I may be able to utilise it better for generation.
    */
    private void BuildDelaunay()
    {
	    IPoint[] delaunayPoints = _voronoiSites.Select(s => (IPoint)new Point(s.Position.x, s.Position.y)).ToArray();
	    
	    // Do triangulation
	    Log.Info( $"Delaunay triangulation for {delaunayPoints.Length} Voronoi sites." );
	    Delaunay = new Delaunator(delaunayPoints);
	    Log.Info( $"{Delaunay.Triangles.Length} triangles created." );
	    
	    // Chunk the Delaunay space
	    Log.Info( $"Dividing the Delaunay triangles into grid sections {Settings.CellGridSize} wide." );
	    BuildSpatialDelaunayGrid();
	    Log.Info( $"Spatial delaunay grid created with {DelaunayChunks.Count} chunks." );

	    var pdRenderer = GetComponent<ProceduralDelaunayRenderer>();
	    pdRenderer.RebuildMesh(Delaunay);
	    Log.Info( "Mesh geometry created for voronoi information. Toggle 'Draw Voronoi Cells' to view." );
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
			    chunk = new DelaunayChunk { 
				    ChunkBounds = BBox.FromPositionAndSize( a3D, 0f ), 
				    TriangleIndices = new List<int>() 
			    };
			    grid[gridPos] = chunk;
		    }
		    
		    chunk.ChunkBounds = chunk.ChunkBounds.AddPoint( a3D ).AddPoint( b3D ).AddPoint( c3D );
		    chunk.TriangleIndices.Add( i );
	    }
	    
	    DelaunayChunks = grid.Values.ToList();
    }
    
    [Obsolete("Being replaced with Sandbox.Triangulation.ProceduralDelaunayRenderer")]
    private void DrawDelaunay()
    {
	    // stop rendering immediately if the mesh count doesn't exist
	    if (Delaunay == null || Delaunay.Triangles.Length == 0 ) return; 
	    
	    // stop rendering if no camera exists
	    var camera = Scene.Camera;
	    MapCameraController camControl = Scene.GetAllComponents<MapCameraController>().FirstOrDefault();
	    if (camera == null || camControl == null) return;
	    
	    // clamp so that bottom 60% of zoom levels is the only time it's actually rendering, even if culled
	    float maxRenderHeight = camControl.MaxZoom * 0.6f; 
	    if ( camera.OrthographicHeight > maxRenderHeight ) return;
	    
	    // get the current frustum to ensure it only draws voronoi in the screen bounds.
	    var frustum = camera.GetFrustum( camera.ScreenRect, Screen.Size );
	    foreach ( var chunk in DelaunayChunks )
	    {
		    if (!frustum.IsInside( chunk.ChunkBounds, true )) continue;

		    for ( int i = 0; i < chunk.TriangleIndices.Count; i += 3)
		    {
			    int t = chunk.TriangleIndices[i];
			    if (t + 2 >= Delaunay.Triangles.Length ) continue;
			    
			    IPoint pA = Delaunay.Points[Delaunay.Triangles[t]];
			    IPoint pB = Delaunay.Points[Delaunay.Triangles[t + 1]];
			    IPoint pC = Delaunay.Points[Delaunay.Triangles[t + 2]];
            
			    Vector3 a3D = new Vector3( (float)pA.X, (float)pA.Y, 0 );
			    Vector3 b3D = new Vector3( (float)pB.X, (float)pB.Y, 0 );
			    Vector3 c3D = new Vector3( (float)pC.X, (float)pC.Y, 0 );

			    DebugOverlay.Line( a3D, b3D, Color.Magenta, 0f );
			    DebugOverlay.Line( b3D, c3D, Color.Magenta, 0f );
			    DebugOverlay.Line( c3D, a3D, Color.Magenta, 0f );
		    }
	    }
    }

    [Button( "Draw Voronoi Cells " )]
    public void ToggleDrawDelaunay()
    {
	    _drawDelaunay = !_drawDelaunay;
    }
}
