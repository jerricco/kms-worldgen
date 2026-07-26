import type { REGION_CONFIG as RegionConfig } from '../data/regions'
import { REGIONS } from '../data/regions'
import { BiomeType } from '../data/biomes'
import { SeededRandom } from './seed'
import { OpenSimplexNoise } from './noise'

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

// dynamic settings
export type RiverSetting = {
    name: string,
    count: number,
    bedWidth: number, // in tiles
    windiness: number, // 0 straight -> 2.5 extroime
    genThreshold: number, // The amount to randomly vary the river cout with
};

export type MapSettings = {
    // altitude settings
    peakLevel: number,
    treeLevel: number,
    hillLevel: number,
    plainLevel: number,
    beachLevel: number,
    seaLevel: number,
    trenchLevel: number,
    abyssalLevel: number,
    // generation levers
    macroScale: number, 
    squishFactor: number, 
    mapFenceSize: number,
    isIsland: boolean,
    islandRadius: number,
    // allocation settings
    minLakeSize: number,
    // altitude climatic settings
    minSnowLine: number, 
    rivers: {
        MAJOR: RiverSetting,
        MINOR: RiverSetting,
    },
}

export const defaultSettings: MapSettings = {
    peakLevel: 0.91,
    treeLevel: 0.74,
    hillLevel: 0.60,
    plainLevel: 0.48,
    beachLevel: 0.34,
    seaLevel: 0.32,
    trenchLevel: 0.16,
    abyssalLevel: 0.1,
    macroScale: 0.0045,
    squishFactor: 1.0,
    minLakeSize: 16,
    minSnowLine: 0.61,
    mapFenceSize: 4,
    // isIsland: true,
    isIsland: true, // @DEBUG
    islandRadius: 0.80,
    rivers: {
        MAJOR: { name: 'MAJOR_RIVER', count: 5, genThreshold: 1, bedWidth: 3, windiness: 0.8 },
        MINOR: { name: 'RIVER', count: 10, genThreshold: 1, bedWidth: 1, windiness: 1.6 },
    }
}

export class MapGenerator {
    static DEFAULT_WIDTH = 750;
    static DEFAULT_HEIGHT = 750;
    static BIOMES_MAX = 64;

    public width: number;
    public height: number;

    public seed: string;
    public rng: SeededRandom;
    public noise: OpenSimplexNoise;

    public grid: Grid;
    public settings: MapSettings;

    constructor(seed: string, width?: number, height?: number, config?: MapSettings) {
        // config
        this.settings = { ...defaultSettings, ...config };

        // construction
        this.width = width || MapGenerator.DEFAULT_WIDTH
        this.height = height || MapGenerator.DEFAULT_HEIGHT
        this.seed = seed || 'aborio rice'
        this.rng = new SeededRandom(seed);
        this.noise = new OpenSimplexNoise(this.rng);
        this.grid = MapGenerator.getGrid(this.width, this.height, this.settings.trenchLevel)

        MapGenerator.generate(this.width, this.height, this.settings, this.grid, this.noise, this.rng)
    }

    static getGrid(width: number, height: number, trenchLevel: number): Tile[][] {
        return Array.from({ length: width }, (_, x) =>
            Array.from({ length: height }, (_, y) => ({
                id: `${x + 1},${y + 1}`,
                biome: null,
                region: REGIONS.DEEP_OCEAN,
                elevation: trenchLevel,
            }))
        );
    }

    static #getDomainWarpedSample(
        x: number, 
        y: number, 
        mOffsetX: number, 
        mOffsetY: number, 
        settings: MapSettings, 
        noise: OpenSimplexNoise,
    ): { sampleX: number, sampleY: number } {
        const warpX = noise.noise2D((x + 200) * 0.018, (y + 200) * 0.018) * 45;
        const warpY = noise.noise2D((x - 200) * 0.018, (y - 200) * 0.018) * 45;

        const sampleX = (x + mOffsetX + warpX) * settings.macroScale;
        const sampleY = (y + mOffsetY + warpY) * settings.macroScale;

        return { sampleX, sampleY };
    }

    static generate(width: number, height: number, settings: MapSettings, grid: Grid, noise: OpenSimplexNoise, rng: SeededRandom) {
        const centerX = width / 2;
        const centerY = height / 2;
        const maxRadius = Math.sqrt(centerX * centerX + centerY * centerY);

        // Substantial offsets derived from seed to guarantee absolute structural variance
        const mOffsetX = rng.nextRange(10000, 90000);
        const mOffsetY = rng.nextRange(10000, 90000);

        for (let x = 0; x < width; x++) {
            for (let y = 0; y < height; y++) {
                const tile = grid[x][y];

                // use domain warping to create sinusoidal lines for coastlines
                const { sampleX, sampleY } = MapGenerator.#getDomainWarpedSample(x, y, mOffsetX, mOffsetY, settings, noise)
                
                // generate distorted distance gradient from the center of the map
                // this creates a mask shape that looks like an island.
                const dx = x - centerX, dy = y - centerY;                            // displacement from center
                const edgeWarp = noise.noise2D(sampleX * 1.2, sampleY * 1.2) * 0.12; // warp edges of current noise
                const normalizedDistance = (Math.sqrt(dx * dx + dy * dy) / maxRadius) + edgeWarp; // fuzzy circular edge distance


                // Create a radial boundary to force the edges toward oceans.
                const sizeModifier = 1.0 / settings.islandRadius;
                const maskStrength = normalizedDistance * settings.squishFactor * sizeModifier;
                const islandMask = Math.max(0, 1.0 - Math.pow(maskStrength, 4.0));

                // 3. ELEVATION PASSES
                const baseLand = MapGenerator.#getStandardfBm(sampleX, sampleY, 4, noise);
                const mountainSpines = MapGenerator.#getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6, noise);

                // Blend the foundational base land with sharp ridged tectonic spines
                // The multiplications add up to 100 to ensure a certain fBm will take up that proportion of elevation
                let mixedElevation = (baseLand * 0.3) + (mountainSpines * 0.85);

                // Apply a non-linear steepening curve to values that push past sea/beach heights
                if (mixedElevation > settings.seaLevel) {
                    // Isolate the height above sea level, raise it exponentially, and magnify it
                    const relativeHeight = mixedElevation - settings.seaLevel;
                    mixedElevation = settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4);
                }

                // Apply the high-exponent perimeter cliff mask
                const maskedElevation = settings.isIsland ? mixedElevation * islandMask : mixedElevation
                let finalElevation = Math.max(0, Math.min(1.0, maskedElevation));

                // Inflate and clamp the final elevation structure so that mountains form more readily
                if (finalElevation > settings.beachLevel) {
                    // 1. Normalize the land height between beach level (0.0) and the maximum possible height (1.0)
                    const t = (finalElevation - settings.beachLevel) / (1.0 - settings.beachLevel);

                    // 2. Smoothstep easing function (smoothes the transition out from the beach and into the peaks)
                    const smoothT = t * t * (3.0 - 2.0 * t);

                    // 3. Linearly interpolate between the original height and a slightly inflated target height (e.g., boosted by 15%)
                    const inflatedTarget = finalElevation * 1.15;

                    // 4. Blend smoothly: uses more of the original height near beaches, and blends toward the inflated target near peaks
                    finalElevation = finalElevation + (inflatedTarget - finalElevation) * smoothT;

                    // 5. Soft safety clamp (should never hit a hard flat edge due to the easing curve)
                    if (finalElevation > 1.0) finalElevation = 1.0;
                }

                tile.elevation = finalElevation;

                const { slope, cardinalDir } = MapGenerator.findNearestSlopeAspect(x, y, width, height, grid)
                // Tectonic Rule: Only allow cliffs to form if they face West, North-West, or South-West
                // This simulates a mountain range being shoved from the East!
                const tectonicallyShoved = ["W", "NW", "SW"].includes(cardinalDir)

                // 6. REGION TRANSLATION PIPELINE
                if (tile.elevation < settings.seaLevel) {
                    if (tile.elevation < settings.abyssalLevel) {
                        tile.region = REGIONS.ABYSSAL;
                    } else if (tile.elevation < settings.trenchLevel) {
                        tile.region = REGIONS.DEEP_OCEAN;
                    } else {
                        tile.region = REGIONS.OCEAN;
                    }
                } else if (tile.elevation < settings.beachLevel) {
                    tile.region = REGIONS.BEACH;
                } else if (tile.elevation < settings.plainLevel) {
                    tile.region = REGIONS.PLAINS;
                } else if (tile.elevation < settings.hillLevel) {
                    tile.region = REGIONS.HILLS;
                } else if (tile.elevation < settings.treeLevel) {
                    tile.region = slope > 0.4 && tectonicallyShoved ? REGIONS.CLIFF : REGIONS.FOOTHILLS;
                } else if (tile.elevation < settings.peakLevel) {
                    tile.region = slope > 0.5 && tectonicallyShoved ? REGIONS.CLIFF : REGIONS.MOUNTAIN;
                } else {
                    tile.region = REGIONS.PEAK;
                }
            }
        }
    }

    // CLIMATE:
    // - Create climate system so that I can tell where certain features should spawn

    // LAKES: 
    // - All enclosed bodies of water inside land must be redesignated as LAKE
    // - Some small local depressions can be made at across the map. Also, isolated landlocked BEACH will be carved out as a BASIN 
    // - If the climatic conditions suit, fill a BASIN with water.
    // - Lakes of a particular altitude can become river sources (these don't include lakes later generated by rivers)

    // RIVERS:
    // - Source from high altitude tiles (MOUNTAIN, or HILL) or as the edge tile of basin LAKEs
    // - The larger the lake, the more likely it will spawn a river
    // - Create major rivers which then have minor tributaries flowing out of them
    // - Rivers below HILLS will later get wrapped in RIVERBANK tiles
    // - Rivers should path with the following priorities:
    // - 1. Check if it can move downhill
    // - 2. If nowhere to move downhill exists, check if the river has ended up in a basin and fill it with LAKE.
    // - 3. If no basin, find the nearest OCEAN tile. Then...
    // -     3.1. check if that path is crossed by a HILL, if it is, snake toward the hill on encountering the hill,
    // -     3.2. if we encounter the hill, do the basin check/fill. if no hill is there, snake toward the OCEAN, until elevation resumes downhill
    // -     3.3. If a basin fill was successful on terminating against a hill and the river is MAJOR, carve snaking a canyon through it until we reach PLAIN.
    // - 4. If a MAJOR river has terminated in a LAKE, pick the lowest altitude tile on the opposing half of the LAKE body and begin a new MINOR river from there using these same rules


    ///////////////////////////////
    //     UTILITY FUNCTIONS     //
    ///////////////////////////////
    // POSSIBLY DEPRECATED
    // find the vector from a tile to the nearest tile of a type of REGION
    static findNearestTileVector(sx: number, sy: number, width: number, height: number, grid: Grid, findingRegion: RegionConfig): { x: number, y: number } | null {
        const queue: { x: number, y: number }[] = [{ x: sx, y: sy }];
        const visited = new Set<string>([`${sx},${sy}`]);
        const scanDirs = [{ dx: 0, dy: -1 }, { dx: 0, dy: 1 }, { dx: -1, dy: 0 }, { dx: 1, dy: 0 }];
        let head = 0;

        while (head < queue.length && queue.length < 1500) {
            const curr = queue[head++];
            if (grid[curr.x][curr.y].region.name === findingRegion.name) return curr;

            for (const d of scanDirs) {
                const nx = curr.x + d.dx, ny = curr.y + d.dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                    const key = `${nx},${ny}`;
                    if (!visited.has(key)) { visited.add(key); queue.push({ x: nx, y: ny }); }
                }
            }
        }
        return null;
    }

    // Attempt to detect whether the current selected tile is within a basin.
    static detectBasin(sx: number, sy: number, width: number, height: number, grid: Grid): { x: number, y: number }[] | null {
        const queue: { x: number, y: number }[] = [{ x: sx, y: sy }];
        const visited = new Set<string>([`${sx},${sy}`]);
        const startElevation = grid[sx][sy].elevation;
        const scanDirs = [{ dx: 0, dy: -1 }, { dx: 0, dy: 1 }, { dx: -1, dy: 0 }, { dx: 1, dy: 0 }];

        let head = 0;
        while (head < queue.length) {
            const curr = queue[head++];
            if (queue.length > 200) return null;

            for (const d of scanDirs) {
                const nx = curr.x + d.dx, ny = curr.y + d.dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) return null;

                const key = `${nx},${ny}`;
                if (visited.has(key)) continue;
                visited.add(key);

                if (grid[nx][ny].elevation < startElevation - 0.01) return null;
                if (grid[nx][ny].elevation <= startElevation + 0.02) { queue.push({ x: nx, y: ny }); }
            }
        }
        return queue;
    }

    // NOISE GENERATION
    // Fractional Brownian Motion for ridged multi-fractal noise structures
    // This is scienceish for forked mountain range structures.
    static #getRidgedfBm(nx: number, ny: number, octaves: number, noise: OpenSimplexNoise): number {
        let value = 0;
        let amplitude = 1.0;
        let frequency = 1.0;
        let maxValue = 0;

        for (let i = 0; i < octaves; i++) {
            // Signal inversion maps absolute valleys into sharp ridged spines
            let signal = noise.noise2D(nx * frequency, ny * frequency);
            signal = 1.0 - Math.abs(signal);

            // Square the signal to sharpen the peak crests
            signal = signal * signal;

            value += signal * amplitude;
            maxValue += amplitude;

            frequency *= 2.15;  // Lacunarity
            amplitude *= 0.5;  // Persistence
        }
        return value / maxValue;
    }

    // Standard fBm for organic landmass foundational plateaus
    // More science language for when plains form outwards from mountain ridges.
    static #getStandardfBm(nx: number, ny: number, octaves: number, noise: OpenSimplexNoise): number {
        let value = 0;
        let amplitude = 1.0;
        let frequency = 1.0;
        let maxValue = 0;

        for (let i = 0; i < octaves; i++) {
            const signal = (noise.noise2D(nx * frequency, ny * frequency) + 1.0) / 2.0;
            value += signal * amplitude;
            maxValue += amplitude;
            frequency *= 2.0;
            amplitude *= 0.54;
        }
        return value / maxValue;
    }

    // LOCAL TERRAIN ANALYSIS
    // finds the direction of the steepest ascent
    static findNearestSlopeVector(x: number, y: number, width: number, height: number, grid: Grid): SlopeVector {
        // 1. Establish cardinal neighbour coordinates with boundary clamping
        const westX  = Math.max(0, x - 1);
        const eastX  = Math.min(width - 1, x + 1);
        const northY = Math.max(0, y - 1);
        const southY = Math.min(height - 1, y + 1);

        // 2. Sample elevations from the grid
        const elevationWest  = grid[westX][y].elevation;
        const elevationEast  = grid[eastX][y].elevation;
        const elevationNorth = grid[x][northY].elevation;
        const elevationSouth = grid[x][southY].elevation;
        
        // 3. Compute the rate of change along both axes (Finite Differences)
        // Dividing by the distance between samples (2 tiles) normalizes the gradient scale
        const gx = (elevationEast - elevationWest) / 2.0;
        const gy = (elevationSouth - elevationNorth) / 2.0;

        // 4. Calculate overall steepness using the Pythagorean theorem
        const slope = Math.sqrt(gx * gx + gy * gy);

        return { slope, gradient: { x: gx, y: gy } };
    }

    static findNearestSlopeAspect(x: number, y: number, width: number, height: number, grid: Grid): SlopeAspect {
        const vect = MapGenerator.findNearestSlopeVector(x, y, width, height, grid);
        if (vect.slope < 0.01) {
            return { ...vect, angleDeg: -1, cardinalDir: 'FLAT' };
        }

        // 3. Compute the angle using Math.atan2. 
        // Inverting gy aligns the angle with standard screen coordinate systems (Y down)
        const { x: gx, y: gy } = vect.gradient;
        let radians = Math.atan2(-gy, gx)
        let angleDeg = (90.0 - (radians * 180.0 / Math.PI)) % 360.0;
        if (angleDeg < 0) angleDeg += 360.0;

        // 4. Map degrees to a structural 8-way compass string
        const directions = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        const index = Math.round(angleDeg / 45.0) % 8;
        const cardinalDir = directions[index];

        return { ...vect, angleDeg, cardinalDir };
    }

    // IMPROVEMENTS
    // DECOUPLED FEATURE MASKING
    // DYNAMIC THRESHOLDING
}
