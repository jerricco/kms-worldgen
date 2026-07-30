import { SeededRandom } from './seed'
import { OpenSimplexNoise } from './noise'
import type { GameSettings } from '../../game/GameScene';
import type { ChunkSettings } from './chunk';
import type { BasementRockType, SubterraneanLayer } from "./geology";

// governs configuration that is semantically relevant to the entire world generation
export interface GlobalGenerationMeta {
    // configurables
    worldWidth: number,                 // 50 -> 12800 as an expression of Tiles. Defines the max width of the whole world generation
    worldHeight: number,                // 50 -> 12800 as an expression of Tiles. Defines the max height of the whole world generation
    cellGridSize: number,               // (DEFAULT: 400) Size of a voronoi cell region to contain a single landmass node.
    bufferFactor: number,               // buffer factor that is used to cleanly calculate the bufferX/bufferY values uniformly
    oceanClamp: number,                 // 0.0 -> 1.0 as a percentage of the map which it should try to force the entire above-ground generation inside of
    
    // back-calculated (private)
    cosA: number;                       // cosine factor with which to rotate the original coordinates around for generation.
    sinA: number;                       // sine factor with which to rotate the original coordinates around for generation.
    bufferX: number,                    // 0.0 -> 1.0 as a multiplier of the world width (eg: worldWidth * bufferFactor).
    bufferY: number,                    // 0.0 -> 1.0 as a multiplier of the world height (eg: worldHeight * bufferFactor).
    randomAngle: number,                // Completely random angle anywhere in the range of 0->2π radians.
    
    // unknown
    stretchX: number,
    stretchY: number,
    mOffsetX: number;
    mOffsetY: number;
}

export interface TileComposition {
    elevation: number,
    geology: SubterraneanLayer,
}

export class MapGenerator {
    static DEFAULT_SEED = 'aborio rice';

    // rng objects
    seed: string;
    rng: SeededRandom;
    noise: OpenSimplexNoise;
    
    // player & engine configuration
    settings: ChunkSettings; // chunk-centered settings
    meta: GlobalGenerationMeta; // metadata for the global generation

    // helper properties
    get halfW() {
        return (this.settings.worldWidth || 0) / 2
    }

    get halfH() {
        return (this.settings.worldWidth || 0) / 2
    }

    constructor(config: GameSettings) {
        this.seed = config.seed; // expose seed so we can easily see the current one being operated on.
        this.rng = new SeededRandom(config.seed);
        this.noise = new OpenSimplexNoise(this.rng);

        this.settings = { ...config }; // @TODO: more robustly pull the right properties for ChunkSettings
        this.meta = this.generateGlobalMetadata()
    }

    // @TODO: adjust it so that the world size is by default 256*256 chunks (12800*12800)
    generateGlobalMetadata(): GlobalGenerationMeta {
        const randomAngle = this.rng.nextRange(0, Math.PI * 2);
        // @NOTE this is where the GenerationSettings should overwrite & split between this & ChunkSettings 
        const worldWidth = this.settings.worldWidth || 750;
        const worldHeight = this.settings.worldHeight || 750;
        const bufferFactor = 0.05;
        const oceanClamp = 0.95;

        return {
            worldWidth,
            worldHeight,
            cellGridSize: 600, // @TEST
            bufferFactor,
            oceanClamp,
            randomAngle,

            bufferX: worldWidth * bufferFactor,
            bufferY: worldHeight * bufferFactor,
            stretchX: 0.7,
            stretchY: 1.3,
            mOffsetX: this.rng.nextRange(10000, 90000),
            mOffsetY: this.rng.nextRange(10000, 90000),
            cosA: Math.cos(randomAngle),
            sinA: Math.sin(randomAngle),
        }
    }

    generateSuperstructureElevation(globalX: number, globalY: number): number {
        // In dense land zones, cells shrink to 250 tiles for tight clustering.
        // In ocean zones, cells expand up to 600 tiles to prevent fragmenting.
        const cellSize = this.meta.cellGridSize;

        // rotate the continent spine
        const rotX = globalX * this.meta.cosA - globalY * this.meta.sinA;
        const rotY = globalX * this.meta.sinA + globalY * this.meta.cosA;
        // grid coord translation to 0,0 to avoid negative coordinate space
        const shiftedGridX = rotX + this.halfW
        const shiftedGridY = rotY + this.halfH;
        const rawCellX = Math.floor(shiftedGridX / cellSize);
        const rawCellY = Math.floor(shiftedGridY / cellSize);

        let totalLandWeight = 0.0;
        const searchRadius = 2; // for sampling surrounding cells

        const macroFreq = 1.0 /this.meta.worldWidth;
        const detailFreq = 3.5 /this.meta.worldWidth;
        const seedOffsetX = this.meta.mOffsetX || 0;
        const seedOffsetY = this.meta.mOffsetY || 0;

        // find nearby tectonic nodes
        for (let cx = -searchRadius; cx <= searchRadius; cx++) {
            for (let cy = -searchRadius; cy <= searchRadius; cy++) {
                const targetCellX = rawCellX + cx;
                const targetCellY = rawCellY + cy;

                // Calculate the bounding area of this neighbor cell
                // must be retranslated from the shifted coordinates
                const cellMinX = (targetCellX * cellSize) - this.halfW;
                const cellMinY = (targetCellY * cellSize) - this.halfH;

                const cellCenterX = cellMinX + cellSize / 2;
                const cellCenterY = cellMinY + cellSize / 2;

                const sampleX = cellCenterX + seedOffsetX;
                const sampleY = cellCenterY + seedOffsetY;

                const macroNoise = (this.noise.noise2D(sampleX * macroFreq, sampleY * macroFreq) + 1.0) * 0.5;

                // Pass B: High-frequency erosion wave to fracture uniform edges
                const detailNoise = (this.noise.noise2D((sampleX + 5000) * detailFreq, (sampleY + 5000) * detailFreq) + 1.0) * 0.5;

                // Combine passes to determine the final tectonic density profile
                const cellDensity = Math.max(0.0, Math.min(1.0, (macroNoise * 0.7) + (detailNoise * 0.3)));




                
                // Define how many sub-nodes spawn in this cell based on tectonic density
                let subNodeCount = 1;
                if (cellDensity > 0.65) subNodeCount = 3;      // High density = Core mainland cluster
                else if (cellDensity > 0.40) subNodeCount = 2; // Mid density = Connecting land bridges
                else if (cellDensity < 0.20) continue;         // Deep ocean basin = Deactivate cell completely

                // Generate deterministic sub-nodes inside the locked cell frame
                for (let sub = 0; sub < subNodeCount; sub++) {
                    // @TODO: use rng here
                    const seedX = Math.sin(targetCellX * 12.9898 + targetCellY * 78.233 + sub * 45.12) * 43758.5453;
                    const seedY = Math.sin(targetCellX * 39.3464 + targetCellY * 11.135 + sub * 87.93) * 76351.9814;
                    
                    const offsetX = seedX - Math.floor(seedX);
                    const offsetY = seedY - Math.floor(seedY);
                    const nodeGlobalX = cellMinX + (offsetX * cellSize);
                    const nodeGlobalY = cellMinY + (offsetY * cellSize);

                    // Calculate the distance from our current tile to this neighbor cell node
                    const dx = globalX - nodeGlobalX;
                    const dy = globalY - nodeGlobalY;
                    const distToNode = Math.sqrt(dx * dx + dy * dy);

                    // Nodes in dense zones grow larger to blend into a unified continent mass,
                    // while isolated nodes shrink into small barrier islands.
                    const baseRadius = cellSize * (0.55 + offsetX * 0.45);
                    const nodeRadius = baseRadius * (0.35 + cellDensity * 0.85);
                    
                    if (distToNode < nodeRadius) {
                        const linearT = 1.0 - (distToNode / nodeRadius);
                        const smoothWeight = linearT * linearT * (3.0 - 2.0 * linearT); // Smoothstep
                        totalLandWeight += smoothWeight;
                    }
                }
            }
        }

        // clamp our fused landmass base structure cleanly between 0.0 and 1.0
        let macroSuperstructure = Math.min(1.0, totalLandWeight);

        // global ocean mask
        const trueDistanceToCenter = Math.sqrt(globalX * globalX + globalY * globalY);
        const maxAllowedRadius = this.halfW * this.meta.oceanClamp;
        const edgeT = Math.max(0.0, Math.min(1.0, trueDistanceToCenter / maxAllowedRadius));
        const globalOceanMask = 1.0 - Math.pow(edgeT, 3.0);

        // Merge our organic multi-node landmass layout with the map boundary protection
        let continentMask = Math.sqrt(macroSuperstructure * globalOceanMask);

        let elevation = continentMask;
        if (elevation < 0.6) {
            const shelfT = elevation / 0.6;
            elevation = (shelfT * shelfT * (3.0 - 2.0 * shelfT)) * 0.6;
        }

        return Math.max(0, Math.min(1.0, elevation));
    }

    generateFBMElevation(sampleX: number, sampleY: number, elevation: number) {
        // Create two noise landscapes using brownian-motion analysis
        // baseLand -> a flatter noise landscape to define the gentle rise of plains upward to mountainous biomes
        // mountainSpines -> generates a sharp ridge-peaked noise which can be used to define mountain ranges.
        const baseLand = this.getStandardfBm(sampleX, sampleY, 4);
        const mountainSpines = this.getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6);

        // blend the two noises together, ensuring that each is proprotionally scaled to 
        // take a proportion of the landscape's elevation values.
        let spineBlendMask = (baseLand * 0.3) + (mountainSpines * 0.85);
        if (spineBlendMask > this.settings.seaLevel) {
            const relativeHeight = spineBlendMask - this.settings.seaLevel;
            spineBlendMask = this.settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4)
        }

        const terrainDetail = spineBlendMask * elevation;
        const plateauBase = elevation * 0.35;

        elevation = Math.max(plateauBase, terrainDetail);

        return Math.max(0.0, Math.min(1.0, elevation));
    }

    generateTileComposition(globalX: number, globalY: number): TileComposition {
        //////////////////////////////
        // ARGUMENTATION VALIDATION //
        //////////////////////////////
        // clamp coords inside world border
        globalX = Math.max(-this.halfW, Math.min(this.halfW, globalX));
        globalY = Math.max(-this.halfH, Math.min(this.halfH, globalY));

        // @TODO: validate meta & transform it so that configuration for calculated meta
        // is easier for the player to handle.

        //////////////////////////
        // value initialization //
        //////////////////////////
        // use random offsets provided in meta (offsetX/offsetY), to scramble the noise provided.
        const { sampleX, sampleY } = this.getDomainWarpedSample(globalX, globalY);
        
        ////////////////////////
        // generate elevation //
        ////////////////////////
        // 1. create superstructure Voroni cells to push basic topography into.
        // coordinate warping for smooth edges - this removes any sharp artifacts from the
        // rigid (globalX, globalY) coordiantes.
        // @TEMP
        const artifactFreq = 1.0 / this.meta.worldWidth;
        const artifactAmp = 45.0
        const smoothCellX = globalX + this.noise.noise2D(globalX * artifactFreq, globalY * artifactFreq) * artifactAmp;
        const smoothCellY = globalY + this.noise.noise2D((globalX + 1000) * artifactFreq, (globalY + 1000) * artifactFreq) * artifactAmp;
        let elevation = this.generateSuperstructureElevation(smoothCellX, smoothCellY);
        
        // 2. mess about with brownian noise to create coastal
        elevation = this.generateFBMElevation(sampleX, sampleY, elevation);

        // clamp elevation to max (@TODO: I might make this a larger proportion)
        elevation = Math.max(0, Math.min(1.0, elevation))

        //////////////////////////
        //   generate geology   //
        //////////////////////////
        // Generates internal geological metadata to drive mechanics and visuals
        const geoFreq = 5.0 / this.meta.worldWidth;
        const geoNoise = (this.noise.noise2D(globalX * geoFreq, globalY * geoFreq) + 1.0) * 0.5;

        let primaryRock: BasementRockType = 'sedimentary';
        if (elevation > this.settings.mountainLevel) {
            primaryRock = geoNoise > 0.4 ? 'granite' : 'basalt'; // Igneous basement rock cores
        } else {
            primaryRock = geoNoise > 0.5 ? 'limestone' : 'sandstone'; // Basins and shelves
        }

        const geology: SubterraneanLayer = {
            bedrockDepth: Math.floor((1.0 - elevation) * 120 + 20),
            sedimentaryThickness: elevation > this.settings.mountainLevel ? Math.floor(geoNoise * 10) : Math.floor(geoNoise * 80 + 20),
            primaryRockType: primaryRock
        };

        return { elevation, geology }
    }

    ///////////////////////////////
    //     UTILITY FUNCTIONS     //
    ///////////////////////////////
    // NOISE GENERATION
    getDomainWarpedSample(x: number, y: number): { sampleX: number, sampleY: number } {
        const warpX = this.noise.noise2D((x + 200) * 0.018, (y + 200) * 0.018) * 45;
        const warpY = this.noise.noise2D((x - 200) * 0.018, (y - 200) * 0.018) * 45;

        const sampleX = (x + this.meta.mOffsetX + warpX) * this.settings.macroScale;
        const sampleY = (y + this.meta.mOffsetY + warpY) * this.settings.macroScale;

        return { sampleX, sampleY };
    }

    // Fractional Brownian Motion for ridged multi-fractal noise structures
    // This is scienceish for forked mountain range structures.
    getRidgedfBm(nx: number, ny: number, octaves: number): number {
        let value = 0;
        let amplitude = 1.0;
        let frequency = 1.0;
        let maxValue = 0;

        for (let i = 0; i < octaves; i++) {
            // Signal inversion maps absolute valleys into sharp ridged spines
            let signal = this.noise.noise2D(nx * frequency, ny * frequency);
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
    getStandardfBm(nx: number, ny: number, octaves: number): number {
        let value = 0;
        let amplitude = 1.0;
        let frequency = 1.0;
        let maxValue = 0;

        for (let i = 0; i < octaves; i++) {
            const signal = (this.noise.noise2D(nx * frequency, ny * frequency) + 1.0) / 2.0;
            value += signal * amplitude;
            maxValue += amplitude;
            frequency *= 2.0;
            amplitude *= 0.54;
        }
        return value / maxValue;
    }

    randomCellNodeOffset(cellX: number, cellY: number): { x: number, y: number } {
        // Sample noise at high integers to get stable, pseudorandom 0.0 to 1.0 offsets
        const nx = (this.noise.noise2D(cellX * 50.123, cellY * 34.567) + 1.0) * 0.5;
        const ny = (this.noise.noise2D(cellX * 23.456, cellY * 78.910) + 1.0) * 0.5;
        return { x: nx, y: ny };
    }
}
