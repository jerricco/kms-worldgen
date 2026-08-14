using System;

// Utilising https://github.com/nol1fe/delaunator-sharp
// Credit to library author
namespace Sandbox.Triangulation;

public class Delaunator
{
	private readonly int[] EDGE_STACK = new int[512];
	private readonly double EPSILON = Math.Pow(2, -52);
	private readonly float[] coords;

	private readonly float cx;
	private readonly float cy;

	private readonly int hashSize;
	private readonly int[] hullHash;
	private readonly int[] hullNext;
	private readonly int[] hullPrev;
	private readonly int hullSize;
	private readonly int hullStart;
	private readonly int[] hullTri;

	private int trianglesLen;

	public Delaunator(IList<Vector2> points)
	{
		this.Points = points;
		this.coords = new float[this.Points.Count * 2];

		for (var i = 0; i < this.Points.Count; i++)
		{
			var p = this.Points[i];
			this.coords[2 * i] = p.x;
			this.coords[2 * i + 1] = p.y;
		}

		var n = points.Count;
		var maxTriangles = Math.Max(2 * n - 5, 0);

		this.Triangles = new int[maxTriangles * 3];

		this.Halfedges = new int[maxTriangles * 3];
		this.hashSize = (int)Math.Ceiling(Math.Sqrt(n));

		this.hullPrev = new int[n];
		this.hullNext = new int[n];
		this.hullTri = new int[n];
		this.hullHash = new int[this.hashSize];

		var ids = new int[n];

		var minX = float.PositiveInfinity;
		var minY = float.PositiveInfinity;
		var maxX = float.NegativeInfinity;
		var maxY = float.NegativeInfinity;

		for (var i = 0; i < n; i++)
		{
			var x = this.coords[2 * i];
			var y = this.coords[2 * i + 1];
			if (x < minX)
			{
				minX = x;
			}

			if (y < minY)
			{
				minY = y;
			}

			if (x > maxX)
			{
				maxX = x;
			}

			if (y > maxY)
			{
				maxY = y;
			}

			ids[i] = i;
		}

		var cx = (minX + maxX) / 2;
		var cy = (minY + maxY) / 2;

		var minDist = double.PositiveInfinity;
		var i0 = 0;
		var i1 = 0;
		var i2 = 0;

		// pick a seed point close to the center
		for (var i = 0; i < n; i++)
		{
			var d = Dist(cx, cy, this.coords[2 * i], this.coords[2 * i + 1]);
			if (d < minDist)
			{
				i0 = i;
				minDist = d;
			}
		}

		var i0x = this.coords[2 * i0];
		var i0y = this.coords[2 * i0 + 1];

		minDist = double.PositiveInfinity;

		// find the point closest to the seed
		for (var i = 0; i < n; i++)
		{
			if (i == i0)
			{
				continue;
			}

			var d = Dist(i0x, i0y, this.coords[2 * i], this.coords[2 * i + 1]);
			if (d < minDist && d > 0)
			{
				i1 = i;
				minDist = d;
			}
		}

		var i1x = this.coords[2 * i1];
		var i1y = this.coords[2 * i1 + 1];

		var minRadius = double.PositiveInfinity;

		// find the third point which forms the smallest circumcircle with the first two
		for (var i = 0; i < n; i++)
		{
			if (i == i0 || i == i1)
			{
				continue;
			}

			var r = Circumradius(i0x, i0y, i1x, i1y, this.coords[2 * i], this.coords[2 * i + 1]);
			if (r < minRadius)
			{
				i2 = i;
				minRadius = r;
			}
		}

		var i2x = this.coords[2 * i2];
		var i2y = this.coords[2 * i2 + 1];

		if (minRadius == double.PositiveInfinity)
		{
			// All points are collinear (or there's only 1 or 2 points)
			// order collinear points by dx (or dy if all x are identical)
			// and return the list as a hull
			var primaryCoords = new double[n];
			for (var i = 0; i < n; i++)
			{
				primaryCoords[i] = this.coords[2 * i] - this.coords[0] == 0 ? this.coords[2 * i + 1] - this.coords[1] : this.coords[2 * i] - this.coords[0];
			}

			Quicksort(ids, primaryCoords, 0, n - 1);
			// Trim duplicate points from hull
			var hull = new int[n];
			var j = 0;
			var d0 = double.NegativeInfinity;
			for (var i = 0; i < n; i++)
			{
				var id = ids[i];
				if (primaryCoords[id] > d0)
				{
					hull[j++] = id;
					d0 = primaryCoords[id];
				}
			}

			this.Hull = new ArraySegment<int>(hull, 0, j).ToArray();
			this.Triangles = new int[0];
			this.Halfedges = new int[0];
			return;
		}

		if (Orient(i0x, i0y, i1x, i1y, i2x, i2y))
		{
			var i = i1;
			var x = i1x;
			var y = i1y;
			i1 = i2;
			i1x = i2x;
			i1y = i2y;
			i2 = i;
			i2x = x;
			i2y = y;
		}

		var center = Circumcenter(i0x, i0y, i1x, i1y, i2x, i2y);
		this.cx = center.x;
		this.cy = center.y;

		var dists = new double[n];
		for (var i = 0; i < n; i++)
		{
			dists[i] = Dist(this.coords[2 * i], this.coords[2 * i + 1], center.x, center.y);
		}

		// sort the points by distance from the seed triangle circumcenter
		Quicksort(ids, dists, 0, n - 1);

		// set up the seed triangle as the starting hull
		this.hullStart = i0;
		this.hullSize = 3;

		this.hullNext[i0] = this.hullPrev[i2] = i1;
		this.hullNext[i1] = this.hullPrev[i0] = i2;
		this.hullNext[i2] = this.hullPrev[i1] = i0;

		this.hullTri[i0] = 0;
		this.hullTri[i1] = 1;
		this.hullTri[i2] = 2;

		this.hullHash[this.HashKey(i0x, i0y)] = i0;
		this.hullHash[this.HashKey(i1x, i1y)] = i1;
		this.hullHash[this.HashKey(i2x, i2y)] = i2;

		this.trianglesLen = 0;
		this.AddTriangle(i0, i1, i2, -1, -1, -1);

		double xp = 0;
		double yp = 0;

		for (var k = 0; k < ids.Length; k++)
		{
			var i = ids[k];
			var x = this.coords[2 * i];
			var y = this.coords[2 * i + 1];

			// skip near-duplicate points
			if (k > 0 && Math.Abs(x - xp) <= this.EPSILON && Math.Abs(y - yp) <= this.EPSILON)
			{
				continue;
			}

			xp = x;
			yp = y;

			// skip seed triangle points
			if (i == i0 || i == i1 || i == i2)
			{
				continue;
			}

			// find a visible edge on the convex hull using edge hash
			var start = 0;
			for (var j = 0; j < this.hashSize; j++)
			{
				var key = this.HashKey(x, y);
				start = this.hullHash[(key + j) % this.hashSize];
				if (start != -1 && start != this.hullNext[start])
				{
					break;
				}
			}

			start = this.hullPrev[start];
			var e = start;
			var q = this.hullNext[e];

			while (!Orient(x, y, this.coords[2 * e], this.coords[2 * e + 1], this.coords[2 * q], this.coords[2 * q + 1]))
			{
				e = q;
				if (e == start)
				{
					e = int.MaxValue;
					break;
				}

				q = this.hullNext[e];
			}

			if (e == int.MaxValue)
			{
				continue;// likely a near-duplicate point; skip it
			}

			// add the first triangle from the point
			var t = this.AddTriangle(e, i, this.hullNext[e], -1, -1, this.hullTri[e]);

			// recursively flip triangles from the point until they satisfy the Delaunay condition
			this.hullTri[i] = this.Legalize(t + 2);
			this.hullTri[e] = t;// keep track of boundary triangles on the hull
			this.hullSize++;

			// walk forward through the hull, adding more triangles and flipping recursively
			var next = this.hullNext[e];
			q = this.hullNext[next];

			while (Orient(x, y, this.coords[2 * next], this.coords[2 * next + 1], this.coords[2 * q], this.coords[2 * q + 1]))
			{
				t = this.AddTriangle(next, i, q, this.hullTri[i], -1, this.hullTri[next]);
				this.hullTri[i] = this.Legalize(t + 2);
				this.hullNext[next] = next;// mark as removed
				this.hullSize--;
				next = q;

				q = this.hullNext[next];
			}

			// walk backward from the other side, adding more triangles and flipping
			if (e == start)
			{
				q = this.hullPrev[e];

				while (Orient(x, y, this.coords[2 * q], this.coords[2 * q + 1], this.coords[2 * e], this.coords[2 * e + 1]))
				{
					t = this.AddTriangle(q, i, e, -1, this.hullTri[e], this.hullTri[q]);
					this.Legalize(t + 2);
					this.hullTri[q] = t;
					this.hullNext[e] = e;// mark as removed
					this.hullSize--;
					e = q;

					q = this.hullPrev[e];
				}
			}

			// update the hull indices
			this.hullStart = this.hullPrev[i] = e;
			this.hullNext[e] = this.hullPrev[next] = i;
			this.hullNext[i] = next;

			// save the two new edges in the hash table
			this.hullHash[this.HashKey(x, y)] = i;
			this.hullHash[this.HashKey(this.coords[2 * e], this.coords[2 * e + 1])] = e;
		}

		this.Hull = new int[this.hullSize];
		var s = this.hullStart;
		for (var i = 0; i < this.hullSize; i++)
		{
			this.Hull[i] = s;
			s = this.hullNext[s];
		}

		this.hullPrev = this.hullNext = this.hullTri = null;// get rid of temporary arrays

		//// trim typed triangle mesh arrays
		this.Triangles = this.Triangles.Take(this.trianglesLen).ToArray();
		this.Halfedges = this.Halfedges.Take(this.trianglesLen).ToArray();
	}

	/// <summary>
	/// One value per half-edge, containing the point index of where a given half edge starts.
	/// Half-edges are stored in triplets for each triangle in the triangulation,
	/// so this is essentially a list of triangle points flattened into a single array.
	/// </summary>
	public int[] Triangles { get; }

	/// <summary>
	/// One value per half-edge, containing the opposite half-edge in the adjacent triangle, or -1 if there is no adjacent
	/// triangle
	/// </summary>
	public int[] Halfedges { get; }

	/// <summary>
	/// The initial points Delaunator was constructed with.
	/// </summary>
	public IList<Vector2> Points { get; }

	/// <summary>
	/// A list of point indices that traverses the hull of the points.
	/// </summary>
	public int[] Hull { get; }

	#region CreationLogic

	private int Legalize(int a)
	{
		var i = 0;
		int ar;

		// recursion eliminated with a fixed-size stack
		while (true)
		{
			var b = this.Halfedges[a];

			/* if the pair of triangles doesn't satisfy the Delaunay condition
			 * (p1 is inside the circumcircle of [p0, pl, pr]), flip them,
			 * then do the same check/flip recursively for the new pair of triangles
			 *
			 *           pl                    pl
			 *          /||\                  /  \
			 *       al/ || \bl            al/    \a
			 *        /  ||  \              /      \
			 *       /  a||b  \    flip    /___ar___\
			 *     p0\   ||   /p1   =>   p0\---bl---/p1
			 *        \  ||  /              \      /
			 *       ar\ || /br             b\    /br
			 *          \||/                  \  /
			 *           pr                    pr
			 */
			var a0 = a - a % 3;
			ar = a0 + (a + 2) % 3;

			if (b == -1)
			{
				// convex hull edge
				if (i == 0)
				{
					break;
				}

				a = this.EDGE_STACK[--i];
				continue;
			}

			var b0 = b - b % 3;
			var al = a0 + (a + 1) % 3;
			var bl = b0 + (b + 2) % 3;

			var p0 = this.Triangles[ar];
			var pr = this.Triangles[a];
			var pl = this.Triangles[al];
			var p1 = this.Triangles[bl];

			var illegal = InCircle(
				this.coords[2 * p0],
				this.coords[2 * p0 + 1],
				this.coords[2 * pr],
				this.coords[2 * pr + 1],
				this.coords[2 * pl],
				this.coords[2 * pl + 1],
				this.coords[2 * p1],
				this.coords[2 * p1 + 1]
			);

			if (illegal)
			{
				this.Triangles[a] = p1;
				this.Triangles[b] = p0;

				var hbl = this.Halfedges[bl];

				// edge swapped on the other side of the hull (rare); fix the halfedge reference
				if (hbl == -1)
				{
					var e = this.hullStart;
					do
					{
						if (this.hullTri[e] == bl)
						{
							this.hullTri[e] = a;
							break;
						}

						e = this.hullPrev[e];
					}
					while (e != this.hullStart);
				}

				this.Link(a, hbl);
				this.Link(b, this.Halfedges[ar]);
				this.Link(ar, bl);

				var br = b0 + (b + 1) % 3;

				// don't worry about hitting the cap: it can only happen on extremely degenerate input
				if (i < this.EDGE_STACK.Length)
				{
					this.EDGE_STACK[i++] = br;
				}
			}
			else
			{
				if (i == 0)
				{
					break;
				}

				a = this.EDGE_STACK[--i];
			}
		}

		return ar;
	}
	private static bool InCircle(
		double ax,
		double ay,
		double bx,
		double by,
		double cx,
		double cy,
		double px,
		double py
	)
	{
		var dx = ax - px;
		var dy = ay - py;
		var ex = bx - px;
		var ey = by - py;
		var fx = cx - px;
		var fy = cy - py;

		var ap = dx * dx + dy * dy;
		var bp = ex * ex + ey * ey;
		var cp = fx * fx + fy * fy;

		return dx * (ey * cp - bp * fy) -
			dy * (ex * cp - bp * fx) +
			ap * (ex * fy - ey * fx) < 0;
	}
	private int AddTriangle(
		int i0,
		int i1,
		int i2,
		int a,
		int b,
		int c
	)
	{
		var t = this.trianglesLen;

		this.Triangles[t] = i0;
		this.Triangles[t + 1] = i1;
		this.Triangles[t + 2] = i2;

		this.Link(t, a);
		this.Link(t + 1, b);
		this.Link(t + 2, c);

		this.trianglesLen += 3;
		return t;
	}
	private void Link(int a, int b)
	{
		this.Halfedges[a] = b;
		if (b != -1)
		{
			this.Halfedges[b] = a;
		}
	}
	private int HashKey(double x, double y)
	{
		return (int)(Math.Floor(PseudoAngle(x - this.cx, y - this.cy) * this.hashSize) % this.hashSize);
	}
	private static double PseudoAngle(double dx, double dy)
	{
		var p = dx / (Math.Abs(dx) + Math.Abs(dy));
		return (dy > 0 ? 3 - p : 1 + p) / 4;// [0..1]
	}
	private static void Quicksort(int[] ids, double[] dists, int left, int right)
	{
		if (right - left <= 20)
		{
			for (var i = left + 1; i <= right; i++)
			{
				var temp = ids[i];
				var tempDist = dists[temp];
				var j = i - 1;
				while (j >= left && dists[ids[j]] > tempDist)
				{
					ids[j + 1] = ids[j--];
				}

				ids[j + 1] = temp;
			}
		}
		else
		{
			var median = (left + right) >> 1;
			var i = left + 1;
			var j = right;
			Swap(ids, median, i);
			if (dists[ids[left]] > dists[ids[right]])
			{
				Swap(ids, left, right);
			}

			if (dists[ids[i]] > dists[ids[right]])
			{
				Swap(ids, i, right);
			}

			if (dists[ids[left]] > dists[ids[i]])
			{
				Swap(ids, left, i);
			}

			var temp = ids[i];
			var tempDist = dists[temp];
			while (true)
			{
				do
				{
					i++;
				}
				while (dists[ids[i]] < tempDist);

				do
				{
					j--;
				}
				while (dists[ids[j]] > tempDist);

				if (j < i)
				{
					break;
				}

				Swap(ids, i, j);
			}

			ids[left + 1] = ids[j];
			ids[j] = temp;

			if (right - i + 1 >= j - left)
			{
				Quicksort(ids, dists, i, right);
				Quicksort(ids, dists, left, j - 1);
			}
			else
			{
				Quicksort(ids, dists, left, j - 1);
				Quicksort(ids, dists, i, right);
			}
		}
	}
	private static void Swap(int[] arr, int i, int j)
	{
		var tmp = arr[i];
		arr[i] = arr[j];
		arr[j] = tmp;
	}
	private static bool Orient(
		double px,
		double py,
		double qx,
		double qy,
		double rx,
		double ry
	)
	{
		// Non-robust orientation
		//return (qy - py) * (rx - qx) - (qx - px) * (ry - qy) < 0;
		return GeometricPredicates.Orient2D(px, py, qx, qy, rx, ry) > 0;
	}
	private static double Circumradius(
		double ax,
		double ay,
		double bx,
		double by,
		double cx,
		double cy
	)
	{
		var dx = bx - ax;
		var dy = by - ay;
		var ex = cx - ax;
		var ey = cy - ay;
		var bl = dx * dx + dy * dy;
		var cl = ex * ex + ey * ey;
		var d = 0.5 / (dx * ey - dy * ex);
		var x = (ey * bl - dy * cl) * d;
		var y = (dx * cl - ex * bl) * d;
		return x * x + y * y;
	}
	private static Vector2 Circumcenter(
		float ax,
		float ay,
		float bx,
		float by,
		float cx,
		float cy
	)
	{
		var dx = bx - ax;
		var dy = by - ay;
		var ex = cx - ax;
		var ey = cy - ay;
		var bl = dx * dx + dy * dy;
		var cl = ex * ex + ey * ey;
		var d = 0.5f / (dx * ey - dy * ex);
		var x = ax + (ey * bl - dy * cl) * d;
		var y = ay + (dx * cl - ex * bl) * d;

		return new Vector2(x, y);
	}
	private static double Dist(float ax, float ay, float bx, float by)
	{
		var dx = ax - bx;
		var dy = ay - by;
		return dx * dx + dy * dy;
	}

	#endregion CreationLogic

	#region GetMethods

	/// <summary>
	/// Gets a triangle with the three points around a given triangle index.
	/// </summary>
	public Triangle GetTriangle(int t)
	{
		var vertices = this.PointsAroundTriangle(t);
		return new Triangle(t, vertices.Item1, vertices.Item2, vertices.Item3);
	}

	/// <summary>
	/// Returns the points of all triangles in the Delaunay triangulation
	/// </summary>
	public IEnumerable<Triangle> GetTriangles()
	{
		for (var t = 0; t < this.Triangles.Length / 3; t++)
		{
			yield return this.GetTriangle(t);
		}
	}

	/// <summary>
	/// Returns the pair of points for an edge by a given halfedge index.
	/// </summary>
	public (Vector2, Vector2) GetEdge(int e)
	{
		var p = this.Points[this.Triangles[e]];
		var q = this.Points[this.Triangles[NextHalfedge(e)]];
		return (p, q);
	}

	/// <summary>
	/// Returns all edges in the triangulation.
	/// Each edge is only represented once, even if there is a triangle on either side.
	/// </summary>
	public IEnumerable<(Vector2, Vector2)> GetEdges()
	{
		for (var e = 0; e < this.Triangles.Length; e++)
		{
			if (e > this.Halfedges[e])
			{
				yield return this.GetEdge(e);
			}
		}
	}

	public IEnumerable<(Vector2, Vector2)> GetHullEdges()
	{
		return CreateHull(this.GetHullPoints());
	}

	public IEnumerable<Vector2> GetHullPoints()
	{
		return this.Hull.Select(x => this.Points[x]);
	}

	public Triple<Vector2> PointsAroundTriangle(int t)
	{
		var pointIndicies = this.PointIndiciesAroundTriangle(t);
		return (this.Points[pointIndicies.Item1], this.Points[pointIndicies.Item2], this.Points[pointIndicies.Item3]
			);
	}

	public Triple<(Vector2, Vector2)> EdgesAroundTriangle(int t)
	{
		var edgeIndices = EdgeIndicesAroundTriangle(t);
		return (this.GetEdge(edgeIndices.Item1), this.GetEdge(edgeIndices.Item2), this.GetEdge(edgeIndices.Item3)
			);
	}

	private static IEnumerable<(Vector2, Vector2)> CreateHull(IEnumerable<Vector2> points)
	{
		return points.Zip(points.Skip(1).Append(points.FirstOrDefault()), (a, b) => (a, b));
	}
	public Vector2 GetTriangleCircumcenter(int t)
	{
		var vertices = this.PointsAroundTriangle(t);
		return GetCircumcenter(vertices.Item1, vertices.Item2, vertices.Item3);
	}
	public Vector2 GetCentroid(int t)
	{
		var vertices = this.PointsAroundTriangle(t);
		return GetCentroid(vertices.Item1, vertices.Item2, vertices.Item3);
	}

	public static Vector2 GetCentroid(Vector2 a, Vector2 b, Vector2 c)
	{
		return (a + b + c) / 3;
	}

	public static Vector2 GetCircumcenter(Vector2 a, Vector2 b, Vector2 c)
	{
		return Circumcenter(a.x, a.y, b.x, b.y, c.x, c.y);
	}

	#endregion GetMethods

	#region Methods based on index

	/// <summary>
	/// Returns the half-edges that share a start point with the given half edge, in order.
	/// </summary>
	internal IEnumerable<int> EdgeIndiciesAroundPoint(int halfEdge)
	{
		var incoming = halfEdge;
		do
		{
			yield return incoming;
			var outgoing = NextHalfedge(incoming);
			incoming = this.Halfedges[outgoing];
		}
		while (incoming != -1 && incoming != halfEdge);
	}

	public Triple<int> PointIndiciesAroundTriangle(int t)
	{
		var edgeIndicies = EdgeIndicesAroundTriangle(t);
		return (this.Triangles[edgeIndicies.Item1], this.Triangles[edgeIndicies.Item2], this.Triangles[edgeIndicies.Item3]
			);
	}
	public IEnumerable<int> TrianglesAdjacentToTriangle(int t)
	{
		var triangleEdges = EdgeIndicesAroundTriangle(t);
		int opposite;
		if ((opposite = this.Halfedges[triangleEdges.Item1]) >= 0)
		{
			yield return EdgeIndexToTriangleIndex(opposite);
		}

		if ((opposite = this.Halfedges[triangleEdges.Item2]) >= 0)
		{
			yield return EdgeIndexToTriangleIndex(opposite);
		}

		if ((opposite = this.Halfedges[triangleEdges.Item3]) >= 0)
		{
			yield return EdgeIndexToTriangleIndex(opposite);
		}
	}

	public static int NextHalfedge(int e)
	{
		return (e % 3 == 2) ? e - 2 : e + 1;
	}
	public static int PreviousHalfedge(int e)
	{
		return (e % 3 == 0) ? e + 2 : e - 1;
	}
	public static Triple<int> EdgeIndicesAroundTriangle(int t)
	{
		return (3 * t, 3 * t + 1, 3 * t + 2);
	}
	public static int EdgeIndexToTriangleIndex(int e) { return e / 3; }

	#endregion Methods based on index
}
