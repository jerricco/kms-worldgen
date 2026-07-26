import * as pc from 'playcanvas';
import { TextInputBinder } from '../scripts/TextInputBinder';

export class TextboxEntity {
    static create(app?: pc.AppBase, parentEntity: pc.Entity | null = null, initValue: string = "",): pc.Entity {
        if (!app) throw new Error('TextboxEntity: please provide a valid playcanvas application');

        // @TODO: better font handling
        const fontAsset = app.assets.find('PatrickHandFont');

        const inputElement = new pc.Entity('InputWrapper');
        inputElement.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 0, 1, 1), // fill parent
            pivot: new pc.Vec2(0, 0),
            margin: new pc.Vec4(0, 0, 0, 0),
            color: new pc.Color(1, 1, 1, 1),
        });

        // @TODO: ensure the textbox masks overlfow text and can scroll through the whole value if it does.
        const inputText = new pc.Entity('InputText');
        inputText.addComponent('element', {
            type: pc.ELEMENTTYPE_TEXT,
            anchor: new pc.Vec4(0.02, 0.02, 0.98, 0.98), // fills parent (with a tiny buffer for focus states)
            pivot: new pc.Vec2(0, 0.5),
            alignment: new pc.Vec2(0, 0.5),
            margin: new pc.Vec4(0, 0, 0, 0),
            text: initValue,
            fontSize: 24,
            color: new pc.Color(0.1, 0.1, 0.1),
            useInput: true,
            fontAsset,
        });

        inputText.addComponent('script');
        inputText!.script!.create(TextInputBinder);
        inputElement.addChild(inputText);

        // bubble events
        // @TODO: convert this to a script so that it can safely destroy them easily.
        inputText.on('ui:focus', (e) => inputElement.fire('ui:focus', e));
        inputText.on('ui:blur', (e) => inputElement.fire('ui:blur', e));

        if (parentEntity instanceof pc.Entity) {
            parentEntity.addChild(inputElement)
        }

        return inputElement;
    }
}