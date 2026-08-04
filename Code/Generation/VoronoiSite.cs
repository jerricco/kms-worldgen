using Sandbox;

namespace Aeons;

public record VoronoiSite
{
    public int Id { get; set; }
    public Vector2 Position { get; set; }
    public int PlateId { get; set; }
    public bool IsOceanic { get; set; }
    public double BaseElevation { get; set; }
}
