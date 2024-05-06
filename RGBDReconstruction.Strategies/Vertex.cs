using OpenTK.Mathematics;

namespace RGBDReconstruction.Strategies;

public record Vertex(Vector3 Position, Vector3 Normal, Vector2 TextureCoordinate);

public record WeightedVertex(Vector3 Position, Vector3 Normal, Vector2 TextureCoordinate, float Weight);

public record ColouredVertex(Vector3 Position, Vector3 Normal, Vector4 Colour);