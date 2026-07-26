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