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
        // generate cell data
        // a Cell represents a voroni cell to place tectonic zones.
        const cellSize = this.meta.cellGridSize;;

        // grid coord translation to 0,0 to avoid negative coordinate space
        const shiftedGridX = globalX + this.halfW
        const shiftedGridY = globalY + this.halfH;

        const rawCellX = Math.floor(shiftedGridX / cellSize);
        const rawCellY = Math.floor(shiftedGridY / cellSize);

        let totalLandWeight = 0.0;
        const searchRadius = 1; // for sampling surrounding cells (up to 9)

        // find nearby tectonic nodes
        for (let cx = -searchRadius; cx <= searchRadius; cx++) {
            for (let cy = -searchRadius; cy <= searchRadius; cy++) {
                const targetCellX = rawCellX + cx;
                const targetCellY = rawCellY + cy;

                // Calculate the bounding area of this neighbor cell
                // must be retranslated from the shifted coordinates
                const cellMinX = (targetCellX * cellSize) - this.halfW;
                const cellMinY = (targetCellY * cellSize) - this.halfH;

                // Fetch the unique node offset position for this cell
                const seedX = Math.sin(targetCellX * 12.9898 + targetCellY * 78.233) * 43758.5453;
                const seedY = Math.sin(targetCellX * 39.3464 + targetCellY * 11.135) * 76351.9814;
                const offsetX = seedX - Math.floor(seedX);
                const offsetY = seedY - Math.floor(seedY);

                // Establish the final, shifted global position of this cell's land node
                const nodeGlobalX = cellMinX + (offsetX * cellSize);
                const nodeGlobalY = cellMinY + (offsetY * cellSize);

                // Calculate the distance from our current tile to this neighbor cell node
                const dx = globalX - nodeGlobalX;
                const dy = globalY - nodeGlobalY;
                const distToNode = Math.sqrt(dx * dx + dy * dy);

                // Vary node sizes dynamically based on their cell seeds
                const nodeRadius = cellSize * (0.6 + offsetX * 0.7);

                // Smooth step falloff curve (1.0 at center, 0.0 at radius edge)
                if (distToNode < nodeRadius) {
                    const linearT = 1.0 - (distToNode / nodeRadius);
                    const smoothWeight = linearT * linearT * (3.0 - 2.0 * linearT);

                    totalLandWeight += smoothWeight;
                }
            }
        }

        // Clamp our fused landmass base structure cleanly between 0.0 and 1.0
        let macroSuperstructure = Math.min(1.0, totalLandWeight);
        const trueDistanceToCenter = Math.sqrt(globalX * globalX + globalY * globalY);
        const maxAllowedRadius = this.halfW * this.meta.oceanClamp;

        const edgeT = Math.max(0.0, Math.min(1.0, trueDistanceToCenter / maxAllowedRadius));
        const globalOceanMask = 1.0 - Math.pow(edgeT, 3.0); // Cubic ocean falloff

        // Merge our organic multi-node landmass layout with the map boundary protection
        let continentMask = macroSuperstructure * globalOceanMask;

        let elevation = continentMask;
        if (elevation < 0.6) {
            const shelfT = elevation / 0.6;
            elevation = (shelfT * shelfT * (3.0 - 2.0 * shelfT)) * 0.6;
        }

        return Math.max(0, Math.min(1.0, elevation));
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

        let elevation = this.generateSuperstructureElevation(globalX, globalY);

        // ///////////////// 
        // // INIT VALUES //
        // /////////////////
        // // Sample a warped perlin landscape.
        // // Uses random offsets provided in meta (offsetX, offsetY), which are random large numbers to scramble
        // // the noise provided. There is also a configurable macroScale settings item which will
        // // define the overall macro-variance of the generated landscape.
        // const { sampleX, sampleY } = this.getDomainWarpedSample(globalX, globalY);

        // // proximity to edge of map
        // const distToLeft = -this.halfW + globalX;
        // const distToRight = this.halfW - globalX;
        // const distToTop = -this.halfH + globalY;
        // const distToBottom = this.halfH - globalY;

        // // express edge proximity as how far the current tile is inside the defined bufferzone around the edge of the grid.
        // // this ensures there is always a falloff to 0 elevation as the noise approaches the edge of the max grid.
        // const edgeXFactor = Math.min(1.0, Math.min(distToLeft, distToRight) / this.meta.bufferX);
        // const edgeYFactor = Math.min(1.0, Math.min(distToTop, distToBottom) / this.meta.bufferY);
        // const globalEdgeFactor = edgeXFactor * edgeYFactor; // 0.0 (world edge) -> 1.0 (buffer inner edge) of how far into the buffer zone I am.

        // // configure warping strength, multiplied by the edge factor so that
        // // warping drops to 0 inside the outer buffer zone.
        // const maskWarpStrength = 0.25 * globalEdgeFactor; 
        // // Using the previously sampled x,y, create eliptical noise in each x,y direction
        // // multiplied by the warp strength. Note that Y values are buffered by +50 to skew
        // // the landscape elevation along one axis more than another.
        // const maskWarpX = this.noise.noise2D(sampleX * 0.4, sampleY * 0.4) * maskWarpStrength;
        // const maskWarpY = this.noise.noise2D(sampleX * 0.4 + 50, sampleY * 0.4 + 50) * maskWarpStrength;

        // // simulate tectonics with Voronoi noise. This warps and shifts 0,0 itself
        // // so that the landscape shears in an approximation of plate tectonics.
        // const tectonicFreq = 0.5 / this.meta.worldWidth // very low frequencey for a large scale noise landscape
        // // generate tectonic noise, multiplied by a small proportion so the voronoi cells are large (15% of the map size)
        // const tectonicShiftX = this.noise.noise2D(globalX * tectonicFreq, globalY * tectonicFreq) * (this.meta.worldWidth * 0.15)
        // const tectonicShiftY = this.noise.noise2D((globalX + 2000) * tectonicFreq, (globalY + 2000) * tectonicFreq) * (this.meta.worldHeight * 0.15);

        // // rotation factor
        // // create a new set of x,y coordinates, rotated by sinA,cosA.
        // // these values are given a randomAngle at configuration time.
        // // this should spin the x,y skewed landmas in a random direction.
        // // here we supply our tectonic coordinates so that the rotation respects the
        // // tectonic voroni cells.
        // const rx = (globalX + tectonicShiftX) * this.meta.cosA - (globalY + tectonicShiftY) * this.meta.sinA;
        // const ry = (globalX + tectonicShiftX) * this.meta.sinA + (globalY + tectonicShiftY) * this.meta.cosA;

        // // multiplying by stretchX/Y here will force the circularness of the 
        // // resulting landscape into more of an ovaloid shape.
        // // giving it the rotation and warped masked coordinates will
        // // break up the ovaloid into jagged coast-like edges 
        // // (via distortion of the noise in general).
        // const distanceToEdgeMask = Math.sqrt(
        //     Math.pow((rx + maskWarpX) * this.meta.stretchX, 2) +
        //     Math.pow((ry + maskWarpY * this.meta.stretchY) * this.meta.stretchY, 2)
        // );

        // // directional shelf variation
        // // this should break up the low frequency tectonic noise
        // const angleFromCenter = Math.atan2(ry + maskWarpY, rx + maskWarpX);
        // const shelfVariation = this.noise.noise2D(Math.cos(angleFromCenter) * 1.2, Math.sin(angleFromCenter) * 1.2);
        // const dynamicRadiusModifier = 1.0 + (shelfVariation * 0.45);

        // const normalisedDistance = distanceToEdgeMask / (this.halfW * dynamicRadiusModifier);
        // const sizeModifier = 1.0 / this.settings.islandRadius // player config
        // const maskStrength = normalisedDistance * this.settings.squishFactor * sizeModifier;

        // // stepped edge drop-off curve
        // elevation = Math.max(0, 1.0 - Math.pow(maskStrength, 2.5))
        // if (elevation < 0.6) {
        //     const shelfT = elevation / 0.6;
        //     elevation = (shelfT * shelfT * (3.0 - 2.0 * shelfT)) * 0.6
        // }
        
        // // Create two noise landscapes using brownian-motion analysis
        // // baseLand -> a flatter noise landscape to define the gentle rise of plains upward to mountainous biomes
        // // mountainSpines -> generates a sharp ridge-peaked noise which can be used to define mountain ranges.
        // const baseLand = this.getStandardfBm(sampleX, sampleY, 4);
        // const mountainSpines = this.getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6);

        // // blend the two noises together, ensuring that each is proprotionally scaled to 
        // // take a proportion of the landscape's elevation values.
        // let spineBlendMask = (baseLand * 0.3) + (mountainSpines * 0.85);
        // if (spineBlendMask > this.settings.seaLevel) {
        //     const relativeHeight = spineBlendMask - this.settings.seaLevel;
        //     spineBlendMask = this.settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4)
        // }

        // blend the current elevation with the brownian motion masks.
        // elevation *= spineBlendMask

        // clamp elevation to max (@TODO: I might make this a larger proportion)
        // elevation = Math.max(0, Math.min(1.0, elevation))

        /////////////////////////////
        //   subterranean layers   //
        /////////////////////////////
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
