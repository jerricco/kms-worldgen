// @NOTES:
// Climatic factors influence biome generation. The climate is a cross-section of TEMPERATURE & PRECIPITATION with ALTITUDE modifyling local areas.
// Some rules:
// - LITHOSPHERE will contain all subterranean layers
// - SURFACE_CAVE are much more likely to spawn under TOWER_KARST and FAULT_MOUNTAIN biomes.
//    - Though they can occasionally spawn anywhere, leading to the LITHOSPHERE
// - KARST_GROUNDWATER_BASIN can only spawn inside a KARST
// - LAVA_TUBE is most likely to spawn underneath a SANDY_DESERT and always under a VOLCANIC_ISLAND
// - CALDERA will use the geological fold information to determine if it needs a LAVA_CHAMBER
// - SUBTERRANEAN_AQUIFER will spawn underneath VALLEY, PLAIN or KARST, which will later inform RIVER/FRESH_LAKE baselines
// - DEEP_BIOSPHERE is isolated from above generation and can only also contain CONTINENTAL_DEEP_CRUST, LAVA_CHAMBER, LAVA_TUBE & SUBTERRENE_CAVE
//    - The generation should bottom out at a layer of CONTINENTAL_DEEP_CRUST that stretches the whole map.
// -  Biomes from HYDROTHERMAL_SYSTEM will semantically link to structures on the surface lithograpy
//    - SEAFLOOR_FRACTURE can only form in ABYSSAL regions
//    - MAGMATIC_CHAMBER_VENT can form in DEEP_OCEAN, ABYSSAL or occasionally in the open ocean, forming an ATOL above it
//    - GEOTHERMAL_AQUIFER forms underneath where BARRIER_REEF form.
// - Any RIVER_DELTA will need to climatically form at the boundary between RIVER and SEA/OCEAN
// - CLIFFS need to create veritcal bounding zones the following regions where elevation changes significantly enough...
//    - MOUNTAIN (ESCARPMENT, GORGE)
//    - PLAIN (GORGE)
//    - SEA/OCEAN (SEA_CLIFF)
// - Form FOOTHILLS as buffer zones between sharply rising mountains and more generall flat lands.
export const BiomeID = {
    // Aquatic biomes
    // ABYSSAL
    HADAL_TRENCH       : 0, // subterrene
    ABYSSAL_PLAIN      : 1, // subterrene
    HYDROTHERMAL_VENT  : 2, // subterrene
    BATHYPELAGIC       : 3,
    // DEEP_OCEAN
    PELAGIC            : 4, 
    BENTHIC            : 5, // subterrene
    // OCEAN
    SUNLIT_OCEAN       : 6,
    CONTINENTAL_SHELF  : 7, // subterrene
    SHORELINE          : 8,
    // SEA
    MARGINAL_SEA       : 9,
    MEDITERRANEAN_SEA  : 10,
    BAY                : 11,
    GULF               : 12,
    // FRESH_LAKE
    CLEAR_LAKE         : 13,
    RICH_LAKE          : 14,
    BILLABONG          : 15,
    // SALINE_LAKE
    SALT_FLAT          : 16,
    ALKALINE_LAKE      : 17,
    HYPERSALINE_BASIN  : 18,
    // REEF
    FRINGE_REEF        : 19,
    BARRIER_REEF       : 20,
    ATOL               : 21,
    //BEACH
    SAND_BEACH         : 22,
    ROCK_BEACH         : 23,
    MUDFLAT            : 24,
    // CLIFF 
    SEA_CLIFF          : 25,
    ESCARPMENT         : 26,
    GORGE              : 27,
    // ISLAND 
    VOLCANIC_ISLAND    : 28,
    CORAL_CAY          : 29,
    ARCHIPELAGO        : 30,
    // WETLAND 
    SWAMP              : 31,
    MARSH              : 32,
    BOG                : 33,
    // ESTUARY 
    FJORD              : 34,
    SALT_MARSH_ESTUARY : 35,
    LAGOON             : 36,
    // RIVER 
    TORRENTIAL_RIVER   : 37,
    MEANDERING_RIVER   : 38,
    LOW_COURSE_RIVER   : 39,
    // RIVER DELTA 
    BIRD_FOOT_DELTA    : 40,
    WAVE_DELTA         : 41,
    TIDE_DELTA         : 42,
    // HILL
    ROLLING_HILL       : 43,
    MOUND              : 44,
    BUTTE              : 45,
    FOOTHILL           : 46,
    // MOUNTAIN
    FOLD_MOUNTAIN      : 47,
    FAULT_MOUNTAIN     : 48,
    VOLCANO            : 49,
    //PEAK
    SUMMIT             : 50,
    CALDERA            : 51,
    RIDGE              : 52,
    //PLAIN
    PRARIE             : 53,
    STEPPE             : 54,
    SAVANNA            : 55,
    COASTAL_PLAIN      : 56,
    // PLATEAU
    MESA               : 57,
    TABLELAND          : 58,
    // VALLEY
    FLUVIAL_VALLEY     : 59,
    GLACIAL_VALLEY     : 60,
    RIFT_VALLEY        : 61,
    // DESERT
    SANDY_DESERT       : 62,
    STONY_DESERT       : 63,
    POLAR_DESERT       : 64,
    SEMI_ARID_SCRUBLAND: 65,
    // FOREST
    TROPICAL_RAINFOREST: 66,
    DECIDUOUS_FOREST   : 67,
    TAIGA              : 68,
    // TUNDRA
    ICE_SHELF          : 69,
    ICE_TABLELANDS     : 70,
    ALPINE_TUNDRA      : 71,
    // KARST 
    SINKHOLE           : 72,
    TOWER_KARST        : 73,
    // LITHOSPEHERE 
    REGOLITH           : 74,
    BEDROCK            : 75,
    CRUSTAL_FAULT      : 76,
    // CAVE
    SURFACE_CAVE       : 77,
    SUBTERRENE_CAVE    : 78,
    ICE_CAVE           : 79,
    LAVA_TUBE          : 80,
    // SUBTERRANEAN_AQUIFIER
    AQUIFER            : 81,
    SUBTERRANEAN_RIVER : 82,
    KARST_GROUNDWATER_BASIN: 83,
    // DEEP_BIOSPHERE
    CONTINENTAL_DEEP_CRUST : 84,
    LAVA_CHAMBER       : 85,
    // HYDROTHERMAL_SYSTEM
    SEAFLOOR_FRACTURE: 86,
    MAGMATIC_CHAMBER_VENT: 87,
    GEOTHERMAL_AQUIFER: 88,

    // SPECIAL
    //EXOTIC
}


// types
export const BiomeType = {
    GARDEN: 'GARDEN',
    SWAMP: 'SWAMP', 
    BEACH: 'BEACH', 
    OCEAN: 'OCEAN', 
    FOREST: 'FOREST', 
    BADLAND: 'BADLAND', 
    GLACIER: 'GLACIER', 
    VOLCANIC: 'VOLCANIC',
} as const

export type BiomeType = typeof BiomeType[keyof typeof BiomeType]

export type BiomeConfig = {
    type: BiomeType,
    defaultColor: `#${string}`,
    influenceRadius: number,
}

// Biomes
const GARDEN: BiomeConfig = {
    type: BiomeType.GARDEN,
    defaultColor: '#79c261',
    influenceRadius: 50,
}

const SWAMP: BiomeConfig = {
    type: BiomeType.SWAMP,
    defaultColor: '#5d8c4d',
    influenceRadius: 28,
}

const BEACH: BiomeConfig = {
    type: BiomeType.BEACH,
    defaultColor: '#d9d77e',
    influenceRadius: 28,
}

const OCEAN: BiomeConfig = {
    type: BiomeType.OCEAN,
    defaultColor: '#5c80bd',
    influenceRadius: 28,
}

const FOREST: BiomeConfig = {
    type: BiomeType.FOREST,
    defaultColor: '#365c3b',
    influenceRadius: 28,
}

const BADLAND: BiomeConfig = {
    type: BiomeType.BADLAND,
    defaultColor: '#bf845c',
    influenceRadius: 28,
}

const GLACIER: BiomeConfig = {
    type: BiomeType.GLACIER,
    defaultColor: '#cbf5f7',
    influenceRadius: 28,
}

const VOLCANIC: BiomeConfig = {
    type: BiomeType.VOLCANIC,
    defaultColor: '#db5240',
    influenceRadius: 28,
}


// exports
export default {
    GARDEN,
    SWAMP,
    BEACH,
    OCEAN,
    FOREST,
    BADLAND,
    GLACIER,
    VOLCANIC,
}