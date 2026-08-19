namespace Sandbox.Generation;

[Category("Procedural Generation")]
public class LifeManagerComponent: Component
{
	// A voronoi-like structure of bounding boxes which contain different biogeographical regions
	// determined by the planetary climate
	[Property, ReadOnly, Group("Generative Values"), Order(2)]
	public float Biogeographies;
	
	// plants, algae, cyanobacteria, other photosynthesisers
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int AutotrophBucketSize;
	// fungi, bacteria, scavengers, carrion eaters, decomposing insects
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int DetritivoreBucketSize;
	// Grazers, browsers, anything which consumes Autotrophs
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int HerbivoreBucketSize;
	// Hunters, meat eaters, herbivore regulators and partial detritivore feeders
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int CarnivoreBucketSize;
	// parasitic organisms which require other life to survive. Includes viruses.
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int ParasiteBucketSize;
	// diseases, bacteria, protozoa - dense population regulators & evolution driver
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int PathogenBucketSize;
	// seed & pollen dispersers to keep genetic variety and populations of autotrophs high
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int PollinatorBucketSize;
	// species who survive by using environmental engineering, eg: beavers, earthworms.
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int ConstructivoreBucketSize;
	// species which simply just survive, anywhere.
	[Property, ReadOnly, Group("Eco Niche Buckets"), Order(3)]
	public int ExtremophileBucketSize;
}
