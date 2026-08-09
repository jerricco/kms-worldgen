// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation
{
	public interface IEdge
	{
		IPoint P { get; }
		IPoint Q { get; }
		int Index { get; }
	}
}
