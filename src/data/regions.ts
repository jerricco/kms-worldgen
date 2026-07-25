/////////////////////
//     REGIONS     //
/////////////////////

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