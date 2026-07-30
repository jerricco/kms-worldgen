////////////////////////////////////////////////////////////
// Functions for analysing the noise & terrain generation //
////////////////////////////////////////////////////////////
import type { ChunkSettings } from "./chunk";
import { MapGenerator, type GlobalGenerationMeta } from "./generator";
import type { OpenSimplexNoise } from "./noise";
import type { SlopeAspect, SlopeVector } from "./types";

// finds the direction of the steepest ascent
export function nearestTileSlopeVector(
        globalX: number, globalY: number,
        settings: ChunkSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise
    ): SlopeVector {
    const elevWest = MapGenerator.getGlobalTileComposition(globalX - 1, globalY, settings, meta, noise).elevation;
    const elevEast = MapGenerator.getGlobalTileComposition(globalX + 1, globalY, settings, meta, noise).elevation;
    const elevNorth = MapGenerator.getGlobalTileComposition(globalX, globalY - 1, settings, meta, noise).elevation;
    const elevSouth = MapGenerator.getGlobalTileComposition(globalX, globalY + 1, settings, meta, noise).elevation;

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