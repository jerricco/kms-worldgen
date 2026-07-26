import type { BiomeType } from './biomes'

export interface Vector2D {
    x: number;
    y: number;
}

export type SlopeVector = {
    gradient: Vector2D; // Points in the direction of steepest ascent
    slope: number;      // The steepness magnitude (0.0 = flat, higher = steeper)
}

export type SlopeAspect = SlopeVector & {
    angleDeg: number; // Angle from 0 (North) to 360 clockwise. -1 if flat.
    cardinalDir: string; // "N", "NE", "E", "SE", "S", "SW", "W", "NW", or "FLAT"
}

export type Grid = Tile[][]
export type Tile = {
    id: string,
    biome: BiomeType | null,
    region: RegionConfig,
    elevation: number,
}