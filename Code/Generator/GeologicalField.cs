namespace Sandbox.Generator;

public struct GeologicalField : IGeologicalField
{
	public float LandChance { get; set; }
	public int ClosestPlateId { get; set; }

	public GeologicalField(float landChance, int closestPlateId)
	{
		this.LandChance = landChance;
		this.ClosestPlateId = closestPlateId;
	}
}
