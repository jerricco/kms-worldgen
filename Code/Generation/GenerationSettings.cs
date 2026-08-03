using System;

namespace Aeons;

[Serializable]
public sealed class GenerationSettings
{
    // --- STATIC(ISH) PROPERTIES --- //

    // Seeded randomisation
    [Property] public string SeedText       { get; set; } = "aborio rice";

    // Map dimensions
    [Property] public int WorldWidth    { get; set; } = 12800;
    [Property] public int WorldHeight   { get; set; } = 12800;
    [Property] public int CellGridSize  { get; set; } = 400;
    [Property] public float ChunkGridSize  { get; set; } = 50f;
    
    // Map generation modifiers
    [Property] public float ContinentalFragFactor  { get; set; } = 0.45f;
    [Property] public float MacroBayFrequency  { get; set; } = 0.0035f;
    [Property] public float MacroBayIntensity  { get; set; } = 0.28f;
    [Property] public float OceanClamp  { get; set; } = 0.85f;
    [Property] public float MacroScale  { get; set; } = 0.0045f;
    [Property] public float SquishFactor  { get; set; } = 1.0f;
    [Property] public float StretchX  { get; set; } = 0.7f;
    [Property] public float StretchY  { get; set; } = 1.3f;

    // Elevation boundaries
    [Property] public float AbyssalLevel { get; set; } = -1.0f;
    [Property] public float TrenchLevel { get; set; } = -0.85f;
    [Property] public float DeepOceanLevel { get; set; } = -0.55f;
    [Property] public float OceanLevel { get; set; } = -0.25f;
    [Property] public float SeaLevel { get; set; } = 0f;
    [Property] public float BeachLevel { get; set; } = 0.03f;
    [Property] public float PlainLevel { get; set; } = 0.48f;
    [Property] public float HillLevel { get; set; } = 0.68f;
    [Property] public float MointainLevel { get; set; } = 0.82f;
    [Property] public float PeakLevel { get; set; } = 0.95f;


    // --- COMPUTED PROPERTIES --- //
    // dimensionality
    [Property] public int TotalTiles => WorldWidth * WorldHeight;
    [Property] public float TotalChunks => TotalTiles / ChunkGridSize;
    [Property] public float HalfWidth => WorldWidth / 2;
    [Property] public float HalfHeight => WorldHeight / 2;

    // randomisation
    [Property] public float CosA => 0.0f;
    [Property] public float SinA => 0.0f;
    [Property] public float OffsetX => 0.0f;
    [Property] public float OffsetY => 0.0f;
}
