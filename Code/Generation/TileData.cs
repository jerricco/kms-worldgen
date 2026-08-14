using System;
using Sandbox.Ecology;
using Sandbox.GameData;
using Sandbox.Triangulation;
using Sandbox.Utility;

namespace Sandbox.Generation;

using Extensions;
using Generator;

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
		var global = this.GlobalPosition;

		// fractal warp
		var warpX = Noise.Perlin((global.x * settings.MacroScale) + 123.45f, (global.y * settings.MacroScale) + 678.90f);
		var warpY = Noise.Perlin((global.x * settings.MacroScale) - 456.78f, (global.y * settings.MacroScale) + 321.12f);

		// Warp intensity. Higher values = deeper gulfs and broken straits.
		var warpIntensity = 1200f;// @TODO: config?
		var warpedWorldPos = new Vector2(
			(global.x * settings.StretchX) + (warpX * warpIntensity),
			(global.y * settings.StretchY) + (warpY * warpIntensity)
		);

		// distance to nearest spine line segment
		var minDistanceToSpine = float.MaxValue;
		foreach (var spine in tectonicSpines)
		{
			if (spine.Nodes == null || spine.Nodes.Count == 0)
			{
				continue;
			}

			if (spine.Nodes.Count == 1)
			{
				minDistanceToSpine = MathF.Min(minDistanceToSpine, Vector2.Distance(warpedWorldPos, spine.Nodes[0]));
				continue;
			}

			for (var i = 0; i < spine.Nodes.Count - 1; i++)
			{
				var dist = warpedWorldPos.DistanceToSegment(spine.Nodes[i], spine.Nodes[i + 1]);
				if (dist < minDistanceToSpine)
				{
					minDistanceToSpine = dist;
				}
			}
		}

		// base elevation gradient
		var maxSpineInfluence = settings.MaxDimension * 0.4f;
		var spineGradient = 1.0f - (minDistanceToSpine / maxSpineInfluence).Clamp(0f, 1f);

		// creates wide flat sedimentary lowlands before clamping near mountains
		spineGradient = MathF.Pow(spineGradient, 1.8f);
		spineGradient = spineGradient.SmoothStep(0f, 1f);

		// fBm detail - layering multiple frequencies to build complex details (Coastlines, small hills)
		var detailNoise = 0f;
		var amplitude = 1.0f;
		var currentFreq = settings.MicroScale;
		var totalAmplitude = 0f;
		const int octaves = 5;

		for (var i = 0; i < octaves; i++)
		{
			var n = Noise.Perlin(warpedWorldPos.x * currentFreq, warpedWorldPos.y * currentFreq) - 0.5f;
			detailNoise += n * amplitude;
			totalAmplitude += amplitude;

			currentFreq *= 2.1f;// Lacunarity (frequency multiplier)
			amplitude *= 0.48f;// Persistence (amplitude dampener)
		}

		detailNoise /= totalAmplitude;// Normalized back to a clean -0.5 to 0.5 variation range

		// ridged noise - pinches mountain elevation
		// Sharp mountain cresting driven by an aggressive power exponent
		var rawRidge = Noise.Perlin((warpedWorldPos.x * settings.MacroScale * 6f) + 50f, (warpedWorldPos.y * settings.MacroScale * 6f) + 50f);
		var ridgeNoise = 1.0f - MathF.Abs((rawRidge - 0.5f) * 2.0f);
		ridgeNoise = MathF.Pow(ridgeNoise, 3.0f);

		// combine profiles & mask
		// Instead of a flat base, we lerp between Abyssal and Sea Level for ocean basins,
		// and Sea Level to Peak Level for land masses.
        float baseElevation;
		if (spineGradient < 0.25f)
		{
			// Ocean Floor Basin Profile
			baseElevation = MathX.Lerp(settings.AbyssalLevel, settings.SeaLevel, spineGradient / 0.25f);
			baseElevation += detailNoise * 0.15f;
		}
		else
		{
			// Surface Landmass Profile
			var landT = (spineGradient - 0.25f) / 0.75f;
			baseElevation = MathX.Lerp(settings.SeaLevel, settings.MountainLevel, landT);

			// Coastline Scrambler: Targets your explicit SeaLevel property to fractalize borders
			var coastMask = 1.0f - (MathF.Abs(baseElevation - settings.SeaLevel) * 4f).Clamp(0f, 1f);
			baseElevation += detailNoise * 0.45f * coastMask;

			// Rolling Lowland Plains & Hills
			baseElevation += detailNoise * 0.15f * (1.0f - coastMask);
		}

		// pinched mountain peaks
		if (spineGradient > 0.55f)
		{
			var mountainMask = spineGradient.Remap(0.55f, 1.0f);
			baseElevation = MathX.Lerp(baseElevation, settings.PeakLevel, mountainMask * ridgeNoise * 0.8f);
			baseElevation += mountainMask * 0.12f;
		}

		// ocean clamp falloff
		var distX = MathF.Abs(global.x) / settings.HalfWidth;
		var distY = MathF.Abs(global.y) / settings.HalfHeight;
		var edgeDistance = MathF.Max(distX, distY);

		var falloffStart = settings.OceanClamp;
		var edgeFalloff = 0f;

		if (edgeDistance > falloffStart)
		{
			edgeFalloff = (edgeDistance - falloffStart) / (1.0f - falloffStart);
			edgeFalloff = edgeFalloff.SmoothStep(0f, 1f);
		}

		var finalElevation = MathX.Lerp(baseElevation, settings.AbyssalLevel, edgeFalloff);

		this.Elevation = Math.Clamp(finalElevation, -1.0f, 1.0f);
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
		this.Geology = new SubterraneanLayer(0, 0, BasementRockType.Basalt);// @TODO: Does nothing for now
	}

	///////////////////////////////////////////////
	//          Region & Biome Assignment        //
	///////////////////////////////////////////////
	public void GenerateRegion()
	{
		this.RegionId = RegionId.Unassigned;// @TODO: Does nothing for now.
	}
}
