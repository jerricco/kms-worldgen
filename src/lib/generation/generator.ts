import type { REGION_CONFIG as RegionConfig } from './regions'
import { RegionID } from './regions'
import { SeededRandom } from './seed'
import { OpenSimplexNoise } from './noise'
import type { Grid, SlopeAspect, SlopeVector, Tile } from './types';
import { Chunk } from './chunk';

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

const GeneratorDefaults: MapSettings = {
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
    isIsland: false, // @DEBUG
    islandRadius: 0.80,
    rivers: {
        MAJOR: { name: 'MAJOR_RIVER', count: 5, genThreshold: 1, bedWidth: 3, windiness: 0.8 },
        MINOR: { name: 'RIVER', count: 10, genThreshold: 1, bedWidth: 1, windiness: 1.6 },
    }
}

interface GlobalGenerationMeta {
    mOffsetX: number;
    mOffsetY: number;
    cosA: number;
    sinA: number;
}

export class MapGenerator {
    static DEFAULT_SEED = 'aborio rice';
    static DEFAULT_WIDTH = 750;
    static DEFAULT_HEIGHT = 750;
    static BIOMES_MAX = 64;
    static GENERATOR_DEFAULTS = GeneratorDefaults;

    public width: number;
    public height: number;

    public seed: string;
    public rng: SeededRandom;
    public noise: OpenSimplexNoise;

    public meta: GlobalGenerationMeta;
    public settings: MapSettings;

    public chunks: Chunk[] = [];

    constructor(seed: string, width?: number, height?: number, config?: MapSettings) {
        // config
        this.settings = { ...GeneratorDefaults, ...config };
        
        // construction
        this.width = width || MapGenerator.DEFAULT_WIDTH
        this.height = height || MapGenerator.DEFAULT_HEIGHT
        this.seed = seed || MapGenerator.DEFAULT_SEED
        this.rng = new SeededRandom(seed);
        this.meta = MapGenerator.generateGlobalMetadata(this.rng);
        this.noise = new OpenSimplexNoise(this.rng);
        
        // @DEBUG
        // generate the first 15 chunks each way to simulate 750x750
        // @TODO: check this shit, because idk if I'm storing chunks properly for later
        for (let x = 0; x < 15; x++) {
            for (let y = 0; y < 15; y++) {
                const localIndex = Chunk.getLocalIndex(x, y);
                this.chunks[localIndex] = MapGenerator.generateChunk(x, y, this.settings, this.meta, this.noise)
            }
        }
    }

    // @TODO: adjust it so that the world size is by default 256*256 chunks (12800*12800)
    static WORLD_WIDTH = 750; // 12800;
    static WORLD_HEIGHT = 750; // 12800;

    // @TODO: currently these sit here, I should sort them into GlobalGenerationMeta or MapSettings
    static CENTER_X = this.WORLD_WIDTH / 2;
    static CENTER_Y = this.WORLD_HEIGHT / 2;
    static STRETCH_X = 0.7;
    static STRETCH_Y = 1.3;
    static OCEAN_CLAMP = 0.85;
    static MAX_RADIUS = Math.sqrt(this.CENTER_X * this.CENTER_X) * this.OCEAN_CLAMP;
    static BUFFER_FACTOR = 0.05;
    static BUFFER_X = this.WORLD_WIDTH * this.BUFFER_FACTOR
    static BUFFER_Y = this.WORLD_HEIGHT * this.BUFFER_FACTOR

    static generateGlobalMetadata(rng: SeededRandom): GlobalGenerationMeta {
        if (!rng) {
            throw new Error('We need an rng instance!')
        }
        const randomAngle = rng.nextRange(0, Math.PI * 2);
        return {
            mOffsetX: rng.nextRange(10000, 90000),
            mOffsetY: rng.nextRange(10000, 90000),
            cosA: Math.cos(randomAngle),
            sinA: Math.sin(randomAngle),
        }
    }

    static generateChunk(chunkX: number, chunkY: number, settings: MapSettings, meta: GlobalGenerationMeta, noise: OpenSimplexNoise) {
        const chunk = new Chunk(chunkX, chunkY);
        
        for (let x = 0; x < Chunk.SIZE; x++) {
            for (let y = 0; y < Chunk.SIZE; y++) {
                const globalX = chunkX * Chunk.SIZE + x;
                const globalY = chunkY * Chunk.SIZE + y;

                // NO, OUT OF BOUND - BACK TIGER!
                if (globalX >= this.WORLD_WIDTH || globalY >= this.WORLD_HEIGHT || globalX < 0 || globalY < 0)
                    continue;

                const elevation = this.getGlobalTileElevation(globalX, globalY, settings, meta, noise);
                const localIndex = Chunk.getLocalIndex(x, y);

                chunk.elevations[localIndex] = elevation; 
                this.#determineRegions(globalX, globalY, localIndex, chunk, elevation, settings, meta, noise);
            }
        }

        return chunk
    }

    static getGlobalTileElevation(
        globalX: number, globalY: number, 
        settings: MapSettings, 
        meta: GlobalGenerationMeta, 
        noise: OpenSimplexNoise
    ) {
        // clamp coords inside world border
        globalX = Math.max(0, Math.min(this.WORLD_WIDTH - 1, globalX));
        globalY = Math.max(0, Math.min(this.WORLD_HEIGHT - 1, globalY));

        // worldwide ocean boundary proximity
        const distToLeft = globalX;
        const distToRight = (this.WORLD_WIDTH - 1) - globalX;
        const distToTop = globalY;
        const distToBottom = (this.WORLD_HEIGHT - 1) - globalY;

        const edgeXFactor = Math.min(1.0, Math.min(distToLeft, distToRight) / this.BUFFER_X);
        const edgeYFactor = Math.min(1.0, Math.min(distToTop, distToBottom) / this.BUFFER_Y);
        const globalEdgeFactor = edgeXFactor * edgeYFactor;

        const { sampleX, sampleY }
            = this.#getDomainWarpedSample(globalX, globalY, meta.mOffsetX, meta.mOffsetY, settings, noise);

        const dx = globalX - this.CENTER_X;
        const dy = globalY - this.CENTER_Y;
        const rx = dx * meta.cosA - dy * meta.sinA;
        const ry = dx * meta.sinA + dy * meta.cosA;

        // macro mask for bays and gulfs.
        const maskWarpStrength = 0.25 * globalEdgeFactor;
        const maskWarpX = noise.noise2D(sampleX * 0.4, sampleY * 0.4) * maskWarpStrength;
        const maskWarpY = noise.noise2D(sampleX * 0.4 + 50, sampleY * 0.4 + 50) * maskWarpStrength;

        const finalMaskDist = Math.sqrt(
            Math.pow((rx + maskWarpX * this.CENTER_X) * this.STRETCH_X, 2) +
            Math.pow((ry + maskWarpY * this.CENTER_Y) * this.STRETCH_Y, 2)
        );

        const normalisedDistance = finalMaskDist / this.MAX_RADIUS;
        const sizeModifier = 1.0 / settings.islandRadius // @DEPRECATED: replace with continentSize;
        const maskStrength = normalisedDistance * settings.squishFactor * sizeModifier;
        const landMask = Math.max(0, 1.0 - Math.pow(maskStrength, 3.0));

        // pass for elevation
        const baseLand = this.#getStandardfBm(sampleX, sampleY, 4, noise);
        const mountainSpines = this.#getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6, noise);

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

    static #determineRegions(
        globalX: number,
        globalY: number,
        localIndex: number,
        chunk: Chunk, // Pass the high performance structural Chunk instance
        elevation: number,
        settings: MapSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise
    ) {
        const { slope, cardinalDir } = MapGenerator.nearestTileSlopeAspect(globalX, globalY, settings, meta, noise);
        const tectonicallyShoved = ["W", "NW", "SW"].includes(cardinalDir);

        // @TODO: replace elevation based regions with climatic regions
        let region: RegionID = RegionID.UNASSIGNED;

        // MARINE regions - first establish a seafloor
        if (elevation < settings.seaLevel) {
            if (elevation < settings.abyssalLevel) {
                region = RegionID.ABYSSAL;
            } else if (elevation < settings.trenchLevel) {
                region = RegionID.DEEP_OCEAN;
            } else {
                region = RegionID.OCEAN;
            }
        } 
        // TRANSITIONAL regions - create terminals between land and sea
        else if (elevation < settings.beachLevel) {
            region = RegionID.BEACH;
        }
        // FLAT TERRESTRIAL regions - mainland
        else if (elevation < settings.plainLevel) {
            region = RegionID.PLAIN;
        }
        // MOUNTAINOUS TERRESTRIAL REGIONS - higher elevations
        else if (elevation < settings.hillLevel) {
            region = RegionID.HILL;
        }
        else if (elevation < settings.peakLevel) {
            region = slope > 0.5 && tectonicallyShoved ? RegionID.CLIFF : RegionID.MOUNTAIN;
        }

        chunk.regionIds[localIndex] = region;
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
    static nearestTileSlopeVector(
        globalX: number, globalY: number,
        settings: MapSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise
    ): SlopeVector {
        const elevWest = this.getGlobalTileElevation(globalX - 1, globalY, settings, meta, noise);
        const elevEast = this.getGlobalTileElevation(globalX + 1, globalY, settings, meta, noise);
        const elevNorth = this.getGlobalTileElevation(globalX, globalY - 1, settings, meta, noise);
        const elevSouth = this.getGlobalTileElevation(globalX, globalY + 1, settings, meta, noise);

        const gx = (elevEast - elevWest) / 2.0;
        const gy = (elevSouth - elevNorth) / 2.0;
        const slope = Math.sqrt(gx * gx + gy * gy);

        return { slope, gradient: { x: gx, y: gy } };
    }

    static nearestTileSlopeAspect(
        globalX: number, globalY: number,
        settings: MapSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise
    ): SlopeAspect {
        const vect = MapGenerator.nearestTileSlopeVector(globalX, globalY, settings, meta, noise);
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

    // IMPROVEMENTS
    // DECOUPLED FEATURE MASKING
    // DYNAMIC THRESHOLDING
}
