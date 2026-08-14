namespace Sandbox.GameData;

[AssetType(Name = "Chunk Theme", Extension = "gentheme", Category = "Theme")]
public class ChunkTheme : GameResource
{
	public Color AbyssalOcean = Color.Parse("#5e89ae") ?? Color.Red;
	public Color Beach = Color.Parse("#fffbd5") ?? Color.Red;
	public Color Cliff = Color.Parse("#b64c22") ?? Color.Red;
	public Color CrustFloor = Color.Parse("#466c8d") ?? Color.Red;
	public Color DeepOcean = Color.Parse("#749dc1") ?? Color.Red;
	public Color Desert = Color.Parse("#faf296") ?? Color.Red;
	public Color Estuary = Color.Parse("#000000") ?? Color.Red;
	public Color Forest = Color.Parse("#6fbb81") ?? Color.Red;
	public Color FreshLake = Color.Parse("#a2c4e2") ?? Color.Red;
	public Color Hill = Color.Parse("#F5D282") ?? Color.Red;
	public Color Island = Color.Parse("#fffbd5") ?? Color.Red;
	public Color Mountain = Color.Parse("#CD6414") ?? Color.Red;
	public Color Ocean = Color.Parse("#8CB4D7") ?? Color.Red;
	public Color Peak = Color.Parse("#A54119") ?? Color.Red;
	public Color Plain = Color.Parse("#FAF096") ?? Color.Red;
	public Color Plateau = Color.Parse("#facd96") ?? Color.Red;
	public Color Reef = Color.Parse("#a2e2d7") ?? Color.Red;
	public Color River = Color.Parse("#000000") ?? Color.Red;
	public Color RiverDelta = Color.Parse("#000000") ?? Color.Red;
	public Color SalineLake = Color.Parse("#a2d2e2") ?? Color.Red;
	public Color Sea = Color.Parse("#9ec2e2") ?? Color.Red;
	public Color Tundra = Color.Parse("#6f79bb") ?? Color.Red;
	public Color Unassigned = Color.Parse("#e5e5e5") ?? Color.Red;
	public Color Valley = Color.Parse("#ebfa96") ?? Color.Red;
	public Color Void = Color.Parse("#6a3887") ?? Color.Red;
	public Color Wetland = Color.Parse("#000000") ?? Color.Red;

	// resource visual icon
	protected override Bitmap CreateAssetTypeIcon(int width, int height)
	{
		return CreateSimpleAssetTypeIcon("palette", width, height, "#9ec2e2", "black");
	}
}
