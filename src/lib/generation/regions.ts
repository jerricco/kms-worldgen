/////////////////////
//     REGIONS     //
/////////////////////
export const RegionID: Record<string, number> = {
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

export const RegionName = Object.fromEntries(
    Object.entries(RegionID).map(([key, value]) => [value, key])
) as { [K in RegionID]: keyof typeof RegionID }
