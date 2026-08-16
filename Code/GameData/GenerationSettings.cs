using System;

namespace Sandbox.GameData;

// @TODO move away from an AssetType to a component to hold the values for world generation randomisation
// I will need to move settings out that aren't relevant, and split SeedText into a SeedManagerSystem
[AssetType(Name = "Generation Settings", Extension = "genconf", Category = "Configuration")]
public partial class GenerationSettings : GameResource
{
    // --- STATIC(ISH) PROPERTIES --- //
    // Seeded randomisation
    [Property, Obsolete] public string SeedText   { get; set; } = "aborio rice";

    // Map dimensions
    [Property, Obsolete] public int WorldWidth    { get; set; } = 12800;
    [Property, Obsolete] public int WorldHeight   { get; set; } = 12800;
    [Property, Obsolete] public int CellGridSize  { get; set; } = 400;
    [Property, Obsolete] public int CellGridColumns => WorldHeight / CellGridSize; // e.g., 12800 / 400 = 32
    [Property, Obsolete] public int CellGridRows => WorldWidth / CellGridSize; 
    [Property, Obsolete] public int ChunkGridSize { get; set; } = 50;
    [Property, Obsolete] public int ChunksX => HalfWidth / ChunkGridSize;
    [Property, Obsolete] public int ChunksY => HalfHeight / ChunkGridSize;
    [Property, Obsolete] public int TotalChunks => ChunksX * ChunksY;
    
    // Map generation modifiers
    [Property, Obsolete] public float OceanClamp  { get; set; } = 0.85f;
    [Property, Obsolete] public float MacroScale  { get; set; } = 0.00015f;
    [Property, Obsolete] public float MicroScale  { get; set; } = 0.0015f;
    [Property, Obsolete] public float SquishFactor{ get; set; } = 1.0f;
    [Property, Obsolete] public float StretchX    { get; set; } = 0.7f;
    [Property, Obsolete] public float StretchY    { get; set; } = 1.3f;

    // Elevation boundaries
    [Property, Obsolete] public float AbyssalLevel { get; set; } = -256f;
    [Property, Obsolete] public float TrenchLevel  { get; set; } = -218f;
    [Property, Obsolete] public float DeepOceanLevel { get; set; } = -140f;
    [Property, Obsolete] public float OceanLevel { get; set; } = -64f;
    [Property, Obsolete] public float SeaLevel { get; set; } = 0f;
    [Property, Obsolete] public float BeachLevel { get; set; } = 8f;
    [Property, Obsolete] public float PlainLevel { get; set; } = 124f;
    [Property, Obsolete] public float HillLevel { get; set; } = 175f;
    [Property, Obsolete] public float MountainLevel { get; set; } = 210f;
    [Property, Obsolete] public float PeakLevel { get; set; } = 245f;


    // --- COMPUTED PROPERTIES --- //
    // dimensionality
    [Property, Obsolete] public int TotalTiles => WorldWidth * WorldHeight;
    [Property, Obsolete] public int MaxDimension => Math.Max(WorldWidth, WorldHeight);
    [Property, Obsolete] public float MaxRadius => MathF.Sqrt( (HalfWidth * HalfWidth) + (HalfHeight * HalfHeight) );
    [Property, Obsolete] public int HalfWidth => WorldWidth / 2;
    [Property, Obsolete] public int HalfHeight => WorldHeight / 2;
    
    // resource visual icon
    protected override Bitmap CreateAssetTypeIcon( int width, int height )
    {
	    return CreateSimpleAssetTypeIcon( "landscape", width, height, "#fdea60", "black" );
    }
}
