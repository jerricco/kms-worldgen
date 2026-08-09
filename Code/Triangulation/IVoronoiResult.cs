namespace Sandbox.Triangulation;

public interface IVoronoiResult
{
	VoronoiSite Site { get; }
	float DistanceSq { get; }
}
