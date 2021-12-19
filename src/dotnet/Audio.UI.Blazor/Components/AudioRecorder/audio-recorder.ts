import RecordRTC, { MediaStreamRecorder, Options, State } from 'recordrtc';
import { ISendingQueue, SendingQueue, TimeoutCleaningStrategy } from './sending-queue';
import OpusMediaRecorder from 'opus-media-recorder';
import WebMOpusWasm from 'opus-media-recorder/WebMOpusEncoder.wasm';
import { VoiceActivityChanged } from './audio-vad';

const LogScope = 'AudioRecorder';
const sampleRate = 48000;

const opusWorkerOptions = {
    encoderWorkerFactory: _ => new Worker('/dist/encoderWorker.js'),
    WebMOpusEncoderWasmPath: WebMOpusWasm
};

const OpusMediaRecorderWrapper = Object.assign(function (stream: MediaStream, options?: MediaRecorderOptions) {
    return new OpusMediaRecorder(stream, options, opusWorkerOptions);
}, OpusMediaRecorder);

self["StandardMediaRecorder"] = self.MediaRecorder;
self["OpusMediaRecorder"] = OpusMediaRecorderWrapper;

self.MediaRecorder = OpusMediaRecorderWrapper;

export class AudioRecorder {
    protected readonly _debugMode: boolean;
    protected readonly _blazorRef: DotNet.DotNetObject;
    protected readonly isMicrophoneAvailable: boolean;
    protected recording: { recorder: RecordRTC, stream: MediaStream; context: AudioContext; };
    protected _queue: ISendingQueue;

    public constructor(blazorRef: DotNet.DotNetObject, debugMode: boolean, queue: ISendingQueue) {
        this._blazorRef = blazorRef;
        this._debugMode = debugMode;
        this.recording = null;
        this.isMicrophoneAvailable = false;
        this._queue = queue;

        if (blazorRef === undefined || blazorRef === null) {
            console.error(`${LogScope}.constructor.error: blazorRef is undefined`);
        }

        // Temporarily
        if (typeof navigator.mediaDevices === 'undefined' || !navigator.mediaDevices.getUserMedia) {
            alert('Please allow to use microphone.');

            if (navigator["getUserMedia"] !== undefined) {
                alert('This browser seems supporting deprecated getUserMedia API.');
            }
        } else {
            this.isMicrophoneAvailable = true;
        }
    }

    public static create(blazorRef: DotNet.DotNetObject, debugMode: boolean) {
        const queue: ISendingQueue = new SendingQueue({
            debugMode: debugMode,
            minChunkSize: 64,
            chunkSize: 1020,
            maxFillBufferTimeMs: 400,
            cleaningStrategy: new TimeoutCleaningStrategy(60_000),
            sendAsync: async (packet: Uint8Array): Promise<void> => {
                if (debugMode) {
                    console.log(`[${new Date(Date.now()).toISOString()}] AudioRecorder: Send to blazor side data size: ${packet.length}`);
                }
                await blazorRef.invokeMethodAsync('OnAudioData', packet);
            },
        });
        return new AudioRecorder(blazorRef, debugMode, queue);
    }

    public static changeMediaRecorder(useStandardMediaRecorder: boolean) {
        self.MediaRecorder = useStandardMediaRecorder
            ? self["StandardMediaRecorder"]
            : self["OpusMediaRecorder"];
    }

    public static isStandardMediaRecorder(): boolean {
        return self.MediaRecorder === self["StandardMediaRecorder"];
    }

    public dispose() {
        this.recording = null;
    }

    public async startRecording(): Promise<any> {
        if (this.isRecording())
            return null;
        if (!this.isMicrophoneAvailable) {
            console.error(`${LogScope}.startRecording: microphone is unavailable.`);
            return null;
        }

        const channel = new MessageChannel();
        const worker = new Worker('/dist/vadWorker.js');
        worker.onmessage = (ev: MessageEvent<VoiceActivityChanged>) => {
            const vadEvent = ev.data;
            const recording = this.recording;

            if (recording !== null) {
                const state = recording.recorder.getState();
                console.log(state);

                if (vadEvent.kind === 'end') {
                    this._queue.pause();
                }
                else {
                    this._queue.resume();
                }
            }
            console.log(vadEvent);
        };
        worker.postMessage({ topic: 'init-port' }, [channel.port1]);


        if (this.recording === null) {
            let stream: MediaStream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    channelCount: 1,
                    sampleRate: sampleRate,
                    sampleSize: 32,
                    // @ts-ignore
                    autoGainControl: {
                        ideal: true
                    },
                    echoCancellation: {
                        ideal: true
                    },
                    noiseSuppression: {
                        ideal: true
                    }
                },
                video: false
            });
            const options: Options = {
                type: 'audio',
                // @ts-ignore
                mimeType: 'audio/webm;codecs=opus',
                recorderType: MediaStreamRecorder,
                disableLogs: false,
                timeSlice: 60,
                checkForInactiveTracks: true,
                sampleRate: sampleRate,
                desiredSampleRate: sampleRate,
                bufferSize: 16384,
                bitsPerSecond: 32000,
                audioBitsPerSecond: 32000,
                audioBitrateMode: "constant",
                numberOfAudioChannels: 1,


                // as soon as the stream is available
                ondataavailable: async (blob: Blob) => {
                    try {
                        let buffer = await blob.arrayBuffer();
                        let chunk = new Uint8Array(buffer);
                        this._queue.enqueue(chunk);
                    } catch (e) {
                        console.error(`${LogScope}.startRecording: error ${e}`, e.stack);
                    }
                }
            };
            let recorder: RecordRTC = new RecordRTC(stream, options);

            recorder["stopRecordingAsync"] = (): Promise<void> =>
                new Promise((resolve, _) => recorder.stopRecording(() => resolve()));

            this.recording = {
                recorder: recorder,
                stream: stream,
                context: new AudioContext({ sampleRate: 16000, latencyHint: 'interactive' })
            };
        }

        const audioContext = this.recording.context;
        const sourceNode = audioContext.createMediaStreamSource(this.recording.stream);

        if (navigator.userAgent.includes('Safari') && !navigator.userAgent.includes('Chrome')) {
            const response = await fetch('/dist/vadWorklet.js');
            const blob = await response.blob();
            const reader = new FileReader();
            reader.onloadend = (ev) => {
                audioContext.audioWorklet.addModule(reader.result as string);
            };
            reader.readAsText(blob);
        } else {
            await audioContext.audioWorklet.addModule('/dist/vadWorklet.js');
        }
        const audioWorkletOptions: AudioWorkletNodeOptions = {
            numberOfInputs: 1,
            numberOfOutputs: 1,
            channelCount: 1,
            channelInterpretation: 'speakers',
            channelCountMode: 'explicit',
        };
        const vadWorkletNode = new AudioWorkletNode(audioContext, 'audio-vad.worklet-processor', audioWorkletOptions);
        vadWorkletNode.port.postMessage({ topic: 'init-port' }, [channel.port2]);
        sourceNode.connect(vadWorkletNode);

        this.recording.recorder.startRecording();
        await this._blazorRef.invokeMethodAsync('OnStartRecording');
    }

    public async stopRecording(): Promise<void> {
        if (!this.isRecording())
            return;
        if (this._debugMode) {
            console.log(`[${new Date(Date.now()).toISOString()}] AudioRecorder: Received stop recording`);
        }

        let recording = this.recording;
        this.recording = null;

        if (recording !== null) {
            await recording.context.close();
            recording.stream.getAudioTracks().forEach(t => t.stop());
            recording.stream.getVideoTracks().forEach(t => t.stop());
            await recording.recorder["stopRecordingAsync"]();
        }
        await this._queue.flushAsync();
        await this._blazorRef.invokeMethodAsync('OnRecordingStopped');
        if (this._debugMode) {
            console.log(`[${new Date(Date.now()).toISOString()}] AudioRecorder: OnRecordingStopped interop call is done`);
        }

    }

    private isRecording() {
        return this.recording !== null && this.recording.recorder !== null && this.recording.recorder.getState() === 'recording';
    }
}
