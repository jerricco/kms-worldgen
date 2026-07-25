// In degrees
export function getMachineTempChange(
    heatOutput: number, // DTU/s
    mass: number, // in grams,
    material: kms.materials.MATERIAL, // @TODO: should this be the object material or just the name for lookup?
) {
    // getMaterial(material) -> Need a way to get the material from data globally loaded.
    return heatOutput / (mass * material.states.common.specific_heat_capacity) // @TODO: should get the proper SHC based on state
}

export function tickHeatChangeBetweenTiles(
    t1: number,
    m1: kms.materials.MATERIAL,
    s1: kms.materials.STATE,
    t2: number, 
    m2: kms.materials.MATERIAL,
    s2: kms.materials.STATE,
    time: number = 1, // @NOTE: Game ticks, might need to aggregate this calc, or also use real time?
) {
    // @TODO: get TC from state -> can sugar material type??
    const TC = Math.min(
        m1.states.common.thermal_conductivity, 
        m2.states.common.thermal_conductivity
    )

    const tempDiff = t1 < t2 ? t2 - t1 : t1 - t2;
    // @NOTE: these are ONI specific multipliers -> MUST TEST THESE, DO NOT LEAVE
    const multiplier = (() => {
        const states = kms.materials.STATE;
        if (
            (s1 === states.SOLID && s2 === states.GAS) ||
            (s1 === states.GAS && s2 === states.SOLID)
        ) return 25;

        if (s1 === states.LIQUID && s2 === states.LIQUID) return 625;

        return 1;
    })()

    return TC * tempDiff * multiplier * time
}