using System;
using Sandbox.Generator;
using Sandbox.Generation;
using Sandbox.Utility;

namespace Sandbox.Triangulation;

[Category("Procedural Generation")]
public sealed class VoronoiFactory : Component
{
	[Property] public GenerationSettings Settings { get; set;  }
	
	public Delaunator Delaunay;
	public Voronator Voronoi;
	public List<CurvedSpine> TectonicSpines;
	// Cache object for handling fast voronoi/delaunay data spatial lookup
	public static Dictionary<Vector2, List<(int index, Vector2 pos)>> PointGridBuckets = new();
	public Vector2[] CachedCellCenters;
	
	private MapGenerator Generator { get; set;  }
	
    protected override void OnStart()
    {
		Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
    }

    public void GenerateAndRender()
    {
	    float startTime = RealTime.Now;

	    TectonicSpines = GenerateTectonicNetwork();
        List<Vector2> points = SampleDensityPoints();
        Voronoi = new Voronator( points, -Settings.HalfWidth, Settings.HalfWidth );
        Delaunay = Voronoi.Delaunator;
        CacheCellCenters();
        
        Log.Info( $"Voronoi generation complete! Took {RealTime.Now - startTime} s" );
        
        // create mesh for drawing the voronoi sites - @TODO: debug flag
        var pdRenderer = GetComponent<ProceduralDelaunayRenderer>();
        pdRenderer.RebuildMesh(Delaunay);
        
        Log.Info( $"Voronoi mesh complete! Took {RealTime.Now - startTime} s" );
    }
    
    ///////////////////////////////////////////////
    //              Spatial Lookup               //
    ///////////////////////////////////////////////
    /// <summary>
    /// generates an index cache for looking up voronoi cell centers in a 1-D array
    /// </summary>
    public void CacheCellCenters()
    {
	    PointGridBuckets.Clear();
	    CachedCellCenters = Voronoi.GetRelaxedPoints().ToArray();
	    for (int i = 0; i < CachedCellCenters.Length; i++)
	    {
		    Vector2 pos = CachedCellCenters[i];
            
		    int gridX = MathX.FloorToInt(pos.x / Settings.CellGridSize);
		    int gridY = MathX.FloorToInt(pos.y / Settings.CellGridSize);
		    Vector2 bucketKey = new Vector2(gridX, gridY);

		    if (!PointGridBuckets.ContainsKey(bucketKey))
		    {
			    PointGridBuckets[bucketKey] = new List<(int, Vector2)>();
		    }
		    PointGridBuckets[bucketKey].Add((i, pos));
	    }
    }
    
    /// <summary>
    /// Lookup to find the nearest voronoi cell center inside the spatial grid
    /// </summary>
    /// <param name="globalPos"></param>
    /// <returns></returns>
    public int GetNearestVoronoiCell(Vector2 globalPos)
    {
	    // Determine which 400x400 bucket this tile sits in
	    int centerGridX = MathX.FloorToInt(globalPos.x / Settings.CellGridSize);
	    int centerGridY = MathX.FloorToInt(globalPos.y / Settings.CellGridSize);

	    float closestDistSq = float.MaxValue;
	    int bestCellIndex = 0;

	    // Check the tile's current bucket and the 8 surrounding buckets (3x3 grid)
	    // This handles cases where the closest site center belongs to a neighboring chunk
	    for (int offsetX = -1; offsetX <= 1; offsetX++)
	    {
		    for (int offsetY = -1; offsetY <= 1; offsetY++)
		    {
			    Vector2 targetBucket = new Vector2(centerGridX + offsetX, centerGridY + offsetY);

			    if (PointGridBuckets.TryGetValue(targetBucket, out var pointsInBucket))
			    {
				    for (int i = 0; i < pointsInBucket.Count; i++)
				    {
					    float distSq = Vector2.DistanceSquared(globalPos, pointsInBucket[i].pos);
					    if (distSq < closestDistSq)
					    {
						    closestDistSq = distSq;
						    bestCellIndex = pointsInBucket[i].index;
					    }
				    }
			    }
		    }
	    }

	    return bestCellIndex;
    }
    
    ///////////////////////////////////////////////
    //            Landmass Generation            //
    ///////////////////////////////////////////////
    // Structure to hold our spine data
    public struct CurvedSpine
    {
	    public List<Vector2> Nodes;
    }

    public List<CurvedSpine> GenerateTectonicNetwork()
    {
	    var networks = new List<CurvedSpine>();
	    int continentCount = Generator.Rng.NextRange(3, 6);
	    for ( int c = 0; c < continentCount; c++ )
	    {
		    var spine = new CurvedSpine { Nodes = new List<Vector2>() };
		    int nodeCount = Generator.Rng.NextRange(4, 6);
		    
		    float scatterRange = Settings.HalfWidth * 0.4f; 
		    Vector2 continentCenter = new Vector2(
			    Generator.Rng.NextRangeFloat(-scatterRange, scatterRange),
			    Generator.Rng.NextRangeFloat(-scatterRange, scatterRange)
		    );
		    
		    // Give each continent its own unique orientation vector
		    float baseAngle = Generator.Rng.NextRangeFloat(0, 360);
		    Vector2 direction = Vector2.FromDegrees(baseAngle);
		    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
		    
		    float innerRegion = Settings.HalfWidth * 0.25f; // Length of this specific spine
		    float startSpan = -innerRegion;
		    float endSpan = innerRegion;
		    float stepSize = (endSpan - startSpan) / (nodeCount - 1);
		    
		    for (int i = 0; i < nodeCount; i++)
		    {
			    float progress = startSpan + (i * stepSize);
			    Vector2 basePoint = continentCenter + (direction * progress);

			    float varianceScale = Settings.HalfWidth * 0.1f; 
			    float lateralOffset = Game.Random.Float(-varianceScale, varianceScale);
			    float forwardOffset = Game.Random.Float(-stepSize * 0.2f, stepSize * 0.2f);

			    Vector2 finalizedNode = basePoint + (direction * forwardOffset) + (perpendicular * lateralOffset);
			    spine.Nodes.Add(finalizedNode);
		    }

		    networks.Add(spine);
	    }

	    return networks;
    }
    
    /// <summary>
    /// Smoothly blends multiple values together. Lower values (closer distance) win.
    /// </summary>
    private float SmoothMin(float a, float b, float k)
    {
	    float h = Math.Clamp(0.5f + 0.5f * (b - a) / k, 0.0f, 1.0f);
	    return MathX.Lerp(b, a, h) - k * h * (1.0f - h);
    }
    
    /// <summary>
    /// Calculates the shortest distance from point P to line segment AB.
    /// </summary>
    private float DistanceToSpine(Vector2 p, CurvedSpine spine)
    {
	    float minDistance = float.MaxValue;
	    for (int i = 0; i < spine.Nodes.Count - 1; i++)
	    {
		    Vector2 a = spine.Nodes[i];
		    Vector2 b = spine.Nodes[i + 1];

		    // Standard segment distance math
		    Vector2 ab = b - a;
		    Vector2 ap = p - a;
		    float t = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
		    t = Math.Clamp(t, 0f, 1f);

		    Vector2 closestPoint = a + t * ab;
		    float distance = Vector2.Distance(p, closestPoint);

		    if (distance < minDistance)
		    {
			    minDistance = distance;
		    }
	    }

	    return minDistance;
    }
    
    /// <summary>
    /// populates the world with coordinates tightly packed on land, and sparse in oceans.
    /// </summary>
    public List<Vector2> SampleDensityPoints()
    {
        var points = new List<Vector2>();
        
        // Configuration settings for your cell sizes
        float minSeparationLand = 90f;    // Crisp, tiny land cells
        float minSeparationOcean = 950f;  // Large, sweeping ocean cells
        int chunkGridSize = 400;

        // Loop through your 400x400 grid layout across the map
        for (int x = -6400; x < 6400; x += chunkGridSize)
        {
            for (int y = -6400; y < 6400; y += chunkGridSize)
            {
                // sample positions inside each chunk boundary
                int attemptsPerChunk = 6; 
                for (int k = 0; k < attemptsPerChunk; k++)
                {
                    float sampleX = x + Generator.Rng.NextRangeFloat(0, chunkGridSize);
                    float sampleY = y + Generator.Rng.NextRangeFloat(0, chunkGridSize);
                    Vector2 candidate = new Vector2(sampleX, sampleY);

                    // Fetch our 0.0 - 1.0 land density ranking
                    float density = GetContinuousElevationAt(candidate);

                    // Map the density smoothly to a local minimum distance radius
                    float requiredRadius = MathX.Lerp(minSeparationOcean, minSeparationLand, density);

                    // Verify if the candidate respects surrounding neighbors
                    bool positionIsValid = true;
                    foreach (var existingPoint in points)
                    {
                        // Quick distance squaring validation avoids heavy square-root operations
                        if (Vector2.DistanceSquared(candidate, existingPoint) < requiredRadius * requiredRadius)
                        {
                            positionIsValid = false;
                            break;
                        }
                    }

                    if (positionIsValid)
                    {
                        points.Add(candidate);
                    }
                }
            }
        }
        
        // anchor the points to the map corners
        points.Add(new Vector2(-Settings.HalfWidth, -Settings.HalfWidth));
        points.Add(new Vector2(Settings.HalfWidth, -Settings.HalfWidth));
        points.Add(new Vector2(-Settings.HalfWidth, Settings.HalfWidth));
        points.Add(new Vector2(Settings.HalfWidth, Settings.HalfWidth));
        
        return points;
    }
    
    /// <summary>
    /// Evaluates a point against all tectonic spines to create a multi-continental system.
    /// </summary>
    public float GetContinuousElevationAt(Vector2 point)
    {
	    // apply noise distortion to the sample point for organic coastlines
	    float noiseScale = 0.0005f; 
	    float noiseStrength = 800f; 
	    
	    float offsetX = Generator.Rng.NextRangeFloat(-Settings.HalfWidth * 10, Settings.HalfWidth * 10);
	    float offsetY = Generator.Rng.NextRangeFloat(-Settings.HalfWidth * 10, Settings.HalfWidth * 10);

	    float warpedX = point.x + offsetX * noiseScale;
	    float warpedY = point.y + offsetY * noiseScale;
	    
	    float noiseX = Noise.Perlin(warpedX, warpedY, 0f) * noiseStrength;
	    float noiseY = Noise.Perlin(warpedX, warpedY, 1.5f) * noiseStrength;
	    Vector2 warpedPoint = point + new Vector2(noiseX, noiseY);

	    // smoothly blend distance to spine
	    float blendedDistance = float.MaxValue;
	    float smoothBlendRadius = 1200f; 

	    foreach (var spine in TectonicSpines)
	    {
		    float distToThisSpine = DistanceToSpine(point, spine);
		    if (blendedDistance == float.MaxValue)
		    {
			    blendedDistance = distToThisSpine;
		    }
		    else
		    {
			    blendedDistance = SmoothMin(blendedDistance, distToThisSpine, smoothBlendRadius);
		    }
	    }
	    
	    // define thresholds for geographic features with a relationship to the distanceToSpine
	    float mountainRadius = 600f;    // Extremely close to spine = High Mountains (Up to 1.0)
	    float landRadius = 2800f;       // Close to spine = Landmass (0.0 to 0.7)
	    float oceanRadius = 4500f;      // Fading out = Ocean Floor (-1.0 to 0.0)
	    
	    float baseElevation = -1.0f; // Default to Deep Ocean Floor

	    if (blendedDistance < mountainRadius)
	    {
		    // Smoothly scale from 0.6 (high hills) up to 1.0 (mountain peaks) near the spine
		    float t = 1f - (blendedDistance / mountainRadius);
		    baseElevation = MathX.Lerp(0.6f, 1.0f, t);
	    }
	    else if (blendedDistance < landRadius)
	    {
		    // Smoothly scale from 0.0 (sea level shore) up to 0.6 (hills)
		    float t = 1f - ((blendedDistance - mountainRadius) / (landRadius - mountainRadius));
		    baseElevation = MathX.Lerp(0.0f, 0.6f, t);
	    }
	    else if (blendedDistance < oceanRadius)
	    {
		    // Smoothly scale from -1.0 (deep ocean floor) up to 0.0 (sea level shore)
		    float t = 1f - ((blendedDistance - landRadius) / (oceanRadius - landRadius));
		    baseElevation = MathX.Lerp(-1.0f, 0.0f, t);
	    }

	    // force edges to fade smoothly into the deep ocean floor (-1.0)
	    float edgeFalloff = 1f - (point.Length / Settings.HalfWidth);
	    edgeFalloff = SmoothStep(0f, 1f, edgeFalloff);

	    // Lerp down to -1.0f as the map edge approaches
	    return MathX.Lerp(-1.0f, baseElevation, edgeFalloff);
    }
    
    // @TODO: move to math utility
    public float SmoothStep( float edge0, float edge1, float x )
    {
	    // Clamp and normalise x between 0.0 and 1.0
	    float t = Math.Clamp( ( x - edge0 ) / ( edge1 - edge0 ), 0.0f, 1.0f );
        
	    // Evaluate the cubic Hermite polynomial
	    return t * t * ( 3.0f - 2.0f * t );
    }
}
