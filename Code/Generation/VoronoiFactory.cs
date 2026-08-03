using Sandbox;

namespace Aeons;

public sealed class VoronoiFactory
{
    private GenerationSettings Settings { get; set; }

    private List<VoronoiSite> Sites = new();
    private List<Vector2> PlateCenters = new();
    private List<float> PlateElevationBiases = new();
    private List<Delaunay.Triangle> DelaunayMesh = new();

    public VoronoiFactory(GenerationSettings GenSettings)
    {
        Settings = GenSettings;
    }

    public void Generate()
    {
        // Sites.clear();
        // PlateCenters.clear();
        // PlateElevationBiases.clear();
        // DelaunayMesh.clear();

        BuildTectonicSpine();
        BuildVoronoiSites();
        BuildDelaunay(); // @DEBUG
    }

    private void BuildTectonicSpine() {}
    private void BuildVoronoiSites() {}
    private void BuildDelaunay() {}
}
