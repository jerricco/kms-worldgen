using System;

namespace Sandbox.Generation;

[AssetType(Name = "Generation Settings", Extension = "genconf", Category = "Configuration")]
public partial class GenerationSettings : GameResource
{
    // --- STATIC(ISH) PROPERTIES --- //

    // Seeded randomisation
    [Property] public string SeedText   { get; set; } = "aborio rice";

    // Map dimensions
    [Property] public int WorldWidth    { get; set; } = 12800;
    [Property] public int WorldHeight   { get; set; } = 12800;
    [Property] public int CellGridSize  { get; set; } = 400;
    [Property] public int ChunkGridSize { get; set; } = 50;
    
    // Map generation modifiers
    [Property] public float OceanClamp  { get; set; } = 0.85f;
    [Property] public float MacroScale  { get; set; } = 0.0045f;
    [Property] public float SquishFactor{ get; set; } = 1.0f;
    [Property] public float StretchX    { get; set; } = 0.7f;
    [Property] public float StretchY    { get; set; } = 1.3f;

    // Elevation boundaries
    [Property] public float AbyssalLevel { get; set; } = -1.0f;
    [Property] public float TrenchLevel  { get; set; } = -0.85f;
    [Property] public float DeepOceanLevel { get; set; } = -0.55f;
    [Property] public float OceanLevel { get; set; } = -0.25f;
    [Property] public float SeaLevel { get; set; } = 0f;
    [Property] public float BeachLevel { get; set; } = 0.03f;
    [Property] public float PlainLevel { get; set; } = 0.48f;
    [Property] public float HillLevel { get; set; } = 0.68f;
    [Property] public float MountainLevel { get; set; } = 0.82f;
    [Property] public float PeakLevel { get; set; } = 0.95f;


    // --- COMPUTED PROPERTIES --- //
    // dimensionality
    [Property] public int TotalTiles => WorldWidth * WorldHeight;
    [Property] public int MaxDimension => Math.Max(WorldWidth, WorldHeight);
    [Property] public float MaxRadius => MathF.Sqrt( (HalfWidth * HalfWidth) + (HalfHeight * HalfHeight) );
    [Property] public int TotalChunks => TotalTiles / ChunkGridSize;
    [Property] public int HalfWidth => WorldWidth / 2;
    [Property] public int HalfHeight => WorldHeight / 2;
    
    // resource visual icon
    protected override Bitmap CreateAssetTypeIcon( int width, int height )
    {
	    return CreateSimpleAssetTypeIcon( "landscape", width, height, "#fdea60", "black" );
    }
}
