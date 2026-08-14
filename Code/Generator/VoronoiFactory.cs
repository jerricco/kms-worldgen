namespace Sandbox.Generator;

using System;
using Extensions;
using GameData;
using Sandbox.GameObjectSystems.Map;
using Rendering;
using Triangulation;

[Category("Procedural Generation")]
public sealed class VoronoiFactory : Component
{
    [Property]
    public GenerationSettings Settings { get; set; }

    [Property]
    public Material LineMaterial { get; set; }

    public ProceduralDelaunayRenderer Renderer { get; set; }

    public List<CurvedSpine>? TectonicSpines { get; private set; }

    // Cache object for handling fast voronoi/delaunay data spatial lookup
    private static readonly Dictionary<Vector2, List<(int index, Vector2 pos)>> PointGridBuckets = new();

    private Vector2[]? _cachedCellCenters;
    private Delaunator? _delaunay;
    private Voronator? _voronoi;

    protected override void OnStart()
    {
        // create mesh for drawing the voronoi sites - @TODO: default to false visibility when done with
        this.Renderer = this.GameObject.GetOrAddComponent<ProceduralDelaunayRenderer>();
    }

    protected override void OnDestroy()
    {
        this.ClearData();
    }

    public void GenerateAndRender()
    {
        var startTime = RealTime.Now;

        this.TectonicSpines = this.GenerateTectonicNetwork();
        var points = this.SampleDensityPoints();
        this._voronoi = new Voronator(points, -this.Settings.HalfWidth, this.Settings.HalfWidth);
        this._delaunay = this._voronoi.Delaunator;
        this.CacheCellCenters();

        Log.Info($"Voronoi generation complete! Took {RealTime.Now - startTime} s");
        this.Renderer.Settings = this.Settings;
        this.Renderer.LineMaterial = this.LineMaterial;
        this.Renderer.RebuildMesh(this._delaunay);

        Log.Info($"Voronoi mesh complete! Took {RealTime.Now - startTime} s");
    }

    public void ClearData()
    {
        if (this.Renderer is { IsValid: true }) this.Renderer.ClearMesh();

        this.TectonicSpines = null;
        this._voronoi = null;
        this._delaunay = null;
    }

    ///////////////////////////////////////////////
    //              Spatial Lookup               //
    ///////////////////////////////////////////////
    /// <summary>
    /// generates an index cache for looking up voronoi cell centers in a 1-D array
    /// </summary>
    private void CacheCellCenters()
    {
        if (this._voronoi == null)
            throw new InvalidOperationException("_voronoi is not expected as null");

        PointGridBuckets.Clear();
        this._cachedCellCenters = this._voronoi.GetRelaxedPoints().ToArray();
        for (var i = 0; i < this._cachedCellCenters.Length; i++)
        {
            var pos = this._cachedCellCenters[i];

            var gridX = (pos.x / this.Settings.CellGridSize).FloorToInt();
            var gridY = (pos.y / this.Settings.CellGridSize).FloorToInt();
            var bucketKey = new Vector2(gridX, gridY);

            if (!PointGridBuckets.TryGetValue(bucketKey, out var value))
            {
                value = [];
                PointGridBuckets[bucketKey] = value;
            }

            value.Add((i, pos));
        }
    }

    private List<CurvedSpine> GenerateTectonicNetwork()
    {
        var mapManager = MapGeneratorSystem.Current;
        if (mapManager.Rng == null)
            throw new InvalidOperationException("Rng on mapManager is null!");

        var networks = new List<CurvedSpine>();
        var continentCount = mapManager.Rng.NextRange(3, 6);
        for (var c = 0; c < continentCount; c++)
        {
            var spine = new CurvedSpine
            {
                Nodes = [],
            };
            var nodeCount = mapManager.Rng.NextRange(4, 6);

            var scatterRange = this.Settings.HalfWidth * 0.4f;
            var continentCenter = new Vector2(
                mapManager.Rng.NextRangeFloat(-scatterRange, scatterRange),
                mapManager.Rng.NextRangeFloat(-scatterRange, scatterRange)
            );

            // Give each continent its own unique orientation vector
            var baseAngle = mapManager.Rng.NextRangeFloat(0, 360);
            var direction = Vector2.FromDegrees(baseAngle);
            var perpendicular = new Vector2(-direction.y, direction.x);

            var innerRegion = this.Settings.HalfWidth * 0.25f;// Length of this specific spine
            var startSpan = -innerRegion;
            var endSpan = innerRegion;
            var stepSize = (endSpan - startSpan) / (nodeCount - 1);

            for (var i = 0; i < nodeCount; i++)
            {
                var progress = startSpan + (i * stepSize);
                var basePoint = continentCenter + (direction * progress);

                var varianceScale = this.Settings.HalfWidth * 0.1f;
                var lateralOffset = mapManager.Rng.NextRangeFloat(-varianceScale, varianceScale);
                var forwardOffset = mapManager.Rng.NextRangeFloat(-stepSize * 0.2f, stepSize * 0.2f);

                var finalizedNode = basePoint + (direction * forwardOffset) + (perpendicular * lateralOffset);
                spine.Nodes.Add(finalizedNode);
            }

            networks.Add(spine);
        }

        return networks;
    }

    /// <summary>
    /// Calculates the shortest distance from point P to line segment AB.
    /// </summary>
    private static float DistanceToSpine(Vector2 p, CurvedSpine spine)
    {
        var minDistance = float.MaxValue;
        for (var i = 0; i < spine.Nodes.Count - 1; i++)
        {
            var a = spine.Nodes[i];
            var b = spine.Nodes[i + 1];

            // Standard segment distance math
            var ab = b - a;
            var ap = p - a;
            var t = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
            t = Math.Clamp(t, 0f, 1f);

            var closestPoint = a + (t * ab);
            var distance = Vector2.Distance(p, closestPoint);

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
    private List<Vector2> SampleDensityPoints()
    {
        var mapManager = MapGeneratorSystem.Current;
        var points = new List<Vector2>();

        if (mapManager.Rng == null)
            throw new InvalidOperationException("Rng on mapManager is null!");

        // Configuration settings for your cell sizes
        const float minSeparationLand = 90f;// Crisp, tiny land cells
        const float minSeparationOcean = 950f;// Large, sweeping ocean cells
        const int chunkGridSize = 400;

        // Loop through your 400x400 grid layout across the map
        for (var x = -6400; x < 6400; x += chunkGridSize)
        {
            for (var y = -6400; y < 6400; y += chunkGridSize)
            {
                // sample positions inside each chunk boundary
                const int attemptsPerChunk = 6;
                for (var k = 0; k < attemptsPerChunk; k++)
                {
                    var sampleX = x + mapManager.Rng.NextRangeFloat(0, chunkGridSize);
                    var sampleY = y + mapManager.Rng.NextRangeFloat(0, chunkGridSize);
                    var candidate = new Vector2(sampleX, sampleY);

                    // Fetch our 0.0 - 1.0 land density ranking
                    var density = this.GetContinuousElevationAt(candidate);

                    // Map the density smoothly to a local minimum distance radius
                    var requiredRadius = MathX.Lerp(minSeparationOcean, minSeparationLand, density);

                    // Verify if the candidate respects surrounding neighbors
                    var positionIsValid = true;
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
        points.Add(new Vector2(-this.Settings.HalfWidth, -this.Settings.HalfWidth));
        points.Add(new Vector2(this.Settings.HalfWidth, -this.Settings.HalfWidth));
        points.Add(new Vector2(-this.Settings.HalfWidth, this.Settings.HalfWidth));
        points.Add(new Vector2(this.Settings.HalfWidth, this.Settings.HalfWidth));

        return points;
    }

    /// <summary>
    /// Evaluates a point against all tectonic spines to create a multi-continental system.
    /// </summary>
    public float GetContinuousElevationAt(Vector2 point)
    {
        var MapManager = MapGeneratorSystem.Current;

        if (MapManager.Noise == null)
            throw new InvalidOperationException("Noise on MapManager is null!");

        // apply noise distortion to the sample point for organic coastlines
        const float noiseScale = 0.0005f;
        const float noiseStrength = 800f;

        var tPos = new Vector2(point.x + MapManager.OffsetX, point.y + MapManager.OffsetY);
        var rPos = new Vector2(
            (float)((tPos.x * MapManager.CosA) - (tPos.y * MapManager.SinA)),
            (float)((tPos.x * MapManager.SinA) - (tPos.y * MapManager.CosA))
        );
        var sPos = new Vector2(rPos.x * noiseScale, rPos.y * noiseScale);

        var noiseX = (float)MapManager.Noise.Evaluate(sPos.x, sPos.y) * noiseStrength;
        var noiseY = (float)MapManager.Noise.Evaluate(sPos.y, sPos.x) * noiseStrength;
        var warpedPoint = point + new Vector2(noiseX, noiseY);

        // smoothly blend distance to spine
        var blendedDistance = float.MaxValue;
        const float smoothBlendRadius = 1200f;

        foreach (var spine in this.TectonicSpines)
        {
            var distToThisSpine = DistanceToSpine(warpedPoint, spine);
            if (blendedDistance == float.MaxValue)
            {
                blendedDistance = distToThisSpine;
            }
            else
            {
                blendedDistance = blendedDistance.SmoothMin(distToThisSpine, smoothBlendRadius);
            }
        }

        // define thresholds for geographic features with a relationship to the distanceToSpine
        const float mountainRadius = 600f;// Extremely close to spine = High Mountains (Up to 1.0)
        const float landRadius = 2800f;// Close to spine = Landmass (0.0 to 0.7)
        const float oceanRadius = 4500f;// Fading out = Ocean Floor (-1.0 to 0.0)
        var baseElevation = -1.0f;// Default to Deep Ocean Floor

        if (blendedDistance < mountainRadius)
        {
            // Smoothly scale from 0.6 (high hills) up to 1.0 (mountain peaks) near the spine
            var t = 1f - (blendedDistance / mountainRadius);
            baseElevation = MathX.Lerp(0.6f, 1.0f, t);
        }
        else if (blendedDistance < landRadius)
        {
            // Smoothly scale from 0.0 (sea level shore) up to 0.6 (hills)
            var t = 1f - ((blendedDistance - mountainRadius) / (landRadius - mountainRadius));
            baseElevation = MathX.Lerp(0.0f, 0.6f, t);
        }
        else if (blendedDistance < oceanRadius)
        {
            // Smoothly scale from -1.0 (deep ocean floor) up to 0.0 (sea level shore)
            var t = 1f - ((blendedDistance - landRadius) / (oceanRadius - landRadius));
            baseElevation = MathX.Lerp(-1.0f, 0.0f, t);
        }

        // force edges to fade smoothly into the deep ocean floor (-1.0)
        var edgeFalloff = 1f - (point.Length / this.Settings.HalfWidth);
        edgeFalloff = edgeFalloff.SmoothStep(0f, 1f);

        // Lerp down to -1.0f as the map edge approaches
        return MathX.Lerp(-1.0f, baseElevation, edgeFalloff);
    }

    ///////////////////////////////////////////////
    //            Landmass Generation            //
    ///////////////////////////////////////////////
    // Structure to hold our spine data
    public struct CurvedSpine
    {
        public List<Vector2> Nodes { get; set; }
    }
}
