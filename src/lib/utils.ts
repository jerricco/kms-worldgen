export function hexToRgb(hex: string) {
    // Strip the # if it's there
    hex = hex.replace(/^#/, '');

    // Parse the hex substrings into base-10 integers and normalise for 0->1 for playcanvas
    return {
        r: parseInt(hex.substring(0, 2), 16) / 255,
        g: parseInt(hex.substring(2, 4), 16) / 255,
        b: parseInt(hex.substring(4, 6), 16) / 255
    };
}