namespace Sandbox.GameData;

[AssetType(Name = "Chunk Theme", Extension = "gentheme", Category = "Theme")]
public class ChunkTheme : GameResource
{
	// resource visual icon
	protected override Bitmap CreateAssetTypeIcon(int width, int height)
        => CreateSimpleAssetTypeIcon("palette", width, height, "#9ec2e2", "black");
}
