import { Log, LogLevel } from 'logging';

const LogScope: string = 'HistoryUI';
const debugLog = Log.get(LogScope, LogLevel.Debug);
const warnLog = Log.get(LogScope, LogLevel.Warn);
const errorLog = Log.get(LogScope, LogLevel.Error);

export class HistoryUI {
    static create(): HistoryUI {
        debugLog?.log(`create`);
        return new HistoryUI();
    }

    public getState = (): unknown => {
        const currentState = history.state;
        if (currentState && currentState.hasOwnProperty('userState')) {
            const state = currentState.userState;
            debugLog?.log(`getState:`, state);
            return state;
        }
        debugLog?.log(`getState:`, currentState);
        return currentState;
    }

    public setState = (state: unknown): void => {
        debugLog?.log(`setState:`, state);
        const currentState = history.state;
        if (currentState && currentState.hasOwnProperty('userState')) {
            currentState.userState = state;
            history.replaceState(currentState, '');
        }
        else {
            history.replaceState(state, '');
        }
    }
}
