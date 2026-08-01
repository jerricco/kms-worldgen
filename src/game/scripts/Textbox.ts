import * as pc from 'playcanvas';
import { TextInputBinder } from './TextInputBinder';

export class Textbox extends pc.Script {
    static scriptName = 'textbox-input';

    initValue: string = '';
    label!: string;
    readonly!: boolean;

    private inputText: pc.Entity | null = null;
    private fireFocus = (e: FocusEvent) => this.entity?.fire('ui:focus', e);
    private fireBlur = (e: FocusEvent) => this.entity?.fire('ui:blur', e);
    private fireEnterPress = (e: KeyboardEvent) => this.entity?.fire('ui:key:enter', e);

    get inputValue(): String {
        return this.inputText?.element?.text || '';
    }

    set inputValue(text: string) {
        // update the text in appropriate places
        this.inputText.element.text = text;
        this.inputText?.script['text-input-binder'].setInputText(text)
    }

    initialize() {
        const fontAsset = this.app.assets.find('PatrickHandFont');
        const hasLabel = this.label && this.label.length > 0;
        
        if (hasLabel) {
            const labelWrapper = new pc.Entity('LabelWrapper');
            labelWrapper.addComponent('element', {
                type: pc.ELEMENTTYPE_IMAGE,
                anchor: new pc.Vec4(0, 0, 0.7, 1), 
                pivot: new pc.Vec2(0, 0),
                margin: new pc.Vec4(0, 0, 0, 0),
                color: new pc.Color(0.1, 0.1, 0.25, 1),
            });

            const labelElement = new pc.Entity('LabelElement');
            labelElement.addComponent('element', {
                type: pc.ELEMENTTYPE_TEXT,
                anchor: new pc.Vec4(0, 0, 0.7, 1),
                pivot: new pc.Vec2(0, 0),
                alignment: new pc.Vec2(0, 0.5),
                margin: new pc.Vec4(0, 0, 0, 0),
                text: this.label,
                fontSize: 24,
                color: new pc.Color(1, 1, 1),
                useInput: true,
                fontAsset,
            })

            labelWrapper.addChild(labelElement);
            this.entity.addChild(labelWrapper);
        }

        const inputElement = new pc.Entity('InputWrapper');
        inputElement.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(hasLabel ? 0.7 : 0, 0, 1, 1), // fill parent if no label
            pivot: new pc.Vec2(0, 0),
            margin: new pc.Vec4(0, 0, 0, 0),
            color: this.readonly ? new pc.Color(0.8, 0.8, 0.8, 1) : new pc.Color(1, 1, 1, 1),
        });

        this.inputText = new pc.Entity('InputText');
        this.inputText.addComponent('element', {
            type: pc.ELEMENTTYPE_TEXT,
            anchor: new pc.Vec4(0.02, 0.02, 0.98, 0.98), // fills parent (with a tiny buffer for focus states)
            pivot: new pc.Vec2(0, 0.5),
            alignment: new pc.Vec2(0, 0.5),
            margin: new pc.Vec4(0, 0, 0, 0),
            text: this.initValue,
            fontSize: 24,
            color: new pc.Color(0.1, 0.1, 0.1),
            useInput: true,
            fontAsset,
        });

        if (!this.readonly) {
            this.inputText.addComponent('script');
            this.inputText!.script!.create(TextInputBinder);
            // bubble events
            this.inputText.on('ui:key:enter', this.fireEnterPress);
            this.inputText.on('ui:focus', this.fireFocus);
            this.inputText.on('ui:blur', this.fireBlur);
        }
        
        inputElement.addChild(this.inputText);

        this.entity.addChild(inputElement);
    }

    destroy() {
        this.inputText?.off('ui:key:enter', this.fireEnterPress);
        this.inputText?.off('ui:focus', this.fireFocus);
        this.inputText?.off('ui:blur', this.fireBlur);
    }
}