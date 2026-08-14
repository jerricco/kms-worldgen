// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation;

public struct Edge : IEdge
{
	public IPoint P { get; set; }
	public IPoint Q { get; set; }
	public int Index { get; set; }

	public Edge(int e, IPoint p, IPoint q)
	{
		this.Index = e;
		this.P = p;
		this.Q = q;
	}
}
