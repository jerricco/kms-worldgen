using System;

namespace Sandbox.GameData;

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
    [Property] public int CellGridColumns => WorldHeight / CellGridSize; // e.g., 12800 / 400 = 32
    [Property] public int CellGridRows => WorldWidth / CellGridSize; 
    [Property] public int ChunkGridSize { get; set; } = 50;
    [Property] public int ChunksX => HalfWidth / ChunkGridSize;
    [Property] public int ChunksY => HalfHeight / ChunkGridSize;
    [Property] public int TotalChunks => ChunksX * ChunksY;
    
    // Map generation modifiers
    [Property] public float OceanClamp  { get; set; } = 0.85f;
    [Property] public float MacroScale  { get; set; } = 0.00015f;
    [Property] public float MicroScale  { get; set; } = 0.0015f;
    [Property] public float SquishFactor{ get; set; } = 1.0f;
    [Property] public float StretchX    { get; set; } = 0.7f;
    [Property] public float StretchY    { get; set; } = 1.3f;

    // Elevation boundaries
    [Property] public float AbyssalLevel { get; set; } = -256f;
    [Property] public float TrenchLevel  { get; set; } = -218f;
    [Property] public float DeepOceanLevel { get; set; } = -140f;
    [Property] public float OceanLevel { get; set; } = -64f;
    [Property] public float SeaLevel { get; set; } = 0f;
    [Property] public float BeachLevel { get; set; } = 8f;
    [Property] public float PlainLevel { get; set; } = 124f;
    [Property] public float HillLevel { get; set; } = 175f;
    [Property] public float MountainLevel { get; set; } = 210f;
    [Property] public float PeakLevel { get; set; } = 245f;


    // --- COMPUTED PROPERTIES --- //
    // dimensionality
    [Property] public int TotalTiles => WorldWidth * WorldHeight;
    [Property] public int MaxDimension => Math.Max(WorldWidth, WorldHeight);
    [Property] public float MaxRadius => MathF.Sqrt( (HalfWidth * HalfWidth) + (HalfHeight * HalfHeight) );
    [Property] public int HalfWidth => WorldWidth / 2;
    [Property] public int HalfHeight => WorldHeight / 2;
    
    // resource visual icon
    protected override Bitmap CreateAssetTypeIcon( int width, int height )
    {
	    return CreateSimpleAssetTypeIcon( "landscape", width, height, "#fdea60", "black" );
    }
}
