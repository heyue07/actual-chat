import { Disposable } from 'disposable';
import { delayAsync, PromiseSource, PromiseSourceWithTimeout, serialize, TimedOut } from 'promises';
import { EventHandler, EventHandlerSet } from 'event-handling';
import { Interactive } from 'interactive';
import { OnDeviceAwake } from 'on-device-awake';
import { AudioContextRef } from 'audio-context-ref';
import { BrowserInfo } from '../../dotnet/UI.Blazor/Services/BrowserInfo/browser-info';
import { Log, LogLevel, LogScope } from 'logging';
import { InteractiveUI } from '../../dotnet/UI.Blazor/Services/InteractiveUI/interactive-ui';

const LogScope: LogScope = 'AudioContextSource';
const debugLog = Log.get(LogScope, LogLevel.Debug);
const warnLog = Log.get(LogScope, LogLevel.Warn);
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const errorLog = Log.get(LogScope, LogLevel.Error);

const MaintainCyclePeriodMs = 2000;
const FixCyclePeriodMs = 300;
const MaxWarmupTimeMs = 2000;
const MaxResumeTimeMs = 600;
const MaxResumeCount = 60;
const MaxInteractiveResumeCount = 3;
const MaxSuspendTimeMs = 300;
const MaxInteractionWaitTimeMs = 60_000;
const TestIntervalMs = 40;
const WakeUpDetectionIntervalMs = 5000;

export class AudioContextSource implements Disposable {
    private _audioContext: AudioContext | null = null;
    private _isDisposed = false;
    private _onDeviceAwakeHandler: EventHandler<void>;
    private _deviceWokeUpAt = 0;
    private _isInteractiveWasReset = false;
    private _changeCount = 0;
    private _resumeCount = 0;
    private _interactiveResumeCount = 0;
    private _whenReady = new PromiseSource<AudioContext | null>();
    private _whenNotReady = new PromiseSource<void>();
    private readonly _whenDisposed: Promise<void>;

    public readonly changedEvents = new EventHandlerSet<AudioContext | null>();

    constructor() {
        this._onDeviceAwakeHandler = OnDeviceAwake.events.add(() => this.onDeviceAwake());
        this._whenDisposed = this.maintain();
    }

    public get refCount() {
        return this.changedEvents.count;
    }

    public dispose(): void {
        if (this._isDisposed)
            return;

        this._isDisposed = true;
        this.markNotReady(); // This ensures _whenReady is not completed
        this.markReady(null); // Final "ready" event produces null AudioContext
        this._onDeviceAwakeHandler.dispose();
    }

    public whenDisposed(): Promise<void> {
        return this._whenDisposed;
    }

    public async getRef(): Promise<AudioContextRef> {
        debugLog?.log('-> getRef()');
        this.throwIfDisposed();
        try {
            const audioContext = await this._whenReady;
            this.throwIfDisposed();
            return new AudioContextRef(this, audioContext);
        }
        finally {
            debugLog?.log('<- getRef, refCount:', this.refCount);
        }
    }

    // NOTE(AY): both markReady and markNotReady are written so that
    // they can be called repeatedly. Subsequent calls to them produce no effect.

    // Must be private, but good to keep it near markNotReady
    private markReady(audioContext: AudioContext | null) {
        // Invariant it maintains on exit:
        // - _whenReady is completed - and if it's completed, it exists immediately
        // - _whenNotReady is NOT completed
        // In other words, _whenReady state is "ground truth", _whenNotReady state is secondary

        Interactive.isInteractive = true;
        if (this._whenReady.isCompleted())
            return; // Already ready

        this._audioContext = audioContext;
        this._changeCount++;
        debugLog?.log(`markReady(): #${this._changeCount}, AudioContext:`, audioContext);

        // _whenNotReady must be replaced first
        if (this._whenNotReady.isCompleted())
            this._whenNotReady = new PromiseSource<void>();

        this._whenReady.resolve(audioContext);
        if (this._changeCount > 1)
            this.changedEvents.trigger(audioContext)
    }

    public markNotReady(): void {
        // Invariant it maintains on exit:
        // - _whenReady is NOT completed - and if it's NOT completed, it exists immediately
        // - _whenNotReady is completed completed
        // In other words, _whenReady state is "ground truth", _whenNotReady state is secondary

        if (!this._whenReady.isCompleted())
            return; // Already not ready

        debugLog?.log(`markNotReady()`);

        // _whenReady must be replaced first
        this._whenReady = new PromiseSource<AudioContext>();

        if (!this._whenNotReady.isCompleted())
            this._whenNotReady.resolve(undefined);
    }

    // Protected methods

    protected async maintain(): Promise<void> {
        let lastTestTimestamp = Date.now();

        for (;;) { // Renew loop
            if (this._isDisposed)
                return;

            let audioContext = this._audioContext;
            try {
                if (audioContext === null || audioContext.state === 'closed') {
                    audioContext = await this.create();
                    await this.warmup(audioContext);
                    await this.test(audioContext, true);
                    this.markReady(audioContext);
                }
                this.throwIfDisposed();

                // noinspection InfiniteLoopJS
                for (;;) { // Fix loop
                    this.throwIfDisposed();

                    const currentTimestamp = Date.now();
                    const timePassedSinceTest = currentTimestamp - lastTestTimestamp;
                    if (timePassedSinceTest < MaintainCyclePeriodMs) {
                        await delayAsync(MaintainCyclePeriodMs - timePassedSinceTest);
                    }
                    else {
                        const whenDelayCompleted = delayAsync(MaintainCyclePeriodMs);
                        await Promise.race([this._whenNotReady, whenDelayCompleted]);
                    }
                    this.throwIfDisposed();

                    // Let's try to test whether AudioContext is broken and fix
                    try {
                        lastTestTimestamp = Date.now();
                        await this.test(audioContext);
                        // See the description of markReady/markNotReady to understand the invariant it maintains
                        this.markReady(audioContext);
                        continue;
                    }
                    catch (e) {
                        warnLog?.log(`maintain: AudioContext is actually broken:`, e);
                        // See the description of markReady/markNotReady to understand the invariant it maintains
                        this.markNotReady();
                    }

                    for(;;) {
                        this.throwIfDisposed();
                        this.throwIfTooManuResumes();
                        try {
                            await this.fix(audioContext);
                            break;
                        }
                        catch (e) {
                            await delayAsync(FixCyclePeriodMs);
                        }
                    }
                    this.markReady(audioContext);
                }
            }
            catch (e) {
                warnLog?.log(`maintain: error:`, e);
            }
            finally {
                this.markNotReady();
            }
        }
    }

    protected async create(): Promise<AudioContext> {
        debugLog?.log(`create()`);

        this._resumeCount = 0;
        this._interactiveResumeCount = 0;
        // Try to create audio context early w/o waiting for user interaction.
        // It might be in suspended state in this case.
        const audioContext = new AudioContext({
            latencyHint: 'interactive',
            sampleRate: 48000,
        });
        try {
            await this.interactiveResume(audioContext);
            await this.test(audioContext);

            debugLog?.log(`create(): loading modules`);
            const whenModule1 = audioContext.audioWorklet.addModule('/dist/feederWorklet.js');
            const whenModule2 = audioContext.audioWorklet.addModule('/dist/opusEncoderWorklet.js');
            const whenModule3 = audioContext.audioWorklet.addModule('/dist/vadWorklet.js');
            await Promise.all([whenModule1, whenModule2, whenModule3]);
            return audioContext;
        }
        catch (e) {
            await this.closeSilently(audioContext);
            throw e;
        }
    }

    protected async warmup(audioContext: AudioContext): Promise<void> {
        debugLog?.log(`warmup(), AudioContext:`, audioContext);

        await audioContext.audioWorklet.addModule('/dist/warmUpWorklet.js');
        const nodeOptions: AudioWorkletNodeOptions = {
            channelCount: 1,
            channelCountMode: 'explicit',
            numberOfInputs: 0,
            numberOfOutputs: 1,
            outputChannelCount: [1],
        };
        const node = new AudioWorkletNode(audioContext, 'warmUpWorklet', nodeOptions);
        node.connect(audioContext.destination);

        try {
            const whenWarmedUp = new PromiseSourceWithTimeout<void>();
            node.port.postMessage('stop');
            node.port.onmessage = (ev: MessageEvent<string>): void => {
                if (ev.data === 'stopped')
                    whenWarmedUp.resolve(undefined);
                else
                    warnLog?.log(`warmup: unsupported message from warm up worklet`);
            };
            whenWarmedUp.setTimeout(MaxWarmupTimeMs, () => {
                whenWarmedUp.reject(`${LogScope}.warmup: couldn't complete warm-up on time.`);
            })
            await whenWarmedUp;
        }
        finally {
            node.disconnect();
            node.port.onmessage = null;
            node.port.close();
        }
    }

    protected async test(audioContext: AudioContext, isFirstTest = false): Promise<void> {
        if (audioContext.state !== 'running')
            throw `${LogScope}.test: AudioContext isn't running.`;

        const lastTime = audioContext.currentTime;
        const testCycleCount = isFirstTest ? 6 : 2;
        for (let i = 0; i < testCycleCount; i++) {
            await delayAsync(TestIntervalMs);
            if (audioContext.state !== 'running')
                throw `${LogScope}.test: AudioContext isn't running.`;
            if (audioContext.currentTime != lastTime)
                break;
        }
        if (audioContext.currentTime == lastTime) // AudioContext isn't running
            throw `${LogScope}.test: AudioContext is running, but didn't pass currentTime test.`;
    }

    protected async fix(audioContext: AudioContext): Promise<void> {
        debugLog?.log(`fix(): AudioContext:`, audioContext);

        try {
            if (!await this.trySuspend(audioContext)) {
                // noinspection ExceptionCaughtLocallyJS
                throw `${LogScope}.fix: couldn't suspend AudioContext`;
            }
            await this.interactiveResume(audioContext);
            await this.test(audioContext);
            debugLog?.log(`fix: success`, );
        }
        catch (e) {
            warnLog?.log(`fix: failed, error:`, e);
            throw e;
        }
    }

    protected async interactiveResume(audioContext: AudioContext): Promise<void> {
        debugLog?.log(`interactiveResume(): AudioContext:`, audioContext);
        if (audioContext && this.isRunning(audioContext)) {
            debugLog?.log(`interactiveResume(): succeeded (AudioContext is already in running state)`);
            return;
        }

        await BrowserInfo.whenReady; // This is where isAlwaysInteractive flag gets set - it checked further
        if (Interactive.isAlwaysInteractive) {
            debugLog?.log(`interactiveResume(): Interactive.isAlwaysInteractive == true`);
        }
        else {
            // Resume can be called during user interaction only
            const isWakeUp = this.isWakeUp();
            if (isWakeUp && !this._isInteractiveWasReset) {
                this._isInteractiveWasReset = true;
                Interactive.isInteractive = false;
                debugLog?.log(`interactiveResume(): Interactive.isInteractive was reset on wake up`);
            }
        }

        if (Interactive.isInteractive) {
            await this.resume(audioContext, false);
            debugLog?.log(`interactiveResume(): succeeded w/o interaction`);
        }
        else {
            debugLog?.log(`interactiveResume(): waiting for interaction`);
            const e = await Interactive.interactionEvents.whenNextWithTimeout(MaxInteractionWaitTimeMs);
            if (e instanceof TimedOut) {
                // noinspection ExceptionCaughtLocallyJS
                throw `${LogScope}.interactiveResume: timed out while waiting for interaction`;
            }
            await this.resume(audioContext, true);
            debugLog?.log(`interactiveResume(): succeeded on interaction`);
        }
    }

    private async resume(audioContext: AudioContext, isInteractive: boolean): Promise<void> {
        debugLog?.log(`resume(): AudioContext:`, audioContext);

        this._resumeCount++;
        if (isInteractive)
            this._interactiveResumeCount++;

        if (this.isRunning(audioContext)) {
            debugLog?.log(`resume(): already resumed, AudioContext:`, audioContext);
            return;
        }

        const resumeTask = audioContext.resume().then(() => true);
        const timerTask = delayAsync(MaxResumeTimeMs).then(() => false);
        if (!await Promise.race([resumeTask, timerTask]))
            throw `${LogScope}.resume: AudioContext.resume() has timed out`;
        if (!this.isRunning(audioContext))
            throw `${LogScope}.resume: completed resume, but AudioContext.state != 'running'`;

        debugLog?.log(`resume(): resumed, AudioContext:`, audioContext);
    }

    protected async trySuspend(audioContext: AudioContext): Promise<boolean> {
        if (audioContext.state === 'suspended') {
            debugLog?.log(`trySuspend(): already suspended, AudioContext:`, audioContext);
            return true;
        }

        debugLog?.log(`trySuspend(): AudioContext:`, audioContext);
        const suspendTask = audioContext.suspend().then(() => true);
        const timerTask = delayAsync(MaxSuspendTimeMs).then(() => false);
        if (await Promise.race([suspendTask, timerTask])) {
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore
            if (audioContext.state !== 'suspended') {
                debugLog?.log(`trySuspend: completed suspend, but AudioContext.state != 'suspended'`);
                return false;
            }
            debugLog?.log(`trySuspend: success`);
            return true;
        }
        else {
            debugLog?.log(`trySuspend: timed out`);
            return false;
        }
    }


    protected isRunning(audioContext: AudioContext): boolean {
        // This method addresses some weird issues in how AudioContext behaves in different browsers:
        // - Chromium 110 AudioContext can be in 'running' even after
        //   calling constructor, and even without user interaction.
        // - Safari doesn't start incrementing 'currentTime' after 'resume' call,
        //   so we have to warm it up w/ silent audio
        if (audioContext.state !== 'running')
            return false;

        const buffer = audioContext.createBuffer(1, 1, 48000);
        const source = audioContext.createBufferSource();
        source.buffer = buffer;
        source.connect(audioContext.destination);
        source.start(0);
        source.disconnect();
        // NOTE(AK): Somehow - sporadically - currentTime starts ticking only when you log the context!
        console.log(`AudioContext is:`, audioContext, `, its currentTime:`, audioContext.currentTime);
        return audioContext.state === 'running';
    }

    protected async closeSilently(audioContext?: AudioContext): Promise<void> {
        debugLog?.log(`close(): AudioContext:`, audioContext);
        if (!audioContext)
            return;
        if (audioContext.state === 'closed')
            return;

        try {
            await audioContext.close();
        }
        catch (e) {
            warnLog?.log(`close: failed to close AudioContext:`, e)
        }
    }

    private throwIfTooManuResumes(): void {
        if (this._resumeCount >= MaxResumeCount)
            throw `maintain: resume attempt count is too high (${this._resumeCount})`;
        if (this._interactiveResumeCount >= MaxInteractiveResumeCount)
            throw `maintain: interactive resume attempt count is too high (${this._interactiveResumeCount})`;
    }

    private throwIfDisposed(): void {
        if (this._isDisposed)
            throw `${LogScope}.throwIfDisposed: already disposed.`;
    }

    private isWakeUp(): boolean {
        return (Date.now() - this._deviceWokeUpAt) <= WakeUpDetectionIntervalMs;
    }

    // Event handlers

    private onDeviceAwake() {
        debugLog?.log(`onDeviceAwake()`);
        this._deviceWokeUpAt = Date.now();
        this._isInteractiveWasReset = false;
        // close current AudioContext as it might be corrupted and can produce clicking sound
        void this._audioContext.close();
        this.markNotReady();
    }
}

// Init

export const audioContextSource = new AudioContextSource();
globalThis['audioContextSource'] = audioContextSource;

