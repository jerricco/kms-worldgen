// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation
{
	public struct Triangle : ITriangle
	{
		public int Index { get; set; }

		public IEnumerable<IPoint> Points { get; set; }

		public Triangle(int t, IEnumerable<IPoint> points)
		{
			Points = points;
			Index = t;
		}
	}
}
