using System;

namespace Sandbox.GameData;

// @NOTE: The given values are the more correct, and the derived calculations from them should change to ensure
// the value ranges produce sensible results while the default values produce roughly Earth-like results.
public class MapConfig: Component
{
	//////////////////////////
	//   world dimensions   //
	//////////////////////////
	// @TODO: A few of these properties should be moved from elsewhere
	[Property] public int WorldWidth = 16384; // @TODO: center around this value for now, but determine other value sizes
	[Property, ReadOnly] public float InscribedRadius => WorldWidth / 2f;

	[Property, ReadOnly] public float CircumscribedRadius => WorldWidth / MathF.Sqrt( 2f );
	
	//////////////////////////////
	// configuration properties //
	//////////////////////////////
	// END @TODO
	// DistanceToEquator - The proportion from -1 to -1 of how far the equator is from the X=0. A maximum distance represents
	// that the continent's X=0 centers on a respective planetary pole (-1 for south pole, +1 for north pole)
	// this value doesn't actually affect the planet generation, but informs the map on where it is placed on the globe of the planet.
	[Property] public readonly float DistanceToEquator = 0f;
	
	// PlanetInclination- current Earth axial tilt in radians. Maxes out at 90Pi/180 (90degrees).
	// Tipping it all the way should produce tidal-locking. Catastrophic, but maybe survivable???
	[Property] public readonly float PlanetInclination = 0.409f;
	
	// Orbit Eccentricity - Current inclination proportion, multiplied by 10 for a more palatable value. The range maxes out 1/10th of
	// the orbital mechanical maximum since this will still greatly affect climate.
	[Property] public readonly float OrbitEccentricity = 0.167f;
	
	// Glaciation - 0 being ice-free and 1 being the last glacial maximum, earth is currently quite warm
	[Property] public readonly float Glaciation = 0.18f; 
	
	// Radiative Forcing - 0 being pre-industrial CO2, 1 being biosphere collapse-level hothouse.
	// This is intentionally conflating C02 concentration and ocean-acidification effects
	[Property] public readonly float RadiativeForcing = 0.24f; 
	// BiophysicalDegradation - A value from 0 to 1 that explains how much ecological damage the Aeons have
	// historically done to the planet before this point. 0 means the most that was going on was hunter-gathering.
	// 1 means that someone has at some point set off a few nukes.
	[Property] public readonly float BiophysicalDegradation = 0f; // always baseline at 0 here
	
	//////////////////////////
	//  derived properties  //
	//////////////////////////
	// TropicLines - see derivation function DeriveTropicLine
	[Property] public readonly (float, float) TropicLines;
	// GeneticDiversity - see derivation function DeriveGeneticDiversity
	[Property] public readonly float GeneticDiversity;
	
	//////////////////////////
	// hardcoded properties //
	//////////////////////////
	// The proportion of the map (in each direction) by which the ocean forces itself inside the map.
	// These ensure the map rarely generates large parts of a landmass off the edge and tries to contain
	// the continent inside its bounds.
	// @TODO: make configurable in a range?
	[Property, ReadOnly] public readonly float OceanClampX = 0.85f;
	[Property, ReadOnly] public readonly float OceanClampY = 0.85f;

	public MapConfig( 
		float distanceToEquator, 
		float planetInclination, 
		float orbitEccentricity, 
		float glaciation,
		float radiativeForcing, 
		float biophysicalDegradation )
	{
		// clamp all values
		DistanceToEquator      = Math.Clamp(distanceToEquator, -1.0f, 1.0f);
		PlanetInclination      = Math.Clamp(planetInclination, 0f, MathF.PI / 2.0f);
		OrbitEccentricity      = Math.Clamp(orbitEccentricity, -1.0f, 1.0f);
		Glaciation             = Math.Clamp(glaciation, 0f, 1.0f);
		RadiativeForcing       = Math.Clamp(radiativeForcing, 0f, 1.0f);
		BiophysicalDegradation = Math.Clamp(biophysicalDegradation, 0f, 1.0f);
		
		// derive cacheable weights
		float line = DeriveTropicLine(PlanetInclination, OrbitEccentricity, Glaciation, RadiativeForcing);
		TropicLines = (-line, line);
		GeneticDiversity = DeriveGeneticDiversity(this.BiophysicalDegradation, this.Glaciation, this.RadiativeForcing);
	}


	/// <summary>
	/// Derives the genetic diversity present on the planet based on BiophysicalDegradation,RadiativeForcing and
	/// Glaciation being lower to produce a higher value. This really describes the overall presence of life, as
	/// other climatological and biome factors will determine how well the presence of life persists inside their
	/// biogeographical niches. 
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
	/// @TODO: Tweak this to ensure the values come out working for most planet types.
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
        // Uses a 4th-power logistic drop-off the value past 0.5 radians.
        float incSqueeze = 1.0f / (1.0f + MathF.Pow(inc / 0.5f, 4.0f));

        // eccentricity (symmetric around 0, peaks at 0.75, then drops)
        // this ensures that the tropic bands grow as the planet seasonal boundaries drift
        // but drop off if the planet flings too far, since there's too little time in the
        // year any longer for valid tropic bands to form (which gives more latitudal extremes).
        float eccAbs = MathF.Abs(ecc);
        float eccEffect;
        if (eccAbs <= 0.75f)
        {
            // linearly scales up to a +20% positive influence at 0.75
            eccEffect = 1.0f + 0.20f * (eccAbs / 0.75f);
        }
        else
        {
            // sharply drops from the 1.20 peak down to a 0.50 penalty at 1.0 eccentricity
            float progressPastPeak = (eccAbs - 0.75f) / 0.25f;
            eccEffect = 1.20f - 0.70f * progressPastPeak;
        }

        // linearly drop to 0 with glaciation.
        float glacEffect = 1.0f - glac;

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

        return Math.Clamp(tropicLine, 0.0f, 1.0f);
    }
}
