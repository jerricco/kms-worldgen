namespace Sandbox.Ecology;

public enum RegionId
{
	// SPECIAL
	Void = 0,
	Unassigned = 1,

	// AQUATIC REGIONS
	// elevation
	CrustFloor = 2,
	AbyssalOcean = 3,
	DeepOcean = 4,
	Ocean = 5,

	// geographic
	Sea = 6,

	// climatic
	FreshLake = 7,
	SalineLake = 8,
	Reef = 9,

	// TRANSITIONAL REGIONS
	// geographic
	Beach = 10,
	Cliff = 11,
	Island = 12,

	// climatic
	Wetland = 13,
	Estuary = 14,

	// climatic + rivers
	River = 15,
	RiverDelta = 16,

	// TERRESTRIAL REGIONS
	// elevation regions
	Hill = 17,
	Mountain = 18,
	Peak = 19,

	// climatic
	Plain = 20,

	// geographic
	Plateau = 21,
	Valley = 22,

	// climatic
	Desert = 23,
	Forest = 24,
	Tundra = 25,

	// lithogaphic
	Karst = 26,

	// subterrene
	Lithosphere = 27,
	Cave = 28,
	SubterraneanAquifer = 29,

	// special
	DeepBiosphere = 30,
	HydrothermalSystem = 31,
}
