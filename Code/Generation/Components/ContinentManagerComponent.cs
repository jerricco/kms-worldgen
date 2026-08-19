namespace Sandbox.Generation;

[Category("Procedural Generation")]
public class ContinentManagerComponent: Component
{
	// force-directed lloyd-relaxed worldwide tectonic plates.
	// These are affected by properties such as PlanetaryMagneticDynamo & FilterEvents
	[Property, ReadOnly, Group("Plate Tectonics"), Order(1)]
	public float TectonicPlates;
	
	// For each tectonic plate, we need to store the vector directions of each side, ensuring all direction vectors add up to 0.
	// This ensures that each plate has sides which subduct crust as well as sides which form new ones.
	// If all plates have a vectorSum of 0, then we know that the only external forces are the cooling of the crust above and the heat pressure from below.
	// The directional vectors can be seeded by the force-direction of the lloyd-relaxed grid in generating the original plate sizes.
	// This tells the generation engine where to form continent-building superstructures such as
	// continental rock cratons, mid-ocean ridges, fault lines and collision mountain ranges.
	// NOTE to add that transformational boundaries (those sliding against each other) should be weighted to be rarer until a "continental production threshold" has been reached.
	[Property, ReadOnly, Group("Plate Tectonics"), Order(1)]
	public float TectonicPlateBoundaryDirections;
	
	// a number of properties to store a collection of boundaries or lines representing distinct geological superstructures
	[Property, ReadOnly, Group("Geographic Superstructure"), Order(2)]
	public float DivergentBoundaries; // mid-ocean ridges, rift valleys, crustal-formation zones can all spawn in these
	[Property, ReadOnly, Group("Geographic Superstructure"), Order(2)]
	public float ConvergentBoundaries; // fold mountains, subduction trenches, volcanic-ocean arcs, andean-volcanics can all spawn in these boundaries
	[Property, ReadOnly, Group("Geographic Superstructure"), Order(2)]
	public float TransformBoundaries; // fault lines, earthquake zones can both form here. 
}
