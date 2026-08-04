// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation
{
	public interface ITriangle
	{
		IEnumerable<IPoint> Points { get; }
		int Index { get; }
	}
}
