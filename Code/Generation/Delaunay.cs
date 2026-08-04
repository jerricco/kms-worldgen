using System;
using System.Collections.Generic;
using Sandbox;

namespace Sandbox.Generation;

public class Delaunay
{
    public class Triangle
    {
        public Vector2 A, B, C;
        public Vector2 CircumCenter;
        public float CircumRadiusSq;

        public Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            A = a; B = b; C = c;
            CalculateCircumcircle();
        }

        private void CalculateCircumcircle()
        {
            // Determinant math to find the center and radius of the 3 points
            float d = 2 * (A.x * (B.y - C.y) + B.x * (C.y - A.y) + C.x * (A.y - B.y));
            if (MathF.Abs(d) < 0.000001f) d = 0.000001f; // Prevent division by zero

            float ux = ((A.x * A.x + A.y * A.y) * (B.y - C.y) + (B.x * B.x + B.y * B.y) * (C.y - A.y) + (C.x * C.x + C.y * C.y) * (A.y - B.y)) / d;
            float uy = ((A.x * A.x + A.y * A.y) * (C.x - B.x) + (B.x * B.x + B.y * B.y) * (A.x * C.x) + (C.x * C.x + C.y * C.y) * (B.x - A.x)) / d;

            CircumCenter = new Vector2(ux, uy);
            CircumRadiusSq = (A.x - ux) * (A.x - ux) + (A.y - uy) * (A.y - uy);
        }

        public bool ContainsInCircumcircle(Vector2 point)
        {
            float distSq = (point.x - CircumCenter.x) * (point.x - CircumCenter.x) + (point.y - CircumCenter.y) * (point.y - CircumCenter.y);
            return distSq < CircumRadiusSq;
        }

        public bool SharesVertexWith(Triangle other)
        {
            return A == other.A || A == other.B || A == other.C ||
                   B == other.A || B == other.B || B == other.C ||
                   C == other.A || C == other.B || C == other.C;
        }
    }

    public struct Edge
    {
        public Vector2 U, V;
        public Edge(Vector2 u, Vector2 v) { U = u; V = v; }
        public bool Equals(Edge other) => (U == other.U && V == other.V) || (U == other.V && V == other.U);
    }

    public static List<Triangle> Triangulate(List<Vector2> points, float width = 5000f, float height = 5000f)
    {
        var triangulation = new List<Triangle>();

        // 1. Create a super-triangle that covers the generation boundaries safely
        var stA = new Vector2(width / 2f, height * 3f);
        var stB = new Vector2(-width * 2f, -height);
        var stC = new Vector2(width * 3f, -height);
        var superTriangle = new Triangle(stA, stB, stC);
        triangulation.Add(superTriangle);

        // 2. Incrementally insert each point
        foreach (var p in points)
        {
            var badTriangles = new List<Triangle>();
            foreach (var t in triangulation)
            {
                if (t.ContainsInCircumcircle(p))
                    badTriangles.Add(t);
            }

            // Find the boundary edges of the polygonal cavity
            var polygon = new List<Edge>();
            foreach (var t in badTriangles)
            {
                var edges = new Edge[] { new Edge(t.A, t.B), new Edge(t.B, t.C), new Edge(t.C, t.A) };
                foreach (var edge in edges)
                {
                    bool shared = false;
                    foreach (var otherT in badTriangles)
                    {
                        if (t == otherT) continue;
                        if ((otherT.A == edge.U || otherT.B == edge.U || otherT.C == edge.U) &&
                            (otherT.A == edge.V || otherT.B == edge.V || otherT.C == edge.V))
                        {
                            shared = true;
                            break;
                        }
                    }
                    if (!shared) polygon.Add(edge);
                }
            }

            // Remove the bad triangles from the main mesh
            foreach (var t in badTriangles) triangulation.Remove(t);

            // Re-stitch the hole with new triangles bound to the point
            foreach (var edge in polygon)
            {
                triangulation.Add(new Triangle(edge.U, edge.V, p));
            }
        }

        // 3. Clean up any triangles attached to the external super-triangle bounds
        triangulation.RemoveAll(t => t.SharesVertexWith(superTriangle));

        return triangulation;
    }
}
