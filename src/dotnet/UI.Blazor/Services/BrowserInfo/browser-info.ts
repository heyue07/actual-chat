import { PromiseSource } from 'promises';
import { Log, LogLevel } from 'logging';
import { audioContextLazy } from 'audio-context-lazy';
import { ScreenSize } from '../ScreenSize/screen-size';

const LogScope = 'BrowserInfo';
const log = Log.get(LogScope, LogLevel.Info);
const debugLog = Log.get(LogScope, LogLevel.Debug);
const warnLog = Log.get(LogScope, LogLevel.Warn);
const errorLog = Log.get(LogScope, LogLevel.Error);

export class BrowserInfo {
    private static backendRef: DotNet.DotNetObject = null;

    public static utcOffset: number;
    public static isMobile: boolean;
    public static isTouchCapable: boolean;
    public static isMaui: boolean;
    public static windowId: string = "";
    public static whenReady: PromiseSource<void> = new PromiseSource<void>();

    public static init(backendRef1: DotNet.DotNetObject, isMaui: boolean): void {
        this.backendRef = backendRef1;
        this.utcOffset = new Date().getTimezoneOffset();
        // @ts-ignore
        this.windowId = window.App.windowId;
        this.isMaui = isMaui;
        if (isMaui) {
            audioContextLazy.skipWaitForNextInteraction();
        }

        const userAgentData: { mobile?: boolean; } = self.navigator['userAgentData'] as { mobile?: boolean; };
        this.isMobile = userAgentData?.mobile
            // Additional check for browsers which don't support userAgentData
            ?? /Android|Mobile|Phone|webOS|iPhone|iPad|iPod|BlackBerry/i.test(self.navigator.userAgent);
        this.isTouchCapable =
            ( 'ontouchstart' in window )
            || ( navigator.maxTouchPoints > 0 )
            // @ts-ignore
            || ( navigator.msMaxTouchPoints > 0 );

        // Call OnInitialized
        const initResult: InitResult = {
            screenSizeText: ScreenSize.size,
            utcOffset: this.utcOffset,
            isMobile: this.isMobile,
            isTouchCapable: this.isTouchCapable,
            isMaui: this.isMaui,
            windowId: this.windowId,
        };
        log?.log(`init:`, initResult);
        void this.backendRef.invokeMethodAsync('OnInitialized', initResult);

        this.whenReady.resolve(undefined);
        ScreenSize.sizeChange$.subscribe(x => this.onScreenSizeChanged(x))
    }

    // Backend methods

    private static onScreenSizeChanged(screenSize: string): void {
        log?.log(`onScreenSizeChanged, screenSize:`, screenSize);
        this.backendRef.invokeMethodAsync('OnScreenSizeChanged', screenSize)
    };
}

export interface InitResult {
    screenSizeText: string;
    utcOffset: number;
    isMobile: boolean;
    isTouchCapable: boolean;
    isMaui: boolean;
    windowId: string;
}
