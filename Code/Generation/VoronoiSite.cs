using Sandbox;

namespace Aeons;

public record VoronoiSite
{
    int ID { get; set; }
    Vector2 Position { get; set; }
    int PlateID { get; set; }
    bool IsOceanic { get; set; }
    float BaseElevation { get; set; }
}