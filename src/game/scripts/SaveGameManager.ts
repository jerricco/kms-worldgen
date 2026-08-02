import * as pc from 'playcanvas';
import type { GenerationSettings } from '../../lib/generation/MapGenerator';
import murmur from 'murmurhash3js';
import type { VoronoiSite } from '../../lib/generation/VoronoiCluster';
import type { Chunk, SerialChunk } from '../../lib/generation/chunk';
import { getMany, set, setMany } from 'idb-keyval';

export interface SaveData {
    settings: GenerationSettings; // user-configured generation settings
    voronoi: VoronoiSite[];       // vononoi sites for pregeneration pass. Can reconstruct the rest of VornonoiFactory
    chunks: Map<string, Chunk> | SerialChunk[];   // chunks saved to disk, streamed as they generate
}

export type SavePartial = GenerationSettings | VoronoiSite[] | string | SerialChunk | SerialChunk[] | undefined

export class SaveGameManager extends pc.Script {
    static scriptName = 'save-game-manager';


    // GenerationConfig object the savemanager starts with.
    config!: GenerationSettings;

    // the key for the current save file, which references all data associated to it.
    // requires a GeneratorSettings from the app (@TODO: eventually must handle whole game saves,
    // not just a config, but we don't yet have game creation)
    private saveKey!: number; // 32-bit integer for indexdb key storage

    async initialize() {
        // turn the loaded config into a saveKey & clear
        if (!this.config) throw new Error('SaveGameManager: no JSON.strinfigy of the game\'s GenerationSettings was supplied!');

        this.saveKey = murmur.x86.hash32(JSON.stringify(this.config))
        console.log('setting saveKey', this.saveKey)
        this.config = undefined; // remove the config so we can use the same manager to reload a file later.
    }

    /* save all current game resources into a single savefile **/
    /* this expects all properties present, however chunks may be empty **/
    // @TODO: brute force save everything once the intial chunk pass is done, to avoid slowing it down
    // since it's the heaviest processing of the app
    public async save(data: SaveData) {
        const chunkLocations: string[] = []; 
        const saveSet: [string, SavePartial][] = [
            [`${this.saveKey}-settings`, data.settings],
            [`${this.saveKey}-voronoi`, data.voronoi]
        ];

        data.chunks.forEach((chunk: Chunk, key: string) => {
            saveSet.push([`${this.saveKey}-${key}`, chunk.serialize()]);
            chunkLocations.push(key)
        });

        // save a set of chunk locations so later we know what loaded chunks to retrieve for this save.
        if (chunkLocations.length > 0) {
            saveSet.push([`${this.saveKey}-chunk-locations`, chunkLocations.join('|')])
        }

        // @ts-ignore
        await setMany(saveSet)
    }

    /* save part of a save-file for seat-of-the-pants save data **/
    // @TODO: finish this once I can stream chunks on demand. cbf atm. Needs to handle Chunk | Chunk[]
    public async savePartial(key: string, data: SavePartial) {
        const saveKey = `${this.saveKey}-${key}`;
        // handle Chunk[] here.
        const saveData = JSON.stringify(data);

        return await set(saveKey, saveData);
    }

    public async load(): Promise<SaveData> {
        const loadSet = [
            `${this.saveKey}-settings`, 
            `${this.saveKey}-voronoi`,
            `${this.saveKey}-chunk-locations`,
        ];

        // load initial data
        const [settings, voronoi, chunkLocations]: SavePartial[] = await getMany(loadSet);
        const chunkIds = (chunkLocations || '').split('|')
        const chunkSaveKeys = chunkIds.map((loc: string) => `${this.saveKey}-${loc}`);
        // load all chunks
        const chunks = await getMany(chunkSaveKeys);
        
        return { 
            settings: settings as GenerationSettings, 
            voronoi: voronoi as VoronoiSite[], 
            chunks 
        };
    }
}