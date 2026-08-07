namespace Sandbox.Generator
{
	public struct GeologicalField : IGeologicalField
	{
		public float LandChance { get; set; }
		public int ClosestPlateId { get; set; }

		public GeologicalField( float landChance, int closestPlateId )
		{
			LandChance = landChance;
			ClosestPlateId = closestPlateId;
		}
	}	
}
