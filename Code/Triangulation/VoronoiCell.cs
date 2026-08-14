// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation;

public struct VoronoiCell : IVoronoiCell
{
	public IPoint[] Points { get; set; }
	public int Index { get; set; }
	public VoronoiCell(int triangleIndex, IPoint[] points)
	{
		this.Points = points;
		this.Index = triangleIndex;
	}
}
