namespace Sandbox.Triangulation;

public struct VoronoiResult : IVoronoiResult
{
	public VoronoiSite Site { get; set; }
	public float DistanceSq { get; set; }

	public VoronoiResult(VoronoiSite site, float distanceSq)
	{
		this.Site = site;
		this.DistanceSq = distanceSq;
	}
}
