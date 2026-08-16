using System;


namespace Sandbox.GameData;

// @NOTE: The given values are the more correct, and the derived calculations from them should change to ensure
// the value ranges produce sensible results while the default values produce roughly Earth-like results.
[Category("Procedural Generation")]
public class MapConfig: Component
{
	//////////////////////////
	//   world dimensions   //
	//////////////////////////
	// @TODO: A few of these properties should be moved from elsewhere
	// @TODO: Make Map Dimensions a dropdown of valid world generation sizes.
	[Property, Group("Map Dimensions"), Order(3)]
	public int WorldWidth = 16384; // @TODO: center around this value for now, but determine other value sizes
	[Property, ReadOnly, Group("Map Dimensions"), Order(3)]
	public float InscribedRadius => WorldWidth / 2f;

	[Property, ReadOnly, Group("Map Dimensions"), Order(3)]
	public float CircumscribedRadius => WorldWidth / MathF.Sqrt( 2f );
	
	// END @TODO
	//////////////////////////////
	// configuration properties //
	//////////////////////////////
	// DistanceToEquator - The proportion from -1 to -1 of how far the equator is from the X=0. A maximum distance represents
	// that the continent's X=0 centers on a respective planetary pole (-1 for south pole, +1 for north pole)
	// this value doesn't actually affect the planet generation, but informs the map on where it is placed on the globe of the planet.
	[Property, Range( -1f, 1f ), Group( "Map Levers" ), Order( 1 )]
	public float DistanceToEquator { 
		get =>  _distanceToEquator;
		set {
			_distanceToEquator = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// @TODO - integrate this, it currently has no effect
	// SolarIntensity - The nature of the star that is outputting light to the planet, which is assumed to always live
	// in the habitable zone. -1 is a red dwarf, 0 a yellow star and 1 a blue star, affecting a number of gameplay levers
	// including many of the levers in this file, as well as the lighting cast to the map and the overall day length.
	[Property, Range( -1f, 1f ), Group( "Map Levers" ), Order( 1 )]
	public float SolarIntensity { 
		get =>  _solarIntensity;
		set {
			_solarIntensity = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// @TODO - integrate this, it currently has no effect
	// SolarFlux is the measure of how unpredictable the star is (its volatility). This has a small impact on climate weights,
	// but primarily causes interesting conditions, which are highly dependent on SolarIntensity. For example,
	// a high-Flux, red-dwarf will create different effects to a high-Flux blue star.
	[Property, Range( 0f, 1f ), Group( "Map Levers" ), Order( 1 )]
	public float SolarFlux { 
		get =>  _solarFlux;
		set {
			_solarFlux = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// PlanetInclination- current Earth axial tilt in radians. Maxes out at 90Pi/180 (90degrees).
	// Tipping it all the way should produce tidal-locking. Catastrophic, but maybe survivable???
	[Property, Range(0f, MathF.PI / 2.0f), Group("Map Levers"), Order(1)]
	public float PlanetInclination { 
		get =>  _planetInclination;
		set {
			_planetInclination = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// Orbit Eccentricity - Current inclination proportion, multiplied by 10 for a more palatable value. The range maxes out 1/10th of
	// the orbital mechanical maximum since this will still greatly affect climate.
	[Property, Range(-1f, 1f), Group("Map Levers"), Order(1)]
	public float OrbitEccentricity { 
		get =>  _orbitEccentricity;
		set {
			_orbitEccentricity = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// Glaciation - 0 being ice-free and 1 being the last glacial maximum, earth is currently quite warm
	[Property, Range(0f, 1f), Group("Map Levers"), Order(1)]
	public float Glaciation { 
		get =>  _glaciation;
		set {
			_glaciation = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// Radiative Forcing - 0 being pre-industrial CO2, 1 being biosphere collapse-level hothouse.
	// This is intentionally conflating C02 concentration and ocean-acidification effects
	[Property, Range(0f, 1f), Group("Map Levers"), Order(1)]
	public float RadiativeForcing { 
		get =>  _radiativeForcing;
		set {
			_radiativeForcing = value;
			ClampAndUpdateConfig();
		} 
	}
	
	// BiophysicalDegradation - A value from 0 to 1 that explains how much ecological damage the Aeons have
	// historically done to the planet before this point. 0 means the most that was going on was hunter-gathering.
	// 1 means that someone has at some point set off a few nukes. Generally we default to 0.
	[Property, Range(0f, 1f), Group("Map Levers"), Order(1)]
	public float BiophysicalDegradation { 
		get =>  _biophysicalDegradation;
		set {
			_biophysicalDegradation = value;
			ClampAndUpdateConfig();
		} 
	}
	
	//////////////////////////
	//  derived properties  //
	//////////////////////////
	// TropicLines - see derivation function DeriveTropicLine
	[Property, ReadOnly, Group("Calculated Values"), Order(2)]
	public float TropicLine;
	// GeneticDiversity - see derivation function DeriveGeneticDiversity
	[Property, ReadOnly, Group("Calculated Values"), Order(2)]
	public float GeneticDiversity;
	// AverageGlobalTemperature - see derivation function DeriveAverageGlobalTemperature
	[Property, ReadOnly, Group("Calculated Values"), Order(2)]
	public float AverageGlobalTemperature;
	
	//////////////////////////
	// hardcoded properties //
	//////////////////////////
	// The proportion of the map (in each direction) by which the ocean forces itself inside the map.
	// These ensure the map rarely generates large parts of a landmass off the edge and tries to contain
	// the continent inside its bounds.
	// @TODO: make configurable in a range?
	[Property, ReadOnly, Group("Map Constants"), Order(100)]
	public float OceanClampX = 0.85f;
	[Property, ReadOnly, Group("Map Constants"), Order(100)]
	public float OceanClampY = 0.85f;
	
	//////////////////////////
	// component properties //
	//////////////////////////
	private bool _isAwake = false;

	// the private getter/setter properties contain raw defaults
	// these will generally be ovewritten by the player.
	private float _distanceToEquator = 0f;
	private float _solarIntensity = 0f;
	private float _solarFlux = 0.2f;
	private float _planetInclination      = 0.409f;
	private float _orbitEccentricity = 0.167f;
	private float _glaciation = 0.18f;
	private float _radiativeForcing = 0.24f;
	private float _biophysicalDegradation = 0f;
	

	// Even though this is a component, we want the constructor to statically assign properties before the game
	// lifecycle so that it can more easily behave like a data source only modifiably by its parent MapManager
	protected override void OnAwake()
	{
		// awaken component so setters can begin updating values;
		_isAwake = true;
		// immediately do so
		ClampAndUpdateConfig();
	}

	public void ClampAndUpdateConfig()
	{
		if ( !this._isAwake ) return;
		
		// clamp all values by their private members (to not retrigger prop setters recursively)
		this._distanceToEquator      = Math.Clamp(this.DistanceToEquator, -1.0f, 1.0f);
		this._solarIntensity         = Math.Clamp(this.SolarIntensity, -1.0f, 1.0f);
		this._solarFlux              = Math.Clamp(this.SolarFlux, 0f, 1.0f);
		this._planetInclination      = Math.Clamp(this.PlanetInclination, 0f, MathF.PI / 2.0f);
		this._orbitEccentricity      = Math.Clamp(this.OrbitEccentricity, -1.0f, 1.0f);
		this._glaciation             = Math.Clamp(this.Glaciation, 0f, 1.0f);
		this._radiativeForcing       = Math.Clamp(this.RadiativeForcing, 0f, 1.0f);
		this._biophysicalDegradation = Math.Clamp(this.BiophysicalDegradation, 0f, 1.0f);
		
		// derive cacheable weights
		this.AverageGlobalTemperature = DeriveAverageGlobalTemperature();
		
		this.TropicLine = DeriveTropicLine(
			this.PlanetInclination, 
			this.OrbitEccentricity, 
			this.Glaciation, 
			this.RadiativeForcing);
		
		this.GeneticDiversity = DeriveGeneticDiversity(
			this.BiophysicalDegradation, 
			this.Glaciation, 
			this.RadiativeForcing);
	}

	/// <summary>
	/// Derives the average global temperature based on SolarIntensity, SolarFlux, OrbitEccentricity, Glaciation
	/// & Radiative Forcing (which share a relationship here) as well as high-levels of BiophysicalDegradation (since
	/// greenery cools the place. We handle later in the sim for things like Nuclear Winter affecting this value. 
	/// </summary>
	/// <returns></returns>
	public static float DeriveAverageGlobalTemperature()
	{
		return 0f; // placeholder: @TODO
	}

	/// <summary>
	/// Derives the genetic diversity present on the planet based on BiophysicalDegradation,RadiativeForcing and
	/// Glaciation being lower to produce a higher value. This really describes the overall presence of life, as
	/// other climatological and biome factors will determine how well the presence of life persists inside their
	/// biogeographical niches. SolarIntensity has can have a minor bump effect as it reaches 1, since the radiative
	/// properties should produce 50s like mutation variability (heh). SolarFlux will slowly lerp the diversity downward
	/// as it reaches higher values since the star activity would encourage periodic extinctions more often.
	/// </summary>
	/// <param name="bio"></param>
	/// <param name="glac"></param>
	/// <param name="rad"></param>
	/// <returns></returns>
	public static float DeriveGeneticDiversity(float bio, float glac, float rad)
	{
		// invert inputs since they share an inverse relationship with genetic diversity.
		float bioPreservation = 1.0f - bio;
		float glacPreservation = 1.0f - glac;
		float radPreservation = 1.0f - rad;

		// cascade decreasing weights (Must sum to 1.0)
		const float wBio = 0.55f;
		const float wGlac = 0.30f; 
		const float wRad = 0.15f;  

		// get base weighted diversity
		float baseDiversity = (bioPreservation * wBio) + (glacPreservation * wGlac) + (radPreservation * wRad);

		// wipeout: if all three are exactly 1.0, force output to 0.0
		// (Using a small epsilon check instead of == for floating-point safety)
		if (Math.Abs(bio - 1.0) < 1e-6 && Math.Abs(glac - 1.0) < 1e-6 && Math.Abs(rad - 1.0) < 1e-6)
		{
			return 0.0f;
		}

		// clamp result
		return Math.Clamp(baseDiversity, 0.0f, 1.0f);
	}
	
	/// <summary>
	/// Derives the placement of a two TropicLines as a tuple proportion of distance from
	/// the planet's equator. TropicLines are greatly affected by orbital mechanics which forces
	/// tropical biomes to be much rarer if not absent in maps with more extreme biome zones present.
	/// The SolarIntensity through its whole proportion (-1 to 1) adds a tiny linear % boost as the higher
	/// intensity encourages foliage density and higher levels of photosynthetic efficiency.
	/// </summary>
	/// <param name="inc"></param>
	/// <param name="ecc"></param>
	/// <param name="glac"></param>
	/// <param name="rad"></param>
	/// <returns></returns>
	public static float DeriveTropicLine(float inc, float ecc, float glac, float rad)
    {
        // planet inclination squeeze - grow the tropic lines up to about 24-25 degrees
        // and then begin to crunch them as the inclination grows.
        // Uses a 4th-power logistic drop-off the value past 0.5 radians (our deg value).
        float incSqueeze = 1.0f / (1.0f + MathF.Pow(inc / 0.5f, 4.0f));

        // eccentricity (symmetric around 0, peaks at 0.75, then drops)
        // this ensures that the tropic bands grow as the planet seasonal boundaries drift
        // but drop off if the planet flings too far, since there's too little time in the
        // year any longer for valid tropic bands to form (which gives more latitudal extremes).
        float eccAbs = MathF.Abs(ecc);
        float eccEffect;
        if (eccAbs <= 0.75f)
        {
            // linearly scales up to a +90% positive influence at 0.65
            eccEffect = 1.0f + 0.90f * (eccAbs / 0.65f);
        }
        else
        {
            // sharply drops from the peak down to a 0.90 penalty at 1.0 eccentricity
            float progressPastPeak = (eccAbs - 0.65f) / 0.35f;
            eccEffect = 1.20f - 0.90f * progressPastPeak;
        }

        // linearly drop toward 0 with glaciation, but don't quite reach it.
        float glacEffect = 1.0f - (glac * 0.9f);

        // radiative forcing (Small boost up to 0.75, then reduces)
        // greenhouse effects grow the tropics slightly but then quickly overheat the planet.
        float radEffect = 1.0f;
        if (rad <= 0.75f)
        {
            // Small +15% positive influence up to 0.75
            radEffect = 1.0f + 0.15f * (rad / 0.75f);
        }
        else
        {
            // Pulls back down below baseline to 0.85 at maximum forcing
            float progressPastPeak = (rad - 0.75f) / 0.25f;
            radEffect = 1.15f - 0.20f * progressPastPeak;
        }

        // aggregate the factors multiplicatively to maintain 0->1 scaling integrity
        float tropicLine = incSqueeze * eccEffect * glacEffect * radEffect;
        
        float baselineScalar = 0.38f; 
        tropicLine *= baselineScalar; // shrink the value to a base scalar level so we're working with sensible defaults.

        return Math.Clamp(tropicLine, 0.0f, 1.0f);
    }
}
