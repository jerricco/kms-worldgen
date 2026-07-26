import * as pc from 'playcanvas';

export class TextInputBinder extends pc.Script {
    static scriptName = 'text-input-binder';
    
    private currentText: string = ""
    private isFocused: boolean = false;
    private keydownHandler = this.handleKeyDown.bind(this)
    private blurHandler = this.blurInput.bind(this)

    initialize() {
        if (!this.entity || !this.entity.element)
            throw new Error("TextInputBinder: No target ELEMENT entity was found! Please provide one.");

        this.currentText = this.entity.element.text || this.currentText;

        // @NOTE: Ensure parent entity-component has 'useInput=true' for click detection
        if (this.entity.element) {
            this.entity.element.on('mousedown', this.focusInput, this);
            this.entity.element.on('touchstart', this.focusInput, this);
        }

        window.addEventListener('keydown', this.keydownHandler);
        window.addEventListener('mousedown', this.blurHandler);
        window.addEventListener('touchstart', this.blurHandler);
    }

    destroy() {
        window.removeEventListener('keydown', this.keydownHandler);
        window.removeEventListener('mousedown', this.blurHandler);
        window.removeEventListener('touchstart', this.blurHandler);

        if (this.entity.element) {
            this.entity.element.off('mousedown', this.focusInput, this);
            this.entity.element.off('touchstart', this.focusInput, this);
        }
    }

    private focusInput(e: FocusEvent) {
        e.stopPropagation();
        this.isFocused = true;
        // @TODO: indicate focus visually
        // this.app.keyboard.enabled = false; // disable game controls // @TODO: cleanly disable correct game control entities.
        this.entity.fire('ui:focus', this.currentText);
    }

    private handleKeyDown(e: KeyboardEvent) {
        // @TODO: handle ctrl+shift+arrows/backspace/(shift+arrows) like normal key input
        // Only accept typing if user has clicked/focused this UI element
        if (!this.isFocused) return;

        // Prevent browser defaults for tabs, arrows, or space scrolling while typing
        if (e.key === 'Backspace' || e.key === ' ') {
            e.preventDefault();
        }

        if (e.key === 'Backspace') {
            // Remove last character
            this.currentText = this.currentText.slice(0, -1);
        } else if (e.key === 'Enter') {
            this.blurInput()
            this.entity.fire('ui:key:enter', this.currentText)
            return;
        } else if (e.key.length === 1) {
            // Append single character keys (letters, numbers, space)
            this.currentText += e.key;
        }

        // Update the visible PlayCanvas UI Text element mesh
        if (this.entity && this.entity.element) {
            this.entity.element.text = this.currentText;
        }
    }

    private blurInput() {
        if (this.isFocused) {
            this.isFocused = false;
            // this.app.keyboard.enabled = true; // enable game controls // @TODO: cleanly disable correct game control entities.
            // @TODO: indicate blur visually
            this.entity.fire('ui:blur', this.currentText);
        }
    }
}