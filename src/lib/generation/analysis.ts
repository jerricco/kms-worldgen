////////////////////////////////////////////////////////////
// Functions for analysing the noise & terrain generation //
////////////////////////////////////////////////////////////
import type { ChunkSettings } from "./chunk";
import { MapGenerator, type GlobalGenerationMeta } from "./generator";
import type { OpenSimplexNoise } from "./noise";
import type { SlopeAspect, SlopeVector } from "./types";

export function getGlobalTileElevation(
    globalX: number, globalY: number,
    settings: ChunkSettings,
    meta: GlobalGenerationMeta,
    noise: OpenSimplexNoise
) {
    // clamp coords inside world border
    globalX = Math.max(0, Math.min(meta.maxX - 1, globalX));
    globalY = Math.max(0, Math.min(meta.maxY - 1, globalY));

    // worldwide ocean boundary proximity
    const distToLeft = globalX;
    const distToRight = (meta.maxX - 1) - globalX;
    const distToTop = globalY;
    const distToBottom = (meta.maxY - 1) - globalY;

    const edgeXFactor = Math.min(1.0, Math.min(distToLeft, distToRight) / meta.bufferX);
    const edgeYFactor = Math.min(1.0, Math.min(distToTop, distToBottom) / meta.bufferY);
    const globalEdgeFactor = edgeXFactor * edgeYFactor;

    const { sampleX, sampleY }
        = MapGenerator.getDomainWarpedSample(globalX, globalY, meta.mOffsetX, meta.mOffsetY, settings, noise);

    const dx = globalX - meta.centerX;
    const dy = globalY - meta.centerY;
    const rx = dx * meta.cosA - dy * meta.sinA;
    const ry = dx * meta.sinA + dy * meta.cosA;

    // macro mask for bays and gulfs.
    const maskWarpStrength = 0.25 * globalEdgeFactor;
    const maskWarpX = noise.noise2D(sampleX * 0.4, sampleY * 0.4) * maskWarpStrength;
    const maskWarpY = noise.noise2D(sampleX * 0.4 + 50, sampleY * 0.4 + 50) * maskWarpStrength;

    const finalMaskDist = Math.sqrt(
        Math.pow((rx + maskWarpX * meta.centerX) * meta.stretchX, 2) +
        Math.pow((ry + maskWarpY * meta.stretchY) * meta.stretchY, 2)
    );

    const normalisedDistance = finalMaskDist / meta.maxRadius;
    const sizeModifier = 1.0 / settings.islandRadius // @DEPRECATED: replace with continentSize;
    const maskStrength = normalisedDistance * settings.squishFactor * sizeModifier;
    const landMask = Math.max(0, 1.0 - Math.pow(maskStrength, 3.0));

    // pass for elevation
    const baseLand = MapGenerator.getStandardfBm(sampleX, sampleY, 4, noise);
    const mountainSpines = MapGenerator.getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6, noise);

    // blend spines to specific elevations
    let spineBlendMask = (baseLand * 0.3) + (mountainSpines * 0.85);
    if (spineBlendMask > settings.seaLevel) {
        const relativeHeight = spineBlendMask - settings.seaLevel;
        spineBlendMask = settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4)
    }

    let elevationMask = Math.max(0, Math.min(1.0, spineBlendMask * landMask))
    if (spineBlendMask > settings.beachLevel) {
        const t = (elevationMask - settings.beachLevel) / (1.0 - settings.beachLevel);
        const smoothT = t * t * (3.0 - 2.0 * t);
        const inflatedTarget = elevationMask * 1.15;

        elevationMask = elevationMask + (inflatedTarget - elevationMask) * smoothT;
        if (elevationMask > 1.0) elevationMask = 1.0; // clamp to max elevation
    }

    return elevationMask;
}

// finds the direction of the steepest ascent
export function nearestTileSlopeVector(
        globalX: number, globalY: number,
        settings: ChunkSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise
    ): SlopeVector {
    const elevWest = getGlobalTileElevation(globalX - 1, globalY, settings, meta, noise);
    const elevEast = getGlobalTileElevation(globalX + 1, globalY, settings, meta, noise);
    const elevNorth = getGlobalTileElevation(globalX, globalY - 1, settings, meta, noise);
    const elevSouth = getGlobalTileElevation(globalX, globalY + 1, settings, meta, noise);

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