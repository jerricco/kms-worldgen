// SFC32 PRNG Algorithm
export class SeededRandom {
    private a: number;
    private b: number;
    private c: number;
    private d: number;

    // util for converting string via has to a 32-bit integer
    static xmur3(str: string): () => number {
        let h = 1779033703 ^ str.length;
        for (let i = 0; i < str.length; i++) {
            h = Math.imul(h ^ str.charCodeAt(i), 3432918353);
            h = (h << 13) | (h >>> 19);
        }
        return () => {
            h = Math.imul(h ^ (h >>> 16), 2246822507);
            h = Math.imul(h ^ (h >>> 13), 3266489909);
            return (h ^= h >>> 16) >>> 0;
        };
    }

    constructor(seedStr: string) {
        const seedGen = SeededRandom.xmur3(seedStr);
        this.a = seedGen();
        this.b = seedGen();
        this.c = seedGen();
        this.d = seedGen();
    }
    
    // Returns a floating-point number between 0 (inclusive) and 1 (exclusive)
    public next(): number {
        this.a >>>= 0; this.b >>>= 0; this.c >>>= 0; this.d >>>= 0;
        let t = (this.a + this.b) | 0;
        this.a = this.b ^ (this.b >>> 9);
        this.b = (this.c + (this.c << 3)) | 0;
        this.c = (this.c << 21) | (this.c >>> 11);
        this.d = (this.d + 1) | 0;
        t = (t + this.d) | 0;
        this.c = (this.c + t) | 0;
        return (t >>> 0) / 4294967296;
    }
    
    // Helper to get a random integer range
    public nextRange(min: number, max: number): number {
        return Math.floor(this.next() * (max - min)) + min;
    }
}