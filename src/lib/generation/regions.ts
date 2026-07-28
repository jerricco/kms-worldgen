/////////////////////
//     REGIONS     //
/////////////////////
// geographic - requires land shape + elevation
// climatic - climatic (wetness, wind) + elevation + land shape

export const RegionID = {
    // SPECIAL
    VOID        : 0,
    UNASSIGNED  : 1,
    // AQUATIC REGIONS
    // elevation
    ABYSSAL     : 2,
    DEEP_OCEAN  : 3,
    OCEAN       : 4,
    // geographic
    SEA         : 5,
    // climatic
    FRESH_LAKE  : 6,
    SALINE_LAKE : 7,
    REEF        : 8,
    // TRANSITIONAL REGIONS
    // geographic
    BEACH       : 9,
    CLIFF       : 10,
    ISLAND      : 11,
    // climatic
    WETLAND     : 12,
    ESTUARY     : 13,
    // climatic + rivers
    RIVER       : 14,
    RIVER_DELTA : 15,
    // TERRESTRIAL REGIONS
    // elevation regions
    HILL        : 16,
    MOUNTAIN    : 17,
    PEAK        : 18,
    // climatic
    PLAIN       : 19,
    // geographic
    PLATEAU     : 21,
    VALLEY      : 22,
    // climatic
    DESERT      : 23,
    FOREST      : 24,
    TUNDRA      : 25,
    // lithogaphic
    KARST       : 26,
    // subterrene
    LITHOSPHERE : 27,
    CAVE        : 28,
    SUBTERRANEAN_AQUIFER: 29,
    // special
    DEEP_BIOSPHERE: 30,
    HYDROTHERMAL_SYSTEM: 31,
}

export type RegionID = typeof RegionID[keyof typeof RegionID]

// config shape
export type REGION_CONFIG = {
    name: string,
    hydrological?: boolean,
}

// enum types
export const REGION = {
    // home regions
    HOME: 'HOME',
    SURROUNDS: 'SURROUNDS',

    // geographic regions
    PLAINS: 'PLAINS',
    MOUNTAIN: 'MOUNTAIN',
    HILLS: 'HILLS',
    FOOTHILLS: 'FOOTHILLS',
    OCEAN: 'OCEAN',
    DEEP_OCEAN: 'DEEP_OCEAN',
    ABYSSAL: 'ABYSSAL',
    BEACH: 'BEACH',

    // reallocation regions
    BASIN: 'BASIN',
    LAKE: 'LAKE',
    POND: 'POND',
    BILLABONG: 'BILLABONG',
    HIGH_BASIN: 'HIGH_BASIN',
    HIGH_LAKE: 'HIGH_LAKE',
    ISTHMUS: 'ISTHMUS',
    ISLAND: 'ISLAND',

    // sugar regions
    PEAK: 'PEAK',
    CLIFF: 'CLIFF',
    RIVER: 'RIVER',
    RIVERBANK: 'RIVERBANK',
    HYDRO_BASIN: 'HYDRO_BASIN',
} as const

export type REGION = typeof REGION[keyof typeof REGION]


// configuration object
export const REGIONS: { [key: string]: REGION_CONFIG } = {
    PEAK: {
        name: REGION.PEAK,
    },
    CLIFF: {
        name: REGION.CLIFF,
    },
    MOUNTAIN: {
        name: REGION.MOUNTAIN,
    },
    HIGH_BASIN: {
        name: REGION.HIGH_BASIN,
    },
    HILLS: {
        name: REGION.HILLS,
    },
    FOOTHILLS: {
        name: REGION.FOOTHILLS,
    },
    PLAINS: {
        name: REGION.PLAINS,
    },
    BASIN: {
        name: REGION.BASIN,
    },
    
    // feature regions
    BEACH: {
        name: REGION.BEACH,
    },

    // hydrological regions
    HYDRO_BASIN: {
        name: REGION.HYDRO_BASIN,
        hydrological: true,
    },
    POND: {
        name: REGION.POND,
        hydrological: true,
    },
    HIGH_LAKE: {
        name: REGION.HIGH_LAKE,
        hydrological: true,
    },
    LAKE: {
        name: REGION.LAKE,
        hydrological: true,
    },
    OCEAN: {
        name: REGION.OCEAN,
        hydrological: true,
    },
    DEEP_OCEAN: {
        name: REGION.DEEP_OCEAN,
        hydrological: true,
    },
    ABYSSAL: {
        name: REGION.ABYSSAL,
        hydrological: true,
    },
    RIVER: {
        name: REGION.RIVER,
        hydrological: true,
    },
    RIVERBANK: {
        name: REGION.RIVERBANK,
        hydrological: true,
    },
    BILLABONG: {
        name: REGION.BILLABONG,
        hydrological: true,
    },
}