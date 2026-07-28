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
    ABYSSAL_PLAIN      : 0, // subterrene
    HYDROTHERMAL_VENT  : 0, // subterrene
    BATHYPELAGIC       : 0,
    // DEEP_OCEAN
    PELAGIC            : 0, 
    BENTHIC            : 0, // subterrene
    // OCEAN
    SUNLIT_OCEAN       : 0,
    CONTINENTAL_SHELF  : 0, // subterrene
    SHORELINE          : 0,
    // SEA
    MARGINAL_SEA       : 0,
    MEDITERRANEAN_SEA  : 0,
    BAY                : 0,
    GULF               : 0,
    // FRESH_LAKE
    CLEAR_LAKE         : 0,
    RICH_LAKE          : 0,
    BILLABONG          : 0,
    // SALINE_LAKE
    SALT_FLAT          : 0,
    ALKALINE_LAKE      : 0,
    HYPERSALINE_BASIN  : 0,
    // REEF
    FRINGE_REEF        : 0,
    BARRIER_REEF       : 0,
    ATOL               : 0,
    //BEACH
    SAND_BEACH         : 0,
    ROCK_BEACH         : 0,
    MUDFLAT            : 0,
    // CLIFF 
    SEA_CLIFF          : 0,
    ESCARPMENT         : 0,
    GORGE              : 0,
    // ISLAND 
    VOLCANIC_ISLAND    : 0,
    CORAL_CAY          : 0,
    ARCHIPELAGO        : 0,
    // WETLAND 
    SWAMP              : 0,
    MARSH              : 0,
    BOG                : 0,
    // ESTUARY 
    FJORD              : 0,
    SALT_MARSH_ESTUARY : 0,
    LAGOON             : 0,
    // RIVER 
    TORRENTIAL_RIVER   : 0,
    MEANDERING_RIVER   : 0,
    LOW_COURSE_RIVER   : 0,
    // RIVER DELTA 
    BIRD_FOOT_DELTA    : 0,
    WAVE_DELTA         : 0,
    TIDE_DELTA         : 0,
    // HILL
    ROLLING_HILL       : 0,
    MOUND              : 0,
    BUTTE              : 0,
    FOOTHILL           : 0,
    // MOUNTAIN
    FOLD_MOUNTAIN      : 0,
    FAULT_MOUNTAIN     : 0,
    VOLCANO            : 0,
    //PEAK
    SUMMIT             : 0,
    CALDERA            : 0,
    RIDGE              : 0,
    //PLAIN
    PRARIE             : 0,
    STEPPE             : 0,
    SAVANNA            : 0,
    COASTAL_PLAIN      : 0,
    // PLATEAU
    MESA               : 0,
    TABLELAND          : 0,
    // VALLEY
    FLUVIAL_VALLEY     : 0,
    GLACIAL_VALLEY     : 0,
    RIFT_VALLEY        : 0,
    // DESERT
    SANDY_DESERT       : 0,
    STONY_DESERT       : 0,
    POLAR_DESERT       : 0,
    SEMI_ARID_SCRUBLAND: 0,
    // FOREST
    TROPICAL_RAINFOREST: 0,
    DECIDUOUS_FOREST   : 0,
    TAIGA              : 0,
    // TUNDRA
    ICE_SHELF          : 0,
    ICE_TABLELANDS     : 0,
    ALPINE_TUNDRA      : 0,
    // KARST 
    SINKHOLE           : 0,
    TOWER_KARST        : 0,
    // LITHOSPEHERE 
    REGOLITH           : 0,
    BEDROCK            : 0,
    CRUSTAL_FAULT      : 0,
    // CAVE
    SURFACE_CAVE       : 0,
    SUBTERRENE_CAVE    : 0,
    ICE_CAVE           : 0,
    LAVA_TUBE          : 0,
    // SUBTERRANEAN_AQUIFIER
    AQUIFER            : 0,
    SUBTERRANEAN_RIVER : 0,
    KARST_GROUNDWATER_BASIN: 0,
    // DEEP_BIOSPHERE
    CONTINENTAL_DEEP_CRUST : 0,
    LAVA_CHAMBER       : 0,
    // HYDROTHERMAL_SYSTEM
    SEAFLOOR_FRACTURE: 0,
    MAGMATIC_CHAMBER_VENT: 0,
    GEOTHERMAL_AQUIFER: 0,

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