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

    constructor(settings: ChunkSettings, meta: GlobalGenerationMeta) {
        this.settings = settings; // @TODO: fill in defaults?
        this.meta = meta
    }

    // @TODO: adjust it so that the world size is by default 256*256 chunks (12800*12800)
    static generateGlobalMetadata(config: GameSettings, rng: SeededRandom): GlobalGenerationMeta {
        if (!rng) {
            throw new Error('generateGlobalMetadata: We need an rng instance to generate randomness!')
        }

        const randomAngle = rng.nextRange(0, Math.PI * 2);
        // @NOTE this is where the GenerationSettings should overwrite & split between this & ChunkSettings 
        const worldWidth = config.worldWidth || 750;
        const worldHeight = config.worldHeight || 750;
        const bufferFactor = 0.05;
        const oceanClamp = 0.85;

        return {
            worldWidth,
            worldHeight,
            bufferFactor,
            oceanClamp,
            randomAngle,

            bufferX: worldWidth * bufferFactor,
            bufferY: worldHeight * bufferFactor,
            stretchX: 0.7,
            stretchY: 1.3,
            mOffsetX: rng.nextRange(10000, 90000),
            mOffsetY: rng.nextRange(10000, 90000),
            cosA: Math.cos(randomAngle),
            sinA: Math.sin(randomAngle),
        }
    }

    getGlobalTileComposition(
        globalX: number, globalY: number,
        settings: ChunkSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise,
    ): TileComposition {
        //////////////////////////////
        // ARGUMENTATION VALIDATION //
        //////////////////////////////
        // clamp coords inside world border
        const halfX = meta.worldWidth / 2, halfY = meta.worldHeight / 2;
        globalX = Math.max(-this.halfW, Math.min(halfX, globalX));
        globalY = Math.max(-halfY, Math.min(halfY, globalY));

        // @TODO: validate meta & transform it so that configuration for calculated meta
        // is easier for the player to handle.

        ///////////////// 
        // INIT VALUES //
        /////////////////
        // Sample a warped perlin landscape.
        // Uses random offsets provided in meta (offsetX, offsetY), which are random large numbers to scramble
        // the noise provided. There is also a configurable macroScale settings item which will
        // define the overall macro-variance of the generated landscape.
        const { sampleX, sampleY } = this.getDomainWarpedSample(globalX, globalY, meta, settings, noise);

        // proximity to edge of map
        const distToLeft = -halfX + globalX;
        const distToRight = halfX - globalX;
        const distToTop = -halfY + globalY;
        const distToBottom = halfY - globalY;

        // express edge proximity as how far the current tile is inside the defined bufferzone around the edge of the grid.
        // this ensures there is always a falloff to 0 elevation as the noise approaches the edge of the max grid.
        const edgeXFactor = Math.min(1.0, Math.min(distToLeft, distToRight) / meta.bufferX);
        const edgeYFactor = Math.min(1.0, Math.min(distToTop, distToBottom) / meta.bufferY);
        const globalEdgeFactor = edgeXFactor * edgeYFactor; // 0.0 (world edge) -> 1.0 (buffer inner edge) of how far into the buffer zone I am.

        // configure warping strength, multiplied by the edge factor so that
        // warping drops to 0 inside the outer buffer zone.
        const maskWarpStrength = 0.25 * globalEdgeFactor; 
        // Using the previously sampled x,y, create eliptical noise in each x,y direction
        // multiplied by the warp strength. Note that Y values are buffered by +50 to skew
        // the landscape elevation along one axis more than another.
        const maskWarpX = noise.noise2D(sampleX * 0.4, sampleY * 0.4) * maskWarpStrength;
        const maskWarpY = noise.noise2D(sampleX * 0.4 + 50, sampleY * 0.4 + 50) * maskWarpStrength;

        // simulate tectonics with Voronoi noise. This warps and shifts 0,0 itself
        // so that the landscape shears in an approximation of plate tectonics.
        const tectonicFreq = 0.5 / meta.worldWidth // very low frequencey for a large scale noise landscape
        // generate tectonic noise, multiplied by a small proportion so the voronoi cells are large (15% of the map size)
        const tectonicShiftX = noise.noise2D(globalX * tectonicFreq, globalY * tectonicFreq) * (meta.worldWidth * 0.15)
        const tectonicShiftY = noise.noise2D((globalX + 2000) * tectonicFreq, (globalY + 2000) * tectonicFreq) * (meta.worldHeight * 0.15);

        // rotation factor
        // create a new set of x,y coordinates, rotated by sinA,cosA.
        // these values are given a randomAngle at configuration time.
        // this should spin the x,y skewed landmas in a random direction.
        // here we supply our tectonic coordinates so that the rotation respects the
        // tectonic voroni cells.
        const rx = (globalX + tectonicShiftX) * meta.cosA - (globalY + tectonicShiftY) * meta.sinA;
        const ry = (globalX + tectonicShiftX) * meta.sinA + (globalY + tectonicShiftY) * meta.cosA;

        // multiplying by stretchX/Y here will force the circularness of the 
        // resulting landscape into more of an ovaloid shape.
        // giving it the rotation and warped masked coordinates will
        // break up the ovaloid into jagged coast-like edges 
        // (via distortion of the noise in general).
        const distanceToEdgeMask = Math.sqrt(
            Math.pow((rx + maskWarpX) * meta.stretchX, 2) +
            Math.pow((ry + maskWarpY * meta.stretchY) * meta.stretchY, 2)
        );

        // directional shelf variation
        // this should break up the low frequency tectonic noise
        const angleFromCenter = Math.atan2(ry + maskWarpY, rx + maskWarpX);
        const shelfVariation = noise.noise2D(Math.cos(angleFromCenter) * 1.2, Math.sin(angleFromCenter) * 1.2);
        const dynamicRadiusModifier = 1.0 + (shelfVariation * 0.45);

        const normalisedDistance = distanceToEdgeMask / (halfX * dynamicRadiusModifier);
        const sizeModifier = 1.0 / settings.islandRadius // player config
        const maskStrength = normalisedDistance * settings.squishFactor * sizeModifier;

        // stepped edge drop-off curve
        let elevation = Math.max(0, 1.0 - Math.pow(maskStrength, 2.5))
        if (elevation < 0.6) {
            const shelfT = elevation / 0.6;
            elevation = (shelfT * shelfT * (3.0 - 2.0 * shelfT)) * 0.6
        }


        // Define the maximum radius of the map, constrained by the oceanClamp percentage
        // Then normalise it as a percentage of the distance to the edge factor defined above.
        // const maxWorldRadius = Math.sqrt(halfX * halfX) * meta.oceanClamp;
        // const normalisedDistance = distanceToEdgeMask / maxWorldRadius;

        // @DEPRECATED: replace islandRadius with continentSize; - move to meta
        // @DEPRECATED: replace squishFactor with baseLandscapeSquishFactor; - move to meta
        // Define the overall mask strength, which directly maps to an elevation value.
        // This is multiplied by a "squishFactor" whcih can dampen elevations that sharply rise at this stage.
        // This ensures the generation is smooth & flattened in a way we can pinch mountains out of later.
        // We lastly constrain it by the islandRadius (@TODO: continentSizeFactor) so that we can naturally grow
        // and shrink the generation by proportion as needed.
        // Because the normalised distances/squish factor are inverted from the islandRadius setting, flip it first.
        // const maskStrength = normalisedDistance * settings.squishFactor * (1.0 / settings.islandRadius);
        
        // Evaluate elevation of the tile using a cubic exponential curve.
        // this ensures that the generated will plateau toward the centre 
        // with a cubically rising curve strongly. This stops the generation from
        // pinching into a smaller point as it approaches the center.
        // let elevation = Math.max(0, 1.0 - Math.pow(maskStrength, 3.0));

        ////////////////////////////////////////////////////////////
        // @TODO: dome mountains - this implementation doesnt work
        // Isolated Dome Mountains / Batholiths (Billow Noise)
        // const domeFreq = 7.0 / meta.worldWidth;
        // const mountainDomes = MapGenerator.getBillowfBm(sampleX * 1.3, sampleY * 1.3, domeFreq, 6, noise);
        // // Isolate domes into clusters using a low frequency distribution mask
        // const distNoise = noise.noise2D(globalX * (3.0 / meta.worldWidth), globalY * (3.0 / meta.worldWidth));
        // const domeDistribution = Math.max(0, distNoise);
        // const domeMountains = Math.pow(mountainDomes, 2.0) * domeDistribution * 1.5;
        // elevation += (domeMountains * 0.35);  // @NOTE: uncomment to include
        ////////////////////////////////////////////////////////////
        
        // Create two noise landscapes using brownian-motion analysis
        // baseLand -> a flatter noise landscape to define the gentle rise of plains upward to mountainous biomes
        // mountainSpines -> generates a sharp ridge-peaked noise which can be used to define mountain ranges.
        const baseLand = MapGenerator.getStandardfBm(sampleX, sampleY, 4, noise);
        const mountainSpines = MapGenerator.getRidgedfBm(sampleX * 1.3, sampleY * 1.3, 6, noise);

        // blend the two noises together, ensuring that each is proprotionally scaled to 
        // take a proportion of the landscape's elevation values.
        let spineBlendMask = (baseLand * 0.3) + (mountainSpines * 0.85);
        if (spineBlendMask > settings.seaLevel) {
            const relativeHeight = spineBlendMask - settings.seaLevel;
            spineBlendMask = settings.seaLevel + Math.pow(relativeHeight * 1.65, 1.4)
        }

        // blend the current elevation with the brownian motion masks.
        // elevation *= spineBlendMask

        // clamp elevation to max (@TODO: I might make this a larger proportion)
        elevation = Math.max(0, Math.min(1.0, elevation))

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
    // NOISE GENERATION
    static getDomainWarpedSample(
        x: number,
        y: number,
        meta: GlobalGenerationMeta,
        settings: ChunkSettings,
        noise: OpenSimplexNoise,
    ): { sampleX: number, sampleY: number } {
        const warpX = noise.noise2D((x + 200) * 0.018, (y + 200) * 0.018) * 45;
        const warpY = noise.noise2D((x - 200) * 0.018, (y - 200) * 0.018) * 45;

        const sampleX = (x + meta.mOffsetX + warpX) * settings.macroScale;
        const sampleY = (y + meta.mOffsetY + warpY) * settings.macroScale;

        return { sampleX, sampleY };
    }

    // Fractional Brownian Motion for ridged multi-fractal noise structures
    // This is scienceish for forked mountain range structures.
    static getRidgedfBm(nx: number, ny: number, octaves: number, noise: OpenSimplexNoise): number {
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
    static getStandardfBm(nx: number, ny: number, octaves: number, noise: OpenSimplexNoise): number {
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

    static getBillowfBm(
        x: number, y: number,
        octaves: number, baseFreq: number,
        noise: OpenSimplexNoise
    ) {
        let value = 0;
        let amplitude = 1.0;
        let frequency = baseFreq;
        let maxValue = 0;
        for (let i = 0; i < octaves; i++) {
            // Absolute value creates rolling, billowy dome structures
            const n = Math.abs(noise.noise2D(x * frequency, y * frequency));
            value += n * amplitude;
            maxValue += amplitude;
            amplitude *= 0.45;
            frequency *= 2.0;
        }
        return value / maxValue;
    }
    
    // IMPROVEMENTS
    // DECOUPLED FEATURE MASKING
    // DYNAMIC THRESHOLDING
}
