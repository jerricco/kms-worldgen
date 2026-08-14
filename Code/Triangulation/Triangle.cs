using System.Collections;

// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation;

public struct Triangle : IEnumerable<Vector2>
{
	public int TriangleIndex;

	public Vector2 Point1;
	public Vector2 Point2;
	public Vector2 Point3;

	public Triangle(int triangleIndex, Vector2 point1, Vector2 point2, Vector2 point3)
	{
		this.TriangleIndex = triangleIndex;
		this.Point1 = point1;
		this.Point2 = point2;
		this.Point3 = point3;
	}

	public Vector2 Centroid => (this.Point1 + this.Point2 + this.Point3) / 3;
	public Vector2 Circumcenter => Delaunator.GetCircumcenter(this.Point1, this.Point2, this.Point3);

	public IEnumerator<Vector2> GetEnumerator()
	{
		yield return this.Point1;
		yield return this.Point2;
		yield return this.Point3;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		yield return this.Point1;
		yield return this.Point2;
		yield return this.Point3;
	}
}
