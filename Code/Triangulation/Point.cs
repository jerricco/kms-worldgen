// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation;

public struct Point : IPoint
{
	public double X { get; set; }
	public double Y { get; set; }

	public Point(double x, double y)
	{
		this.X = x;
		this.Y = y;
	}
	public override string ToString()
	{
		return $"{this.X},{this.Y}";
	}
}
