using Sandbox.Ecology;
using Sandbox.Triangulation;

namespace Sandbox.Generation
{
	public struct TileData : ITileData
	{
		public double Elevation { get; set; }
		public double Humidity { get; set; }
		public double Temperature { get; set; }
		public int MaterialId { get; set; }
		public RegionId RegionId { get; set; }
		public SubterraneanLayer Geology { get; set; }
		public DelaunayNeighbors NeighborSites { get; set; }

		public TileData( double elevation, double humidity, double temperature, int materialId, RegionId regionId,  SubterraneanLayer geology,  DelaunayNeighbors neighborSites )
		{
			Elevation = elevation;
			Humidity = humidity;
			Temperature = temperature;
			MaterialId = materialId;
			RegionId = regionId;
			Geology = geology;
			NeighborSites = neighborSites;
		}
	}
}
