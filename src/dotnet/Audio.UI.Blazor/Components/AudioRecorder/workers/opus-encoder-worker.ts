/// #if MEM_LEAK_DETECTION
import codec, { Encoder, Codec } from '@actual-chat/codec/codec.debug';
import codecWasm from '@actual-chat/codec/codec.debug.wasm';
import codecWasmMap from '@actual-chat/codec/codec.debug.wasm.map';
/// #else
/// #code import codec, { Decoder, Codec } from '@actual-chat/codec';
/// #code import codecWasm from '@actual-chat/codec/codec.wasm';
/// #endif

import Denque from 'denque';
import * as signalR from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { ResolveCallbackMessage } from 'resolve-callback-message';

import { EndMessage, EncoderMessage, InitEncoderMessage, CreateEncoderMessage } from './opus-encoder-worker-message';
import { BufferEncoderWorkletMessage } from '../worklets/opus-encoder-worklet-message';
import { VoiceActivityChanged } from './audio-vad';

/// #if MEM_LEAK_DETECTION
console.info('MEM_LEAK_DETECTION == true');
/// #endif

// TODO: create wrapper around module for all workers

let codecModule: Codec | null = null;
const codecModuleReady = codec(getEmscriptenLoaderOptions()).then(val => {
    codecModule = val;
    self['codec'] = codecModule;
});

function getEmscriptenLoaderOptions(): EmscriptenLoaderOptions {
    return {
        locateFile: (filename: string) => {
            if (filename.slice(-4) === 'wasm')
                return codecWasm;
            /// #if MEM_LEAK_DETECTION
            else if (filename.slice(-3) === 'map')
                return codecWasmMap;
            /// #endif
            // Allow secondary resources like the .wasm payload to be loaded by the emscripten code.
            // emscripten 1.37.25 loads memory initializers as data: URI
            else if (filename.slice(0, 5) === 'data:')
                return filename;
            else throw new Error(`Emscripten module tried to load an unknown file: "${filename}"`);
        },
    };
}

const CHUNKS_WILL_BE_SENT_ON_RESUME = 3;
/** buffer or callbackId: number of `end` message */
const queue = new Denque<ArrayBuffer | number>();
const worker = self as unknown as Worker;
let connection: signalR.HubConnection;
let recordingSubject = new signalR.Subject<Uint8Array>();
// TODO: check statuses / add an additional status field for VAD
let state: 'inactive' | 'readyToInit' | 'encoding' | 'paused' | 'ended' = 'inactive';
let workletPort: MessagePort = null;
let vadPort: MessagePort = null;
let encoder: Encoder;
let lastInitMessage: InitEncoderMessage | null = null;
let isEncoding = false;

const debug = false;

/** control flow from the main thread */
worker.onmessage = async (ev: MessageEvent<EncoderMessage>) => {
    try {
        const msg = ev.data;
        switch (msg.type) {
            case 'create':
                await onCreate(msg as CreateEncoderMessage, ev.ports[0], ev.ports[1]);
                break;

            case 'init':
                await onInit(msg as InitEncoderMessage);
                break;

            case 'end':
                onEnd(msg as EndMessage);
                break;
            default:
                throw new Error(`Encoder worker: Got unknown message type ${msg.type as string}`);
        }
    }
    catch (error) {
        console.error(error);
    }
};

function onEnd(message: EndMessage) {
    state = 'ended';
    queue.push(message.callbackId);
    processQueue();
}

async function onInit(message: InitEncoderMessage): Promise<void> {
    const { sessionId, chatId, callbackId } = message;
    lastInitMessage = message;

    state = 'encoding';

    recordingSubject = new signalR.Subject<Uint8Array>();
    await connection.send('ProcessAudio', sessionId, chatId, Date.now() / 1000, recordingSubject);

    if (debug) {
        console.log('init recorder worker');
    }
    const msg: ResolveCallbackMessage = { callbackId };
    worker.postMessage(msg);
}

async function onCreate(message: CreateEncoderMessage, workletMessagePort: MessagePort, vadMessagePort: MessagePort): Promise<void> {
    if (workletPort != null) {
        throw new Error('EncoderWorker: workletPort has already been specified.');
    }
    if (vadPort != null) {
        throw new Error('EncoderWorker: vadPort has already been specified.');
    }

    const { audioHubUrl, callbackId } = message;
    workletPort = workletMessagePort;
    vadPort = vadMessagePort;
    workletPort.onmessage = onWorkletMessage;
    vadPort.onmessage = onVadMessage;
    connection = new signalR.HubConnectionBuilder()
        .withUrl(audioHubUrl)
        .withAutomaticReconnect([0, 300, 500, 1000, 3000, 10000])
        .withHubProtocol(new MessagePackHubProtocol())
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Connect to the hub endpoint
    await connection.start();

    // Setting encoder module
    if (codecModule == null) {
        await codecModuleReady;
    }
    encoder = new codecModule.Encoder();
    console.warn('create', encoder);

    // Notify the host ready to accept 'init' message.
    const readyToInit: ResolveCallbackMessage = {
        callbackId
    };
    worker.postMessage(readyToInit);
    state = 'readyToInit';
}
// worklet sends messages with raw audio
const onWorkletMessage = (ev: MessageEvent<BufferEncoderWorkletMessage>) => {
    try {
        const { type, buffer } = ev.data;
        // TODO: add offset & length to the message type
        let audioBuffer: ArrayBuffer;
        switch (type) {
            case 'buffer':
                audioBuffer = buffer;
                break;
            default:
                break;
        }
        if (audioBuffer.byteLength !== 0) {
            if (state === 'encoding') {
                queue.push(buffer);
                processQueue();
            }
            else if (state === 'ended') {
                // nop, we don't need to save a buffer if the user call stop
            }
            else if (state === 'paused') {
                // vad status is
                queue.push(buffer);
                if (queue.length > CHUNKS_WILL_BE_SENT_ON_RESUME) {
                    queue.shift();
                }
            }
        }
    }
    catch (error) {
        console.error(error);
    }
};

const onVadMessage = async (ev: MessageEvent<VoiceActivityChanged>) => {
    try {
        const vadEvent = ev.data;
        if (debug) {
            console.log(vadEvent);
        }

        if (state === 'encoding') {
            if (vadEvent.kind === 'end') {
                state = 'paused';
                recordingSubject.complete();
            }
        } else if (state == 'paused' && vadEvent.kind === 'start') {
            if (!lastInitMessage) {
                throw new Error('OpusEncoderWorker: unable to resume streaming lastNewStreamMessage is null');
            }

            const { sessionId, chatId } = lastInitMessage;
            recordingSubject = new signalR.Subject<Uint8Array>();
            await connection.send('ProcessAudio', sessionId, chatId, Date.now() / 1000, recordingSubject);
            // TODO: after await we can override new state, fix this.
            state = 'encoding';
            processQueue();
        }
    }
    catch (error) {
        console.error(error);
    }
};

function processQueue(): void {
    if (queue.isEmpty()) {
        return;
    }

    if (isEncoding || state === 'paused') {
        return;
    }

    try {
        isEncoding = true;
        const item: ArrayBuffer | number = queue.shift();
        if (typeof (item) === 'number') {
            try {
                const message: ResolveCallbackMessage = { callbackId: item, };
                worker.postMessage(message);
            }
            finally {
                recordingSubject.complete();
            }
        }
        else {
            const result = encoder.encode(item);
            const workletMessage: BufferEncoderWorkletMessage = { type: 'buffer', buffer: item };
            workletPort.postMessage(workletMessage, [item]);
            recordingSubject.next(result);
        }
    }
    catch (error) {
        console.error('Encoder worker: Unhandled processing error:', error);
    }
    finally {
        isEncoding = false;
    }
    processQueue();
}
