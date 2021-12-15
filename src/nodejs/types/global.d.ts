import type { OGVCompat } from "ogv";

declare global {

    interface AudioWorkletProcessor {
        // https://developer.mozilla.org/en-US/docs/Web/API/AudioWorkletProcessor/process
        process(inputs: Float32Array[][], outputs: Float32Array[][], parameters: { [name: string]: Float32Array; }): boolean;
        // https://developer.mozilla.org/en-US/docs/Web/API/AudioWorkletProcessor/AudioWorkletProcessor
        new(options?: AudioWorkletNodeOptions): AudioWorkletProcessor;
    }

    var OGVCompat: OGVCompat;
}

export { };