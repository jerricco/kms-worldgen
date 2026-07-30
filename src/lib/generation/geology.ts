// @TODO: this is clumsy, optimise it.
export interface SubterraneanLayer  {
    bedrockDepth: number,
    sedimentaryThickness: number,
    primaryRockType: BasementRockType,
}

export type BasementRockType = "basalt" | "granite" | "limestone" | "sandstone" | "sedimentary";