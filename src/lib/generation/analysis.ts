////////////////////////////////////////////////////////////
// Functions for analysing the noise & terrain generation //
////////////////////////////////////////////////////////////
import type { ChunkSettings } from "./chunk";
import { MapGenerator, type GlobalGenerationMeta } from "./generator";
import type { BasementRockType, SubterraneanLayer } from "./geology";
import type { OpenSimplexNoise } from "./noise";
import type { SlopeAspect, SlopeVector } from "./types";

export interface TileComposition {
    elevation: number,
    geology: SubterraneanLayer,
}

export function getGlobalTileComposition(
    globalX: number, globalY: number,
    settings: ChunkSettings,
    meta: GlobalGenerationMeta,
    noise: OpenSimplexNoise,
): TileComposition {
    // clamp coords inside world border
    const halfX = meta.worldWidth / 2, halfY = meta.worldHeight / 2;
    globalX = Math.max(-halfX, Math.min(halfX, globalX));
    globalY = Math.max(-halfY, Math.min(halfY, globalY));

    // worldwide ocean boundary proximity
    const distToLeft = -halfX + globalX;
    const distToRight = halfX - globalX;
    const distToTop = -halfY + globalY;
    const distToBottom = halfY - globalY;

    const edgeXFactor = Math.min(1.0, Math.min(distToLeft, distToRight) / meta.bufferX);
    const edgeYFactor = Math.min(1.0, Math.min(distToTop, distToBottom) / meta.bufferY);
    const globalEdgeFactor = edgeXFactor * edgeYFactor;

    const { sampleX, sampleY }
        = MapGenerator.getDomainWarpedSample(globalX, globalY, meta.mOffsetX, meta.mOffsetY, settings, noise);

    const rx = globalX * meta.cosA - globalY * meta.sinA;
    const ry = globalX * meta.sinA + globalY * meta.cosA;

    // macro mask for bays and gulfs.
    const maskWarpStrength = 0.25 * globalEdgeFactor;
    const maskWarpX = noise.noise2D(sampleX * 0.4, sampleY * 0.4) * maskWarpStrength;
    const maskWarpY = noise.noise2D(sampleX * 0.4 + 50, sampleY * 0.4 + 50) * maskWarpStrength;

    const finalMaskDist = Math.sqrt(
        Math.pow((rx + maskWarpX) * meta.stretchX, 2) +
        Math.pow((ry + maskWarpY * meta.stretchY) * meta.stretchY, 2)
    );

    const normalisedDistance = finalMaskDist / meta.maxRadius;
    const sizeModifier = 1.0 / settings.islandRadius // @DEPRECATED: replace with continentSize;
    const maskStrength = normalisedDistance * settings.squishFactor * sizeModifier;
    const landMask = Math.max(0, 1.0 - Math.pow(maskStrength, 3.0));

    // pass for elevation
    const baseLand = MapGenerator.getStandardfBm(sampleX, sampleY, 4, noise);
    const mountainSpines = MapGenerator.getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6, noise);

    // @TODO: dome mountains - this implementation doesnt work
    // Isolated Dome Mountains / Batholiths (Billow Noise)
    // const domeFreq = 14.0 / meta.worldWidth;
    // const mountainDomes = MapGenerator.getBillowfBm(sampleX * 1.3, sampleY * 1.3, domeFreq, 6, noise);
    // // Isolate domes into clusters using a low frequency distribution mask
    // const distNoise = noise.noise2D(globalX * (3.0 / meta.worldWidth), globalY * (3.0 / meta.worldWidth));
    // const domeDistribution = Math.max(0, distNoise);
    // const domeMountains = Math.pow(mountainDomes, 2.0) * domeDistribution * 1.5;
    // elevation += domeMountains * 0.35;  // blend domes
    

    // blend spines to specific elevations
    let spineBlendMask = (baseLand * 0.3) + (mountainSpines * 0.85);
    if (spineBlendMask > settings.seaLevel) {
        const relativeHeight = spineBlendMask - settings.seaLevel;
        spineBlendMask = settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4)

    }
    
    let elevation = Math.max(0, Math.min(1.0, spineBlendMask * landMask))

    if (spineBlendMask > settings.beachLevel) {
        const t = (elevation - settings.beachLevel) / (1.0 - settings.beachLevel);
        const smoothT = t * t * (3.0 - 2.0 * t);
        const inflatedTarget = elevation * 1.15;

        elevation = elevation + (inflatedTarget - elevation) * smoothT;
        if (elevation > 1.0) elevation = 1.0; // clamp to max elevation
    }


    /////////////////////////////
    //   subterranean layers   //
    /////////////////////////////
    // Generates internal geological metadata to drive mechanics and visuals
    const geoFreq = 5.0 / meta.worldWidth;
    const geoNoise = (noise.noise2D(globalX * geoFreq, globalY * geoFreq) + 1.0) * 0.5;

    let primaryRock: BasementRockType = 'sedimentary';
    if (elevation > settings.mountainLevel) {
        primaryRock = geoNoise > 0.4 ? 'granite' : 'basalt'; // Igneous basement rock cores
    } else {
        primaryRock = geoNoise > 0.5 ? 'limestone' : 'sandstone'; // Basins and shelves
    }

    const geology: SubterraneanLayer = {
        bedrockDepth: Math.floor((1.0 - elevation) * 120 + 20),
        sedimentaryThickness: elevation > settings.mountainLevel ? Math.floor(geoNoise * 10) : Math.floor(geoNoise * 80 + 20),
        primaryRockType: primaryRock
    };

    return { elevation, geology }
}

// finds the direction of the steepest ascent
export function nearestTileSlopeVector(
        globalX: number, globalY: number,
        settings: ChunkSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise
    ): SlopeVector {
    const elevWest = getGlobalTileComposition(globalX - 1, globalY, settings, meta, noise).elevation;
    const elevEast = getGlobalTileComposition(globalX + 1, globalY, settings, meta, noise).elevation;
    const elevNorth = getGlobalTileComposition(globalX, globalY - 1, settings, meta, noise).elevation;
    const elevSouth = getGlobalTileComposition(globalX, globalY + 1, settings, meta, noise).elevation;

    const gx = (elevEast - elevWest) / 2.0;
    const gy = (elevSouth - elevNorth) / 2.0;
    const slope = Math.sqrt(gx * gx + gy * gy);

    return { slope, gradient: { x: gx, y: gy } };
}

export function nearestTileSlopeAspect(
    globalX: number, globalY: number,
    settings: ChunkSettings,
    meta: GlobalGenerationMeta,
    noise: OpenSimplexNoise
): SlopeAspect {
    const vect = nearestTileSlopeVector(globalX, globalY, settings, meta, noise);
    if (vect.slope < 0.01) {
        return { ...vect, angleDeg: -1, cardinalDir: 'FLAT' };
    }

    const { x: gx, y: gy } = vect.gradient;
    let radians = Math.atan2(-gy, gx);
    let angleDeg = (90.0 - (radians * 180.0 / Math.PI)) % 360.0;
    if (angleDeg < 0) angleDeg += 360.0;

    const directions = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
    const index = Math.round(angleDeg / 45.0) % 8;
    const cardinalDir = directions[index];

    return { ...vect, angleDeg, cardinalDir };
}