import { SeededRandom } from './seed';

export class OpenSimplexNoise {
    private perm: number[] = [];

    constructor(prng: SeededRandom) {
        const source = Array.from({ length: 256 }, (_, i) => i);
        // Deterministic shuffle using PRNG
        for (let i = 255; i > 0; i--) {
            const j = prng.nextRange(0, i + 1);
            const temp = source[i];
            source[i] = source[j];
            source[j] = temp;
        }
        this.perm = [...source, ...source];
    }

    // Basic 2D Value/Perlin-style approximation for structured biome boundaries
    public noise2D(x: number, y: number): number {
        const X = Math.floor(x) & 255;
        const Y = Math.floor(y) & 255;

        const xf = x - Math.floor(x);
        const yf = y - Math.floor(y);

        const u = this.fade(xf);
        const v = this.fade(yf);

        const aa = this.perm[this.perm[X] + Y];
        const ab = this.perm[this.perm[X] + Y + 1];
        const ba = this.perm[this.perm[X + 1] + Y];
        const bb = this.perm[this.perm[X + 1] + Y + 1];

        const x1 = this.lerp(this.grad(aa, xf, yf), this.grad(ba, xf - 1, yf), u);
        const x2 = this.lerp(this.grad(ab, xf, yf - 1), this.grad(bb, xf - 1, yf - 1), u);

        return (this.lerp(x1, x2, v) + 1) / 2; // Normalized 0.0 to 1.0
    }

    private fade(t: number): number { 
        return t * t * t * (t * (t * 6 - 15) + 10); 
    }

    private lerp(a: number, b: number, t: number): number { 
        return a + t * (b - a);
    }

    private grad(hash: number, x: number, y: number): number {
        return (hash & 1 ? x : -x) + (hash & 2 ? y : -y);
    }
}