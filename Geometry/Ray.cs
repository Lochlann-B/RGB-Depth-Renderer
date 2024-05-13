using OpenTK.Mathematics;

namespace Geometry;

public class Ray(Vector3 source, Vector3 direction) : IRay
{
    
    public List<Vector3> IntersectMesh(Mesh mesh)
    {
        var rayHits = new List<Vector3>();
        var indices = mesh.MeshLayout.IndexArray;

        // gonna parallelise this along each triangle
        for (int i = 0; i < indices.Length; i += 3)
        {
            // gonna put this bit in the mesh class and have a method which produces a struct of triangles
            // Make triangle using positions and indices
            var triangleVertices = new List<Vector3>();
            
            for (int j = i; j < i + 3; j++)
            {
                triangleVertices.Add(new Vector3(
                    mesh.VertexPositions[3*indices[j]],
                    mesh.VertexPositions[3*indices[j]+1],
                    mesh.VertexPositions[3*indices[j]+2]
                ));
            }
        
            var triangle = new Triangle(triangleVertices[0], triangleVertices[1], triangleVertices[2]);
            
            // gonna put this bit in the kernel
            // See if ray intersect triangle
            
            // Check 1: If ray and normal are perpendicular, no intersection.
            var n = triangle.GetNormal();
            if (Math.Abs(Vector3.Dot(n, Direction)) < 1e-6)
            {
                continue;
            }
            
            // Now find the equation of the plane that the triangle lies within.
            
            // Plane is of form N . x - D = 0, where x is a point on the plane.
            var d = Vector3.Dot(n, triangle.V1);
            
            // Ray is of form P = O + tR, where O is origin, t is distance, R is direction.
            var t = (d - Vector3.Dot(n, Source)) / Vector3.Dot(n, Direction);
            
            // Check if triangle is behind ray
            if (t < 0)
            {
                continue;
            }
        
            var p = Source + t * Direction;
            if (triangle.PointIsInside(p))
            {
                // Ray intersects triangle at p
                rayHits.Add(p);
            }
            // don't stop coz ray might intersect more triangles!
        }

        
        return rayHits;
    }

    public Vector3? GetIntersectionPoint(Triangle triangle)
    {
        // Check 1: If ray and normal are perpendicular, no intersection.
        var n = triangle.GetNormal();
        if (Math.Abs(Vector3.Dot(n, Direction)) < 1e-6)
        {
            return null;
        }
            
        // Now find the equation of the plane that the triangle lies within.
            
        // Plane is of form N . x - D = 0, where x is a point on the plane.
        var d = Vector3.Dot(n, triangle.V1);
            
        // Ray is of form P = O + tR, where O is origin, t is distance, R is direction.
        var t = (d - Vector3.Dot(n, Source)) / Vector3.Dot(n, Direction);
            
        // Check if triangle is behind ray
        if (t < 0)
        {
            return null;
        }
        
        var p = Source + t * Direction;
        
        if (triangle.PointIsInside(p))
        {
            // Ray intersects triangle at p
            return p;
        }
        return null;
    }

    public float[] SourceAsFloats()
    {
        return [Source[0], Source[1], Source[2]];
    }

    public float[] DirectionAsFloats()
    {
        return [Direction[0], Direction[1], Direction[2]];
    }
    
    public Vector3 Direction { get; } = direction;
    public Vector3 Source { get; } = source;
}