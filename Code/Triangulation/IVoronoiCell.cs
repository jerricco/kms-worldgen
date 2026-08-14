// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation;

public interface IVoronoiCell
{
	IPoint[] Points { get; }
	int Index { get; }
}
