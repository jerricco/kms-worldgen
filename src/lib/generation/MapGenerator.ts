import * as pc from 'playcanvas'
import { SeededRandom } from './seed'
import { OpenSimplexNoise } from './noise'
import type { BasementRockType, SubterraneanLayer } from "./geology";
import { VoronoiFactory, type VoronoiSite } from './VoronoiCluster';

export type GenerationSettings = {
    seed: string;
    // world settings
    worldWidth: number,                 // 50 -> 12800 as an expression of Tiles. Defines the max width of the whole world generation
    worldHeight: number,                // 50 -> 12800 as an expression of Tiles. Defines the max height of the whole world generation
    cellGridSize: number,               // (DEFAULT: 400) Size of a voronoi cell region to contain a single landmass node.
    oceanClamp: number,                 // 0.0 -> 1.0 as a percentage of the map which it should try to force the entire above-ground generation inside of
    
    // randomisation settings
    macroScale: number,
    squishFactor: number,
    stretchX: number,
    stretchY: number,

    // chunk settings
    chunkSize: number,

    // elevation settings
    abyssalLevel: number,
    trenchLevel: number,
    deepOceanLevel: number,
    oceanLevel: number,
    seaLevel: number,
    beachLevel: number,
    plainLevel: number,
    hillLevel: number,
    mountainLevel: number,
    peakLevel: number,
}

export interface GenerationMeta {
    // back-calculated randomisation
    cosA: number;                       // cosine factor with which to rotate the original coordinates around for generation.
    sinA: number;                       // sine factor with which to rotate the original coordinates around for generation.
    randomAngle: number,                // Completely random angle anywhere in the range of 0->2π radians.
    mOffsetX: number;
    mOffsetY: number;
}

export interface TileComposition {
    elevation: number,
    geology: SubterraneanLayer,
}

export class MapGenerator {
    static DEFAULT_SEED = 'aborio rice';

    // config
    seed: string;
    settings: GenerationSettings;
    meta: GenerationMeta;
    
    // rng objects
    rng: SeededRandom;
    noise: OpenSimplexNoise;
    gd: pc.GraphicsDevice;
    
    // helper classes
    voronoi: VoronoiFactory;

    // helper properties
    get halfW() {
        return (this.settings.worldWidth || 0) / 2
    }

    get halfH() {
        return (this.settings.worldWidth || 0) / 2
    }

    constructor(config: GenerationSettings, gd: pc.GraphicsDevice) {
        this.seed = config.seed; // expose seed so we can easily see the current one being operated on.
        this.settings = { ...config }; // @TODO: more robustly pull the right properties for ChunkSettings
        
        this.rng = new SeededRandom(config.seed);
        this.noise = new OpenSimplexNoise(this.rng);
        this.gd = gd;

        this.meta = this.#generateGlobalMetadata()
        this.voronoi = new VoronoiFactory(this)
    }

    // pregenerate pass must happen once per world generations
    pregenerate() {
        // generate voronoi cell structure
        this.voronoi.generate()
        // low-frequency noise pass for tectonic structure
        // high-frequency pass for coasts, mountains and land basins
    }
    
    // chunk creates a configured chunk of tiles
    chunk(globalX: number, globalY: number) {}
    
    // generate pass happens once-per tile
    generate(globalX: number, globalY: number): TileComposition {
        // clamp coords inside world border
        globalX = Math.max(-this.halfW, Math.min(this.halfW, globalX));
        globalY = Math.max(-this.halfH, Math.min(this.halfH, globalY));
        
        ///////////////
        // ELEVATION //
        ///////////////
        // current tectonic superstructure
        let elevation = this.#getTectonicSuperstructure(globalX, globalY);

        // mess about with brownian noise to create coastal shapes
        // elevation = this.#applyGeologicalDetail(globalX, globalY, elevation);

        // clamp elevation
        elevation = Math.max(-1.0, Math.min(1.0, elevation));

        ///////////////
        //  GEOLOGY  //
        ///////////////
        const geology = this.#getTileGeology(globalX, globalY, elevation)


        return { elevation, geology }
    }

    //////////////////////////
    // this.generate PASSES //
    //////////////////////////
    // @TODO: Fix some issues:
    // - Elevation is whacked out, and doesn't generate into the right spots despite a sensible config
    // - Large bays should fill with scattered archipelagoes some of the time. Should this be a later feature pass?
    // - Mountain chains are filament like and not at all looking like a topographical structure.
    // - The oceanic falloff is too circular and should more losely match the landmass shape (with bays and enclosed areas having a generally higher elevation)
    // - There are no mountian bulks
    // - Mountain range filaments just use additive elevation and do not try to simulate a subductive mountain range topology properly
    // - Areas of highland should be able to exist at various heights across the map.
    #getTectonicSuperstructure(globalX: number, globalY: number): number {
        const sLevel = this.settings.seaLevel ?? 0.0;
        const pLevel = this.settings.peakLevel ?? 0.95;

        const site0Id = this.voronoi.delaunay.find(globalX, globalY);
        const site0 = this.voronoi.sites[site0Id];
        if (!site0) return this.settings.abyssalLevel ?? -1.0;

        const neighborIndices = this.voronoi.delaunay.neighbors(site0Id);
        const candidates: { site: VoronoiSite, dist: number }[] = [];

        const dx0 = globalX - site0.position.x;
        const dy0 = globalY - site0.position.y;
        candidates.push({ site: site0, dist: Math.sqrt(dx0 * dx0 + dy0 * dy0) });

        for (const neighborId of neighborIndices) {
            const nSite = this.voronoi.sites[neighborId];
            if (!nSite) continue;
            const dx = globalX - nSite.position.x;
            const dy = globalY - nSite.position.y;
            candidates.push({ site: nSite, dist: Math.sqrt(dx * dx + dy * dy) });
        }

        candidates.sort((a, b) => a.dist - b.dist);
        if (candidates.length < 2) return site0.baseElevation;

        let totalWeight = 0;
        let baseInterpolatedElevation = 0;
        const plateWeights = new Map<number, number>();

        const w1 = 1.0 / Math.pow(Math.max(1.0, candidates[0].dist), 2.0);
        totalWeight += w1;
        baseInterpolatedElevation += candidates[0].site.baseElevation * w1;
        plateWeights.set(candidates[0].site.plateId, w1);

        const w2 = 1.0 / Math.pow(Math.max(1.0, candidates[1].dist), 2.0);
        totalWeight += w2;
        baseInterpolatedElevation += candidates[1].site.baseElevation * w2;
        plateWeights.set(candidates[1].site.plateId, (plateWeights.get(candidates[1].site.plateId) || 0) + w2);

        if (candidates.length >= 3) {
            const w3 = 1.0 / Math.pow(Math.max(1.0, candidates[2].dist), 2.0);
            totalWeight += w3;
            baseInterpolatedElevation += candidates[2].site.baseElevation * w3;
            plateWeights.set(candidates[2].site.plateId, (plateWeights.get(candidates[2].site.plateId) || 0) + w3);
        }

        let finalElevation = baseInterpolatedElevation / totalWeight;
        if (plateWeights.size > 1) {
            const sortedPlates = Array.from(plateWeights.entries()).sort((a, b) => b[1] - a[1]);
            const primaryInfluence = sortedPlates[0][1] / totalWeight;
            const secondaryInfluence = sortedPlates[1][1] / totalWeight;

            const boundaryFriction = Math.min(primaryInfluence, secondaryInfluence) * 2.0;
            if (boundaryFriction > 0.05 && !candidates[0].site.isOceanic && !candidates[1].site.isOceanic) {
                const boundaryShape = boundaryFriction * boundaryFriction * (3.0 - 2.0 * boundaryFriction);
                const baseMountainHeight = Math.max(candidates[0].site.baseElevation, candidates[1].site.baseElevation);

                // Aggressively elevate fault lines closer to your peakLevel (0.95) to give your detail engine a solid foundation
                const targetSpineHeight = pc.math.lerp(baseMountainHeight, pLevel - 0.02, boundaryShape * 0.70);
                finalElevation = Math.max(finalElevation, targetSpineHeight);
            }
        }

        if (!candidates[0].site.isOceanic) {
            const landCoreFactor = Math.max(0.0, candidates[0].site.baseElevation - sLevel);
            // Slightly boosted internal lift to help push large sectors out of flat plains
            finalElevation += landCoreFactor * 0.32;
        }

        return Math.max(-1.0, Math.min(1.0, finalElevation));
    }

    // @TODO: Re-do this to make better decisions with my life.
    #applyGeologicalDetail(globalX: number, globalY: number, baseElevation: number): number {
        const sLevel = this.settings.seaLevel ?? 0.0;
        const oLevel = this.settings.oceanLevel ?? -0.25;

        let finalElevation = baseElevation;

        // domain warping 
        const coastlineWarpIntensity = 35.0;
        const coastlineWarpScale = 0.025;

        const distanceToCoast = Math.abs(baseElevation - sLevel);
        if (distanceToCoast < 0.15) {
            const coastMask = (1.0 - (distanceToCoast / 0.15));
            const warpX = globalX + this.noise.noise2D(globalX * coastlineWarpScale, globalY * coastlineWarpScale) * coastlineWarpIntensity * coastMask;
            const warpY = globalY + this.noise.noise2D((globalX + 500) * coastlineWarpScale, (globalY + 500) * coastlineWarpScale) * coastlineWarpIntensity * coastMask;
            finalElevation = this.#getTectonicSuperstructure(warpX, warpY);
        }

        const isLand = finalElevation >= sLevel;

        // =========================================================================
        // LAND PROFILE PROCESSING (Passes 1 & 2)
        // =========================================================================
        if (isLand) {
            // --- PASS 1: JAGGED MOUNTAINS (Ridged Multi-Fractal FBM) ---
            const mScale1 = 0.022;
            const mScale2 = 0.055;

            // Generate crisp V-shaped valleys and razor ridges
            const n1 = 1.0 - Math.abs(this.noise.noise2D(globalX * mScale1, globalY * mScale1));
            const n2 = 1.0 - Math.abs(this.noise.noise2D(globalX * mScale2, globalY * mScale2));
            const ridgedFBM = (n1 * 0.70) + (n2 * n1 * 0.30);

            // High exponential sharpening (pow 3.0) forces mountains to form narrow alpine spines
            const sharpPeaks = Math.pow(ridgedFBM, 3.0);

            const pScale = 0.008;
            const plainBillow = Math.abs(this.noise.noise2D(globalX * pScale, globalY * pScale));
            // Foothill mask determines where plains smoothly roll into mountain ranges
            const mountainMask = Math.max(0.0, Math.min(1.0, (finalElevation - 0.45) / 0.25));
            const smoothFoothills = mountainMask * mountainMask * (3.0 - 2.0 * mountainMask);

            // Non-Linear Scaling. We use the mountain noise to scale the height exponentially 
            // to force the peaks to climb out of the hills and pierce custom peak thresholds (0.95).
            if (finalElevation > 0.50) {
                const highAltitudeGrowth = (finalElevation - 0.50) / 0.50;
                finalElevation += sharpPeaks * 0.45 * Math.pow(highAltitudeGrowth, 1.4);
            } else {
                // Keep plains wide, flat, and sedimentary
                finalElevation += plainBillow * 0.03 * (1.0 - smoothFoothills);
            }
        }

        // =========================================================================
        // MARINE DEPTH PROCESSING
        // =========================================================================
        else {
            const marineScale1 = 0.006;
            const marineScale2 = 0.018;

            const marineNoise1 = this.noise.noise2D(globalX * marineScale1, globalY * marineScale1);
            const marineNoise2 = this.noise.noise2D(globalX * marineScale2, globalY * marineScale2);
            const blendedMarineNoise = (marineNoise1 * 0.65) + (marineNoise2 * 0.35);

            // Accelerated Marine Drop-off
            // As base elevation moves toward the outer edges of the map, we multiply by an 
            // amplification curve to force the deep water to drop cleanly down to -1.0.
            const depthFactor = Math.abs(finalElevation - sLevel); // How far down are we from sea level

            // Deep water drops exponentially faster, pulling out deepOcean and abyssal levels
            const marineDropOffMultiplier = 1.0 + (depthFactor * 1.5);
            finalElevation += blendedMarineNoise * 0.22 * marineDropOffMultiplier;

            // Dynamic Continental Shelf Extension
            if (finalElevation > oLevel) {
                const shelfProximity = (finalElevation - oLevel) / (sLevel - oLevel);
                finalElevation = pc.math.lerp(finalElevation, sLevel - 0.06, shelfProximity * 0.50);
            }

            // Pass 4 Volcanic Archipelago Injection
            const archipelagoIntensity = 0.26;
            const archScale1 = 0.035;
            const archScale2 = 0.08;

            if (finalElevation > oLevel - 0.15) {
                const islandSpawnMask = (finalElevation - (oLevel - 0.15)) / (sLevel - (oLevel - 0.15));
                const islandNoise1 = this.noise.noise2D(globalX * archScale1, globalY * archScale1);
                const islandNoise2 = this.noise.noise2D(globalX * archScale2, globalY * archScale2);
                let combinedArchNoise = (islandNoise1 * 0.65) + (islandNoise2 * 0.35);
                combinedArchNoise = Math.pow(Math.max(0.0, combinedArchNoise), 3.0);

                finalElevation += combinedArchNoise * archipelagoIntensity * islandSpawnMask;
            }
        }

        return Math.max(-1.0, Math.min(1.0, finalElevation));
    }

    #getTileGeology(globalX: number, globalY: number, elevation: number): SubterraneanLayer {
        // Generates internal geological metadata to drive mechanics and visuals
        const geoFreq = 5.0 / this.settings.worldWidth;
        const geoNoise = (this.noise.noise2D(globalX * geoFreq, globalY * geoFreq) + 1.0) * 0.5;

        let primaryRock: BasementRockType = 'sedimentary';
        if (elevation > this.settings.mountainLevel) {
            primaryRock = geoNoise > 0.4 ? 'granite' : 'basalt'; // Igneous basement rock cores
        } else {
            primaryRock = geoNoise > 0.5 ? 'limestone' : 'sandstone'; // Basins and shelves
        }

        return {
            bedrockDepth: Math.max(-1.0, Math.min((1.0 - elevation) * 120 + 20)),
            sedimentaryThickness: elevation > this.settings.mountainLevel ? Math.floor(geoNoise * 10) : Math.floor(geoNoise * 80 + 20),
            primaryRockType: primaryRock
        };
    }

    ////////////////////////
    //  HELPER FUNCTIONS  //
    ////////////////////////
    #generateGlobalMetadata(): GenerationMeta {
        const randomAngle = this.rng.nextRange(0, Math.PI * 2);
        return {
            randomAngle,
            mOffsetX: this.rng.nextRange(10000, 90000),
            mOffsetY: this.rng.nextRange(10000, 90000),
            cosA: Math.cos(randomAngle),
            sinA: Math.sin(randomAngle),
        }
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
}
