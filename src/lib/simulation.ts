import type { Planetoid, PlanetoidGrid } from "./generation";

export class PlanetoidSimulator {
    static SIM_TICK_RATE = 5;
    static SIM_TIMESTEP = 0;
    static MAX_ACCUMULATED_FRAMES = 5;

    public planetoid: Planetoid;
    public lastSimTime = 0
    public accumulator = 0
    public ticks = 0
    public timestep: number;

    constructor(planetoid: Planetoid) {
        this.planetoid = planetoid;
        this.timestep = 1000 / PlanetoidSimulator.SIM_TICK_RATE;
    }

    setNow() {
        this.lastSimTime = performance.now()
    }

    simulate(currentTime: number): void {
        let deltaTime = currentTime - this.lastSimTime
        this.lastSimTime = currentTime;

        this.accumulator += deltaTime;

        // stop lockouts on large accumulated jumps
        const max = PlanetoidSimulator.MAX_ACCUMULATED_FRAMES
        if (this.accumulator > this.timestep * max) {
            this.accumulator = this.timestep
        }

        while (this.accumulator >= this.timestep) {
            this.tickSimulation(this.planetoid.grid)
            this.accumulator -= this.timestep
            this.ticks++

            // console.log(`${this.ticks % 2 ? 'TICK' : 'TOCK'}: ${this.ticks}`)
        }

        requestAnimationFrame(this.simulate.bind(this))
    }

    tickSimulation(grid: PlanetoidGrid) {
        if (!grid) return;

        for (let y = 0; y < this.planetoid.height; y++) {
            for (let x = 0; x < this.planetoid.width; x++) {
                const tile = grid[`${x},${y}`];
                if (!tile) continue;
            }
        }
    }
}