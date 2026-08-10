using Sandbox.Ecology;
using Sandbox.Generator;
using Sandbox.Triangulation;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using Sandbox.GameData;

namespace Sandbox.Generation
{
	public struct TileData : ITileData
	{
		public Vector2 GlobalPosition { get; set; }
		public double Elevation { get; set; }
		public double Humidity { get; set; }
		public double Temperature { get; set; }
		public int MaterialId { get; set; }
		public RegionId RegionId { get; set; }
		public SubterraneanLayer Geology { get; set; }
		public DelaunayNeighbors NeighborSites { get; set; }

		///////////////////////////////////////////////
		//            Elevation Generation           //
		///////////////////////////////////////////////
		public void GenerateElevation(GenerationSettings settings, List<VoronoiFactory.CurvedSpine> tectonicSpines)
		{
			var global = GlobalPosition;
			
			float warpX = Noise.Perlin((global.x * settings.MacroScale) + 123.45f, (global.y * settings.MacroScale) + 678.90f);
			float warpY = Noise.Perlin((global.x * settings.MacroScale) - 456.78f, (global.y * settings.MacroScale) + 321.12f);
			
			// Adjust warp intensity. Higher values = deeper gulfs and broken straits.
			float warpIntensity = 800f;  // @TODO: configuration?
			Vector2 warpedPos = new Vector2(
				global.x * settings.StretchX + (warpX * warpIntensity),
				global.y * settings.StretchY + (warpY * warpIntensity)
			);
			
			// get nearest tectonic element
			float minDistanceToSpine = float.MaxValue;
			foreach (var spine in tectonicSpines)
			{
				foreach (var node in spine.Nodes)
				{
					float dist = Vector2.Distance(warpedPos, node);
					if (dist < minDistanceToSpine)
					{
						minDistanceToSpine = dist;
					}
				}
			}
			
			// BASE ELEVATION PROFILE
			// Convert distance to a 0-1 gradient. Max influence distance dictates continental width.
			float maxSpineInfluence = settings.MaxDimension * 0.25f; 
			float spineGradient = 1.0f - MathX.Clamp(minDistanceToSpine / maxSpineInfluence, 0f, 1f);
			
			// Shape the gradient: smoothstep prevents harsh angular "pillowing" lines where cells meet
			spineGradient = SmoothStep(0f, 1f, spineGradient);

			// LOW-FREQUENCY NOISE CONTINENTS
			// Broad continental noise variations to break up spine symmetry
			float continentNoise = Noise.Perlin(global.x * (settings.MacroScale * 0.5f), global.y * (settings.MacroScale * 0.5f));
			
			// Map 0->1 noise to a slight lifting/sinking modifier (-0.3 to 0.3)
			float noiseModifier = (continentNoise - 0.5f) * 0.6f; 

			// Combine spine influence and structural noise
			// This ensures landmasses elevate toward the spine but retain organic variation
			float baseElevation = MathX.Lerp(settings.AbyssalLevel, settings.PeakLevel, spineGradient) + noiseModifier;

			// ASYMMETRICAL MOUNTAIN SPINE JOINING
			// If very close to the spine, sharpen the ridge to form mountain crests
			if (spineGradient > 0.75f)
			{
				float ridgeSharpen = MathX.Remap(spineGradient, 0.75f, 1.0f, 0f, 1f);
				baseElevation += ridgeSharpen * 0.25f; // Extra push into Mountain/Peak territory
			}

			// GLOBAL OCEAN CLAMP FALLOFF
			// Determines how close the tile is to the map edge centered around (0,0)
			float distX = MathF.Abs(global.x) / settings.HalfWidth;
			float distY = MathF.Abs(global.y) / settings.HalfHeight;
			float edgeDistance = MathF.Max(distX, distY); // Square bounding falloff

			// Use OceanClamp to dictate where the drop-off begins
			float falloffStart = settings.OceanClamp;
			float edgeFalloff = 0f;

			if (edgeDistance > falloffStart)
			{
				// Linearly scale falloff from the clamp line to the map border
				edgeFalloff = (edgeDistance - falloffStart) / (1.0f - falloffStart);
				edgeFalloff = SmoothStep(0f, 1f, edgeFalloff); // Smooth transition
			}

			// Pull the elevation down into the deep ocean/abyssal zones near edges
			float finalElevation = MathX.Lerp(baseElevation, settings.AbyssalLevel, edgeFalloff);

			Elevation = Math.Clamp(finalElevation, -1.0f, 1.0f);
		}
		
		///////////////////////////////////////////////
		//             Hydrological Pass             //
		///////////////////////////////////////////////
		
		///////////////////////////////////////////////
		//              Temperature Pass             //
		///////////////////////////////////////////////
		
		///////////////////////////////////////////////
		//            Material Assignment            //
		///////////////////////////////////////////////
		
		///////////////////////////////////////////////
		//             Geology Generation            //
		///////////////////////////////////////////////
		public void GenerateGeology()
		{
			Geology = new SubterraneanLayer( 0, 0, BasementRockType.Basalt ); // @TODO: Does nothing for now
		}
	
		///////////////////////////////////////////////
		//          Region & Biome Assignment        //
		///////////////////////////////////////////////
		public void GenerateRegion()
		{
			RegionId = RegionId.Unassigned; // @TODO: Does nothing for now.
		}
		
		
		/// BUNG
		/// // @TODO: move to math utility u goon
		public float SmoothStep( float edge0, float edge1, float x )
		{
			// Clamp and normalise x between 0.0 and 1.0
			float t = Math.Clamp( ( x - edge0 ) / ( edge1 - edge0 ), 0.0f, 1.0f );
        
			// Evaluate the cubic Hermite polynomial
			return t * t * ( 3.0f - 2.0f * t );
		}
	}
}
