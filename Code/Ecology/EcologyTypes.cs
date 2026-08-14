namespace Sandbox.Ecology;

public record SlopeVector(Vector2 Gradient, float Slope);

public record SlopeAspect(
	Vector2 Gradient,
	float Slope,
	int AngleDegrees,
	CardinalDirection Direction
);

public enum CardinalDirection
{
	North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest,
}

public enum BasementRockType
{
	Basalt, Granite, Limestone, Sandstone, Sedimentary,
}

public record SubterraneanLayer(int BedrockDepth, int SedimentaryDepth, BasementRockType PrimaryRockType);
