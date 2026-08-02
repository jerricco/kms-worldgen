/////////////////////
//     REGIONS     //
/////////////////////
export const RegionID: Record<string, number> = {
    // SPECIAL
    VOID        : 0,
    UNASSIGNED  : 1,
    // AQUATIC REGIONS
    // elevation
    CRUST_FLOOR : 2,
    ABYSSAL_OCEAN: 3,
    DEEP_OCEAN  : 4,
    OCEAN       : 5,
    // geographic
    SEA         : 6,
    // climatic
    FRESH_LAKE  : 7,
    SALINE_LAKE : 8,
    REEF        : 9,
    // TRANSITIONAL REGIONS
    // geographic
    BEACH       : 10,
    CLIFF       : 11,
    ISLAND      : 12,
    // climatic
    WETLAND     : 13,
    ESTUARY     : 14,
    // climatic + rivers
    RIVER       : 15,
    RIVER_DELTA : 16,
    // TERRESTRIAL REGIONS
    // elevation regions
    HILL        : 17,
    MOUNTAIN    : 18,
    PEAK        : 19,
    // climatic
    PLAIN       : 20,
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

export const RegionName = Object.fromEntries(
    Object.entries(RegionID).map(([key, value]) => [value, key])
) as { [K in RegionID]: keyof typeof RegionID }
