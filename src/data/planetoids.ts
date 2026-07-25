

/////////////////////
// PLANETOID TYPES //
/////////////////////
export const PLANETOID = {
    TERRESTRIAL: 'TERRESTRIAL'
} as const

export type PLANETOID = typeof PLANETOID[keyof typeof PLANETOID]

export type PLANETOID_CONFIG = {
    name: PLANETOID;
}

///////////////////////
// PLANETOID CONFIGS //
///////////////////////
const TERRESTRIAL: PLANETOID_CONFIG = {
    name: PLANETOID.TERRESTRIAL,
}

export const PLANETOIDS = {
    TERRESTRIAL,
};