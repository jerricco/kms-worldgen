import { SeededRandom } from './seed'
import { OpenSimplexNoise } from './noise'
import type { GameSettings } from '../../game/GameScene';
import type { ChunkSettings } from './chunk';
import type { BasementRockType, SubterraneanLayer } from "./geology";
import { VoronoiCluster, type VoronoiClusterConfig, type VoronoiSite } from './VoronoiCluster';

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
    gd: pc.GraphicsDevice;
    
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

    // generation artifacts
    voronoiCluster!: VoronoiCluster;

    constructor(config: GameSettings, gd: pc.GraphicsDevice) {
        this.seed = config.seed; // expose seed so we can easily see the current one being operated on.
        this.rng = new SeededRandom(config.seed);
        this.noise = new OpenSimplexNoise(this.rng);
        this.gd = gd;

        this.settings = { ...config }; // @TODO: more robustly pull the right properties for ChunkSettings
        this.meta = this.generateGlobalMetadata()
    }

    generateVoronoiStructure() {
        const config: VoronoiClusterConfig = {
            width: this.settings.worldWidth,
            height: this.settings.worldHeight,
            oceanClamp: this.meta.oceanClamp,
            rng: this.rng
        };

        this.voronoiCluster = new VoronoiCluster(config, this.meta, this.noise, this.gd);
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
            cellGridSize: 400, // @TEST
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
        // rotate the continent spine
        const rotX = globalX * this.meta.cosA - globalY * this.meta.sinA;
        const rotY = globalX * this.meta.sinA + globalY * this.meta.cosA;

        let totalLandWeight = 0.0;

        const maxRadiusLookahead = this.voronoiCluster.spatialGrid.cellGridSize;
        const candidateSites = this.voronoiCluster.spatialGrid.getNearby(rotX, rotY, maxRadiusLookahead);
        
        for (let i = 0; i < candidateSites.length; i++) {
            const site = candidateSites[i];

            // 3. Distance check to the true site point
            const dx = rotX - site.position.x;
            const dy = rotY - site.position.y;
            const distToNode = Math.sqrt(dx * dx + dy * dy);

            // Blending profiles from your original approach
            if (distToNode < site.nodeRadius) {
                const linearT = 1.0 - (distToNode / site.nodeRadius);
                const smoothWeight = linearT * linearT * (3.0 - 2.0 * linearT); // Smoothstep
                totalLandWeight += smoothWeight;
            }
        }

        // Clamp the fused landmass base structure cleanly between 0.0 and 1.0
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
        // 1. create superstructure Voronoi cells to push basic topography into.
        // coordinate warping for smooth edges - this removes any sharp artifacts from the
        // rigid (globalX, globalY) coordiantes.
        // @TEMP
        const artifactFreq = 1.0 / this.meta.worldWidth;
        const artifactAmp = 45.0
        const smoothCellX = globalX + this.noise.noise2D(globalX * artifactFreq, globalY * artifactFreq) * artifactAmp;
        const smoothCellY = globalY + this.noise.noise2D((globalX + 1000) * artifactFreq, (globalY + 1000) * artifactFreq) * artifactAmp;
        let elevation = this.generateSuperstructureElevation(smoothCellX, smoothCellY);
        
        // 2. mess about with brownian noise to create coastal
        // elevation = this.generateFBMElevation(sampleX, sampleY, elevation);

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
