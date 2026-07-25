

const Material: kms.materials.MATERIAL = {
    id: 0,
    name: "Sulfur",
    type: kms.materials.TYPE.ELEMENT,
    tags: [
        // IONISABLE // -> can ionise its gaseous state into cold plasma
    ],
    states: {
        common: {
            thermal_conductivity: 0,
            specific_heat_capacity: 0,
            light_absorption: 0,
            radio_absorption: 0,
            electric_conductivity: 0,
            density: 2070,
            corrosivity: 0,
            supercritical_point: [1314, 563]
        },
        solid: {
            name: 'Sulfur',
            type: kms.materials.SOLID_TYPE.REACTIVE,
            melting_temp: 388.3,
        },
        liquid: {
            name: 'Liquid Sulfur',
            type: kms.materials.LIQUID_TYPE.LIQUID_CHEMICAL,
            viscosity: {
                all: 0.3,
                [160]: 1.6  // changes viscosity above 160 -> this is sulfur's Polymerization (λ) Transition
            },
            boiling_temp: 717.8
        },
        gas: {
            name: 'Sulfur Gas',
            type: kms.materials.GAS_TYPE.REACTIVE,
            // if this is the same as the supercritical_point temp,
            // then the only way plasma can be generated is by it being PLASMA_TYPE.COLD
            ionisation_temp: 1314
        },
        plasma: {
            name: 'Sulfur Plasma',
            type: kms.materials.PLASMA_TYPE.COLD,
        },
    }
}

export default Material;