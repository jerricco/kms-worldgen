declare global {
    type Merge<T, U> = {
        [K in keyof (T & U)]: K extends keyof U ? U[K] : K extends keyof T ? T[K] : never;
    };

    namespace kms.materials {
        enum STATE { 
            GAS = 'gas', 
            LIQUID = 'liquid', 
            SOLID = 'solid', 
            PLASMA ='plasma', 
        }

        enum TYPE {
            ELEMENT = 'element',
            MIXTURE = 'mixture',
            ALLOY = 'alloy',
            COMPOSITE = 'composite',
            SOLUTION = 'solution',
            VACUUM = 'vacuum', 
            VOID = 'void',
        }

        type STATE_INFORMATION = {
            thermal_conductivity: number, // 0.0 -> 256.0
            specific_heat_capacity: number, // 0.0 -> 10.0
            light_absorption: number, // 0.0 -> 1.0
            radio_absorption: number, // 0.0 -> 1.0
            electric_conductivity: number, // -1 -> 1
            density: number, // true density value in kg/m^3
            corrosivity: number, // 0.0 -> 1.0
            supercritical_point: [number, number], // [temp K, kg per tile]
        }

        type STATE_PROPS<
            T extends SOLID_TYPE | LIQUID_TYPE | GAS_TYPE | PLASMA_TYPE
        > = {
            name: N,
            type: T,
        }

        type STATE_PROPS_WITH_OPTIONAL_INFO = STATE_PROPS & Partial<STATE_INFORMATION>

        //tags
        // - supercritical

        type STATE_DEF = {
            common: STATE_INFORMATION,
            [STATE.SOLID]: STATE_PROPS_WITH_OPTIONAL_INFO & {
                hardness: number, // 0 - 255
                permeability: number, // 0.0 -> 1.0
                melting_temp: number,
            }
            [STATE.LIQUID]: STATE_PROPS_WITH_OPTIONAL_INFO & {
                // object notation lets the property change when its temperature hits a lower bound.
                viscosity: number | { ['all' | number]: number }, 
                surface_tension: number,
                boiling_temp: number,
            },
            [STATE.GAS]: STATE_PROPS_WITH_OPTIONAL_INFO & {
                compressibility: number, // 0 - 255
                respirativeness: number, // 0.0 -> 1.0
                ionisation_temp: number,
            },
            [STATE.PLASMA]: STATE_PROPS_WITH_OPTIONAL_INFO & {
                luminecense: number, // 0 - 1,000,000,000 (Lux),
            },
        }

        enum MATERIAL_TAG { SUPERCRITICAL, SUPERCONDUCTIVE, CORROSIVE }
        
        enum SOLID_TYPE {
            AGRICULTURAL, 
            ORGANIC_ORE, // coal, peat, etc
            ORGANIC_MIX, // algae, sludge
            PARTICULATE, // SAND, etc 
            MANUFACTURED, // enriched materials, steel, etc
            PROCESSED, // Rubber, sugar, etc
            METAL_ORE, 
            MINERAL, 
            MISCELLANEOUS, 
            CERAMIC, 
            POLYMER, 
            COMPOSITE, // Carbon Fibre, Plastium, etc
            CRYSTAL,
            REACTIVE,
            SPECIAL,
        }

        enum LIQUID_TYPE { AQUEOUS, ORGANIC, PETROCHEM, LIQUID_METAL, LIQUID_CHEMICAL, LIQUID }
        
        enum GAS_TYPE { BREATHABLE, UNBREATHABLE, REACTIVE, GASEOUS_METAL, MISCELLANEOUS }
        enum PLASMA_TYPE { DUSTY, HOT, COLD }


        enum QOL_TAG { LIQUEFIABLE, SUBLIMATOR, EVAPORATOR }

        type MATERIAL = {
            id: number, // unique, hardcoded ID.
            name: string,
            displayName: string,
            states: STATE_DEF
            properties: PROPERTIES,
        }
    }
}

export {};