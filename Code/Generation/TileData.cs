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
			
			// fractal warp
			float warpX = Noise.Perlin((global.x * settings.MacroScale) + 123.45f, (global.y * settings.MacroScale) + 678.90f);
			float warpY = Noise.Perlin((global.x * settings.MacroScale) - 456.78f, (global.y * settings.MacroScale) + 321.12f);
			
			
			// Warp intensity. Higher values = deeper gulfs and broken straits.
			float warpIntensity = 1200f; // @TODO: config?
			Vector2 warpedWorldPos = new Vector2(
				(global.x * settings.StretchX) + (warpX * warpIntensity),
				(global.y * settings.StretchY) + (warpY * warpIntensity)
			);
			
			// distance to nearest spine line segment
			float minDistanceToSpine = float.MaxValue;
			foreach (var spine in tectonicSpines)
			{
				if (spine.Nodes == null || spine.Nodes.Count == 0) continue;

				if (spine.Nodes.Count == 1)
				{
					minDistanceToSpine = MathF.Min(minDistanceToSpine, Vector2.Distance(warpedWorldPos, spine.Nodes[0]));
					continue;
				}

				for (int i = 0; i < spine.Nodes.Count - 1; i++)
				{
					float dist = DistanceToSegment(warpedWorldPos, spine.Nodes[i], spine.Nodes[i + 1]);
					if (dist < minDistanceToSpine)
					{
						minDistanceToSpine = dist;
					}
				}
			}
			
			// base elevation gradient
			float maxSpineInfluence = settings.MaxDimension * 0.4f; 
			float spineGradient = 1.0f - MathX.Clamp(minDistanceToSpine / maxSpineInfluence, 0f, 1f);
			
			// creates wide flat sedimentary lowlands before clamping near mountains
			spineGradient = MathF.Pow(spineGradient, 1.8f); 
			spineGradient = SmoothStep(0f, 1f, spineGradient);

			// fBm detail - layering multiple frequencies to build complex details (Coastlines, small hills)
			float detailNoise = 0f;
			float amplitude = 1.0f;
			float currentFreq = settings.MicroScale;
			float totalAmplitude = 0f;
			int octaves = 5;
			
			for (int i = 0; i < octaves; i++)
			{
				float n = Noise.Perlin(warpedWorldPos.x * currentFreq, warpedWorldPos.y * currentFreq) - 0.5f;
				detailNoise += n * amplitude;
				totalAmplitude += amplitude;
        
				currentFreq *= 2.1f;  // Lacunarity (frequency multiplier)
				amplitude *= 0.48f;   // Persistence (amplitude dampener)
			}
			detailNoise /= totalAmplitude; // Normalized back to a clean -0.5 to 0.5 variation range
			
			// ridged noise - pinches mountain elevation
			// Sharp mountain cresting driven by an aggressive power exponent
			float rawRidge = Noise.Perlin((warpedWorldPos.x * settings.MacroScale * 6f) + 50f, (warpedWorldPos.y * settings.MacroScale * 6f) + 50f);
			float ridgeNoise = 1.0f - MathF.Abs((rawRidge - 0.5f) * 2.0f); 
			ridgeNoise = MathF.Pow(ridgeNoise, 3.0f);
			
			// combine profiles & mask
			// Instead of a flat base, we lerp between Abyssal and Sea Level for ocean basins, 
			// and Sea Level to Peak Level for land masses.
			float baseElevation = 0f;
			if (spineGradient < 0.25f) 
			{
				// Ocean Floor Basin Profile
				baseElevation = MathX.Lerp(settings.AbyssalLevel, settings.SeaLevel, spineGradient / 0.25f);
				baseElevation += detailNoise * 0.15f; 
			}
			else 
			{
				// Surface Landmass Profile
				float landT = (spineGradient - 0.25f) / 0.75f;
				baseElevation = MathX.Lerp(settings.SeaLevel, settings.MountainLevel, landT);
        
				// Coastline Scrambler: Targets your explicit SeaLevel property to fractalize borders
				float coastMask = 1.0f - MathX.Clamp(MathF.Abs(baseElevation - settings.SeaLevel) * 4f, 0f, 1f); 
				baseElevation += detailNoise * 0.45f * coastMask; 

				// Rolling Lowland Plains & Hills
				baseElevation += detailNoise * 0.15f * (1.0f - coastMask);
			}
			
			// pinched mountain peaks
			if (spineGradient > 0.55f)
			{
				float mountainMask = MathX.Remap(spineGradient, 0.55f, 1.0f, 0f, 1f);
				baseElevation = MathX.Lerp(baseElevation, settings.PeakLevel, mountainMask * ridgeNoise * 0.8f);
				baseElevation += mountainMask * 0.12f; 
			}
			
			// ocean clamp falloff
			float distX = MathF.Abs(global.x) / settings.HalfWidth;
			float distY = MathF.Abs(global.y) / settings.HalfHeight;
			float edgeDistance = MathF.Max(distX, distY);
			
			float falloffStart = settings.OceanClamp;
			float edgeFalloff = 0f;

			if (edgeDistance > falloffStart)
			{
				edgeFalloff = (edgeDistance - falloffStart) / (1.0f - falloffStart);
				edgeFalloff = SmoothStep(0f, 1f, edgeFalloff);
			}

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
		
		/// RANDOM ASS UTILITY FUNCTIONS I SHOULD MOVE
		/// BUNG
		private float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			Vector2 ap = p - a;
			float r = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
    
			if (r <= 0.0f) return Vector2.Distance(p, a);
			if (r >= 1.0f) return Vector2.Distance(p, b);
    
			Vector2 closestPoint = a + r * ab;
			return Vector2.Distance(p, closestPoint);
		}
		
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
