import type { REGION, REGION_CONFIG as RegionConfig } from './regions'
import { REGIONS } from './regions'
import { SeededRandom } from './seed'
import { OpenSimplexNoise } from './noise'
import type { Grid, SlopeAspect, SlopeVector, Tile } from './types';
import { random } from 'playcanvas/build/playcanvas/src/core/math/random.js';

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

    public grid: Grid;
    public settings: MapSettings;

    constructor(seed: string, width?: number, height?: number, config?: MapSettings) {
        // config
        this.settings = { ...GeneratorDefaults, ...config };

        // construction
        this.width = width || MapGenerator.DEFAULT_WIDTH
        this.height = height || MapGenerator.DEFAULT_HEIGHT
        this.seed = seed || MapGenerator.DEFAULT_SEED
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

        // Substantial offsets derived from seed to guarantee absolute structural variance
        const mOffsetX = rng.nextRange(10000, 90000);
        const mOffsetY = rng.nextRange(10000, 90000);

        const randomAngle = rng.nextRange(0, Math.PI * 2);
        const cosA = Math.cos(randomAngle);
        const sinA = Math.sin(randomAngle)

        const stretchX = 0.7;
        const stretchY = 1.3; 
        // @NOTE: I may be able to use settings.landlocked or something to set the clamp multiplier?
        // @NOTE: this clamp multiplier determines proportion of the map size it will try to use
        const oceanClamp = settings.isIsland ? 0.85 : 1
        const maxRadius = Math.sqrt(centerX * centerX + centerY * centerY) * oceanClamp; 

        // @NOTE: config?
        const bufferFactor = 0.05
        const bufferX = width * bufferFactor;
        const bufferY = height * bufferFactor;

        // determine if the current elevation point is a bubble to insert at the map edge where other land noise crosses.
        const { bridgeEdge, bridgeX, bridgeY }
            = MapGenerator.checkNoiseNearBoundaries(width, height, mOffsetX, mOffsetY, noise, settings);

        for (let x = 0; x < width; x++) {
            for (let y = 0; y < height; y++) {
                const tile = grid[x][y];

                // check boundary proximity
                const distToLeft = x, distToRight = (width - 1) - x,
                      distToTop = y, distToBottom = (height - 1) - y;

                // Determine closeness multipliers (0.0 at edge, 1.0 safely inside buffer zone)
                const edgeXFactor = Math.min(1.0, Math.min(distToLeft, distToRight) / bufferX);
                const edgeYFactor = Math.min(1.0, Math.min(distToTop, distToBottom) / bufferY);

                // Combined factor: 0.0 right on any edge, 1.0 when comfortably in the center
                const globalEdgeFactor = edgeXFactor * edgeYFactor;

                // use domain warping to create sinusoidal lines for coastlines
                const { sampleX, sampleY } = MapGenerator.#getDomainWarpedSample(x, y, mOffsetX, mOffsetY, settings, noise)

                // eliptical distance gradient
                const dx = x - centerX, dy = y - centerY; // displacement from center
                const rx = dx * cosA - dy * sinA, ry = dx * sinA + dy * cosA;

                // macro mask warping
                // Low-frequency noise deforms the boundary mask drastically, carving huge bays/gulfs
                const maskWarpStrength = 0.25 * globalEdgeFactor;
                const maskWarpX = noise.noise2D(sampleX * 0.4, sampleY * 0.4) * maskWarpStrength;
                const maskWarpY = noise.noise2D(sampleX * 0.4 + 50, sampleY * 0.4 + 50) * maskWarpStrength;

                // Recompute distance with warped input space
                const finalMaskDist = Math.sqrt(
                    Math.pow((rx + maskWarpX * centerX) * stretchX, 2) +
                    Math.pow((ry + maskWarpY * centerY) * stretchY, 2)
                );

                const normalizedDistance = finalMaskDist / maxRadius;
                
                // Create a radial boundary to force the edges toward oceans.
                const sizeModifier = 1.0 / settings.islandRadius;
                const maskStrength = normalizedDistance * settings.squishFactor * sizeModifier;
                const landMask = Math.max(0, 1.0 - Math.pow(maskStrength, 3.0)); // @NOTE: was 4.0, changed for testing

                // elevation passes
                const baseLand = MapGenerator.#getStandardfBm(sampleX, sampleY, 4, noise);
                const mountainSpines = MapGenerator.#getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6, noise);

                // Blend the foundational base land with sharp ridged tectonic spines
                // The multiplications add up to 100 to ensure a certain fBm will take up that proportion of elevation
                let spineBlendMask = (baseLand * 0.3) + (mountainSpines * 0.85);
                if (spineBlendMask > settings.seaLevel) {
                    // Isolate the height above sea level, raise it exponentially, and magnify it
                    const relativeHeight = spineBlendMask - settings.seaLevel;
                    spineBlendMask = settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4);
                }

                
                let elevationMask = Math.max(0, Math.min(1.0, spineBlendMask * landMask))
                if (!settings.isIsland && bridgeEdge !== "none") {
                    // Define how far the land shoulder extends inward from the edge coordinate
                    const shoulderRadiusX = width * 0.25;
                    const shoulderRadiusY = height * 0.25;

                    const distortionStrength = 0.42; // Increase for more jagged/island-like shapes, decrease for smoother lines
                    const shoulderWarp = noise.noise2D(sampleX * 1.2, sampleY * 1.2) * distortionStrength;

                    // Calculate distance to our detected crossing anchor point
                    const distToBridgeX = (x - bridgeX) / shoulderRadiusX;
                    const distToBridgeY = (y - bridgeY) / shoulderRadiusY;
                    const distanceToBridge = Math.sqrt(distToBridgeX * distToBridgeX + distToBridgeY * distToBridgeY) + shoulderWarp;

                    if (distanceToBridge < 1.0) {
                        // Lateral falloff (0.0 at the sides of the shoulder, 1.0 along the central axis of the bridge)
                        const lateralWeight = Math.pow(1.0 - distanceToBridge, 1.5);

                        // 3. Compute how close we are to the actual map border edge (0.0 deep inland, 1.0 right on the edge line)
                        let edgeProximity = 0.0;
                        if (bridgeEdge === "left") edgeProximity = (shoulderRadiusX - x) / shoulderRadiusX;
                        if (bridgeEdge === "right") edgeProximity = (x - (width - 1 - shoulderRadiusX)) / shoulderRadiusX;
                        if (bridgeEdge === "top") edgeProximity = (shoulderRadiusY - y) / shoulderRadiusY;
                        if (bridgeEdge === "bottom") edgeProximity = (y - (height - 1 - shoulderRadiusY)) / shoulderRadiusY;

                        // Clamp proximity safety buffer between 0.0 and 1.0
                        edgeProximity = Math.max(0.0, Math.min(1.0, edgeProximity));

                        // 4. Calculate a dynamic elevation boost that peaks right at the map border line
                        // At the edge line, this adds up to +0.35 elevation, tapering to 0 as you move inland
                        const baseBoost = 0.12; // Flat baseline boost across the entire shoulder area
                        const edgeScaleBonus = 0.25 * edgeProximity; // Escalates terrain specifically near the border

                        elevationMask += (baseBoost + edgeScaleBonus) * lateralWeight;

                        // 5. Enforce a firm land floor at the edge so ocean never clips back through the shoulder axis
                        const structuralFloor = settings.beachLevel + 0.10 + (0.20 * edgeProximity);
                        const floorWeight = (1.0 - distanceToBridge); // Strongest right on the center axis line

                        const enforcedFloor = structuralFloor * floorWeight;
                        if (elevationMask < enforcedFloor) {
                            elevationMask = enforcedFloor;
                        }
                    }

                    // if (distanceToBridge < 1.0) {
                    //     // Smoothly ease the shoulder weight down to 0 at its radius boundary
                    //     const shoulderWeight = Math.pow(1.0 - distanceToBridge, 2.0);

                    //     // Determine the targeted elevation minimum for our continuous land bridge
                    //     const targetBridgeElevation = settings.beachLevel + 0.22;

                    //     // Blend the elevation upward exclusively inside this localized bubble zone
                    //     if (elevationMask < targetBridgeElevation) {
                    //         elevationMask = elevationMask + (targetBridgeElevation - elevationMask) * shoulderWeight;
                    //     }
                    // }
                }

                // inflate and clamp the final elevation structure so that mountains form more readily
                if (elevationMask > settings.beachLevel) {
                    const t = (elevationMask - settings.beachLevel) / (1.0 - settings.beachLevel);
                    const smoothT = t * t * (3.0 - 2.0 * t);
                    const inflatedTarget = elevationMask * 1.15;

                    elevationMask = elevationMask + (inflatedTarget - elevationMask) * smoothT;
                    if (elevationMask > 1.0) elevationMask = 1.0;
                }

                tile.elevation = elevationMask;

                // region determination pipeline
                this._determineTileRegion(x, y, width, height, grid, tile, settings)
            }
        }
    }

    static checkNoiseNearBoundaries(
        width: number, height: number, 
        mOffsetX: number, mOffsetY: number, 
        noise: OpenSimplexNoise, 
        settings: MapSettings
    ): { bridgeX: number, bridgeY: number, bridgeEdge: string } {
        let maxEdgeNoise = -1;
        let bridgeX = 0;
        let bridgeY = 0;
        let bridgeEdge = "none"; // "left", "right", "top", "bottom"

        if (!settings.isIsland) {
            // top-bottom scan
            for (let x = 0; x < width; x++) {
                const { sampleX: tx, sampleY: ty } = MapGenerator.#getDomainWarpedSample(x, 0, mOffsetX, mOffsetY, settings, noise);
                const nTop = MapGenerator.#getStandardfBm(tx, ty, 4, noise);
                if (nTop > maxEdgeNoise) { maxEdgeNoise = nTop; bridgeX = x; bridgeY = 0; bridgeEdge = "top"; }

                const { sampleX: bx, sampleY: by } = MapGenerator.#getDomainWarpedSample(x, height - 1, mOffsetX, mOffsetY, settings, noise);
                const nBottom = MapGenerator.#getStandardfBm(bx, by, 4, noise);
                if (nBottom > maxEdgeNoise) { maxEdgeNoise = nBottom; bridgeX = x; bridgeY = height - 1; bridgeEdge = "bottom"; }
            }

            // left-right scan
            for (let y = 0; y < height; y++) {
                const { sampleX: lx, sampleY: ly } = MapGenerator.#getDomainWarpedSample(0, y, mOffsetX, mOffsetY, settings, noise);
                const nLeft = MapGenerator.#getStandardfBm(lx, ly, 4, noise);
                if (nLeft > maxEdgeNoise) { maxEdgeNoise = nLeft; bridgeX = 0; bridgeY = y; bridgeEdge = "left"; }

                const { sampleX: rx, sampleY: ry } = MapGenerator.#getDomainWarpedSample(width - 1, y, mOffsetX, mOffsetY, settings, noise);
                const nRight = MapGenerator.#getStandardfBm(rx, ry, 4, noise);
                if (nRight > maxEdgeNoise) { maxEdgeNoise = nRight; bridgeX = width - 1; bridgeY = y; bridgeEdge = "right"; }
            }
        }

        return { bridgeX, bridgeY, bridgeEdge }
    }

    static _determineTileRegion(
        x: number, y: number, 
        width: number, height: number, 
        grid: Grid, tile: Tile, 
        settings: MapSettings
    ): void {
        const { slope, cardinalDir } = MapGenerator.findNearestSlopeAspect(x, y, width, height, grid)
        // Tectonic Rule: Only allow cliffs to form if they face West, North-West, or South-West
        // This simulates a mountain range being shoved from the East!
        const tectonicallyShoved = ["W", "NW", "SW"].includes(cardinalDir)

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
