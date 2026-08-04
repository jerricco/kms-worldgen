using Sandbox;
using System;

namespace Aeons;

public sealed class VoronoiFactory
{
    private GenerationSettings Settings { get; set; }
    private MapGenerator Generator { get; set; }

    private double ContinentalFragmentationFactor { get; set; }
    private double MacroBayFrequency { get; set; }
    private double MacroBayIntensity { get; set; }

    private List<VoronoiSite> Sites;
    private List<Vector2> PlateCenters;
    private List<double> PlateElevationBiases;
    private List<Delaunay.Triangle> DelaunayMesh;

    public VoronoiFactory(GenerationSettings GenSettings, MapGenerator Gen)
    {
        Settings = GenSettings;
        Generator = Gen;
    }

    public void Generate()
    {
        BuildTectonicSpine();
        BuildVoronoiSites();
        BuildDelaunay(); // @DEBUG
    }

    /**
     * PASS 1: THE MACRO TECTIONIC SPINE
     * Generates a linear, curved skeletal structure across the map space
     * to group separate land masses into long continental systems like the Americas.
    */
    private void BuildTectonicSpine()
    {
        PlateCenters = [];
        PlateElevationBiases = [];

        ContinentalFragmentationFactor = Generator.Rng.NextRangeDouble(0.35d, 0.60d);
        MacroBayFrequency = Generator.Rng.NextRangeDouble(0.002d, 0.005d);
        MacroBayIntensity = Generator.Rng.NextRangeDouble(0.20d, 0.35d);

        // @DEBUG
        Log.Info($"Tectonic Spine Settings: {ContinentalFragmentationFactor}, {MacroBayFrequency}, {MacroBayIntensity}");

        int tectonicPlateCount = Generator.Rng.NextRange(6, 9);
        double spineAngle = Generator.Rng.NextRangeDouble(0d, Math.PI * 2d);
        double spineDirectionX = Math.Cos(spineAngle);
        double spineDirectionY = Math.Sin(spineAngle);

        for (int p = 0; p < tectonicPlateCount; p++)
        {

	        double progress = (p / (float)tectonicPlateCount - 1d) * 2.0d - 1.0d;
	        double bowIntensity = Settings.MaxDimension * 0.18d;
	        double bowNoise = Math.Sin(progress * Math.PI) * bowIntensity;

            double px = (spineDirectionX * progress * Settings.HalfWidth * 0.6d) + (-spineDirectionY * bowNoise);
            double py = (spineDirectionY * progress * Settings.HalfHeight * 0.6d) + (spineDirectionX * bowNoise);
            
            Vector2 platePosition = new Vector2((float)px, (float)py);
            double plateElevationBias = Generator.Rng.NextRangeDouble(-0.15d, 0.45d);

            PlateCenters.Add(platePosition);
            PlateElevationBiases.Add(plateElevationBias);
        }

        Log.Info("Tectonic Spine Generated");
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
        for (int p = 0; p < PlateCenters.Count; p++)
        {
            double dx = x - PlateCenters[p].x;
            double dy = y - PlateCenters[p].y;
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
        // @TODO These two values should be a setting in GenerationSettings??
        // - 30 -> Settings.MinVoronoiGridSize
        // - Settings.ChunkGridSize ->(add) Settings.VoronoiGridSize
        int baseSpacing = Math.Max(30, Settings.ChunkGridSize);
        int targetPoints = Settings.WorldWidth * Settings.WorldHeight / (baseSpacing * baseSpacing);

        int siteIdCounter = 0;
        int attempts = 0;
        int maxAttempts = targetPoints * 12;

        while (Sites.Count < targetPoints && attempts < maxAttempts)
        {
	        attempts++;

            long rotX = -Settings.HalfWidth + (Generator.Rng.NextUInt() * Settings.WorldWidth);
            long rotY = -Settings.HalfHeight + (Generator.Rng.NextUInt() * Settings.WorldHeight);
            (double landChance, int closestPlateId) densityField = EvaluateGeologicalField(rotX, rotY);
            double acceptanceProbability = double.Lerp(0.012d, 1.0d, Math.Pow(densityField.landChance, 1.2d));

            if (Generator.Rng.NextUInt() > acceptanceProbability) continue;

            // twist displacement
            double twistFrequency = 1.0 / (baseSpacing * 5.0);
            double twistAngle = Generator.Noise.Evaluate(rotX * twistFrequency, rotY * twistFrequency) * Math.PI * 2d;
            double twistIntensity = baseSpacing * 0.7d * (1.0d - densityField.landChance);

            double finalX = rotX + Math.Cos(twistAngle) * twistIntensity;
            double finalY = rotY + Math.Sin(twistAngle) * twistIntensity;

            if (finalX < -Settings.HalfWidth || finalX > Settings.HalfWidth || finalY < -Settings.HalfHeight || finalY > Settings.HalfHeight)
            {
                continue;
            }

            (double landChance, int closestPlateId) finalField = EvaluateGeologicalField(finalX, finalY);
            bool isOceanic = finalField.landChance < 0.42d; // @TODO: I should figure out how this lever relates to other values
            double baseElevation = Settings.SeaLevel;

            if (isOceanic)
            {
                // @TODO: MaxAllowedRadius might do better as computed on GenerationSettings
                double maxAllowedRadius = Settings.HalfWidth * Settings.OceanClamp;
                double trueDist = Math.Sqrt(finalX * finalX + finalY * finalY);
                double trueRatio = Math.Max(0.0d, Math.Min(1.0d, trueDist / maxAllowedRadius));
                double trenchFactor = Math.Pow(trueRatio, 1.8d);
                // grade the ocean smoothly to Settings.AbyssalLevel
                baseElevation = double.Lerp(Settings.SeaLevel - 0.05d, Settings.AbyssalLevel, trenchFactor)
                                + (PlateElevationBiases[finalField.closestPlateId] * 0.08d);
            } else
            {
                // force values to distribute smoothly up through Settings.HillLevel and Settigns.MountainLevel
                double landProgress = (finalField.landChance - 0.42d) / 0.58d; // @TODO: ???? this feels off.
                double exponentialRise = Math.Pow(landProgress, 1.6d);
                baseElevation = double.Lerp(Settings.SeaLevel + 0.02d, Settings.PeakLevel, exponentialRise)
                                + (PlateElevationBiases[finalField.closestPlateId]) * 0.15d;
            }

            VoronoiSite localSite = new VoronoiSite()
            {
                Id = siteIdCounter++,
                Position = new Vector2((float)finalX, (float)finalY),
                PlateId = finalField.closestPlateId,
                IsOceanic = isOceanic,
                BaseElevation = Math.Max(-1.0, Math.Min(1.0, baseElevation))
            };

            Sites.Add(localSite);
        }
    }

    
    private void BuildDelaunay() {}
}
