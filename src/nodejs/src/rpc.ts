import { PromiseSourceWithTimeout, ResolvedPromise } from 'promises';
import { Log, LogLevel, LogScope } from 'logging';
import { Disposable } from 'disposable';

const LogScope: LogScope = 'Rpc';
const debugLog = Log.get(LogScope, LogLevel.Debug);
const warnLog = Log.get(LogScope, LogLevel.Warn);
const errorLog = Log.get(LogScope, LogLevel.Error);
const mustRunSelfTest = debugLog != null;

export class RpcCall {
    constructor(
        public readonly id: number,
        public readonly method: string,
        public readonly args: unknown[],
    ) { }
}

let nextRpcResultId = 1;

export class RpcResult {
    public static value(id: number, value: unknown): RpcResult {
        return new RpcResult(id, value, undefined);
    }

    public static error(id: number, error: unknown): RpcResult {
        return new RpcResult(id, undefined, error);
    }

    constructor(
        public readonly id: number,
        public readonly value: unknown,
        public readonly error: unknown,
    ) { }
}

const rpcPromisesInProgress = new Map<number, RpcPromise<unknown>>();

export class RpcPromise<T> extends PromiseSourceWithTimeout<T> {
    public readonly id: number;

    constructor() {
        super();
        this.id = nextRpcResultId++;
        const oldResolve = this.resolve;
        const oldReject = this.reject;
        this.resolve = (value: T) => {
            debugLog?.log(`RpcPromise.resolve[#${this.id}] =`, value)
            this.unregister();
            oldResolve(value);
        };
        this.reject = (reason: unknown) => {
            debugLog?.log(`RpcPromise.reject[#${this.id}] =`, reason)
            this.unregister();
            oldReject(reason);
        };
        rpcPromisesInProgress.set(this.id, this);
        debugLog?.log(`RpcPromise.ctor[#${this.id}]`);
    }

    public static get<T>(id: number): RpcPromise<T> | null {
        return rpcPromisesInProgress.get(id) as RpcPromise<T> ?? null;
    }

    public unregister(): boolean {
        return rpcPromisesInProgress.delete(this.id);
    }
}

export function rpc<T>(sender: (rpcPromise: RpcPromise<T>) => void | PromiseLike<void>, timeoutMs?: number): RpcPromise<T> {
    const result = new RpcPromise<T>();
    if (timeoutMs)
        result.setTimeout(timeoutMs);

    void (async () => {
        try {
            await sender(result);
        }
        catch (error) {
            result.reject(error);
        }
    })();
    return result;
}

export function completeRpc(result: RpcResult): void {
    const rpcPromise = RpcPromise.get<unknown>(result.id);
    if (rpcPromise == null) {
        warnLog?.log(`completeRpc: RpcPromise #${result.id} is not found`);
        return;
    }
    try {
        if (result.error !== undefined)
            rpcPromise.reject(result.error);
        else
            rpcPromise.resolve(result.value);
    }
    catch (error) {
        rpcPromise.reject(error);
    }
}

export async function handleRpc<T>(
    resultId: number,
    resultSender: (result: RpcResult) => void | PromiseLike<void>,
    handler: () => Promise<T>,
    errorHandler?: (error: unknown) => void
): Promise<T> {
    let value: T | undefined = undefined;
    let error: unknown = undefined;
    try {
        value = await handler();
    }
    catch (e) {
        error = e;
    }
    const result = new RpcResult(resultId, value, error);
    debugLog?.log(`handleRpc[#${resultId}] =`, result)
    await resultSender(result);
    if (error !== undefined && errorHandler != null)
        errorHandler(error);
    return value;
}

export function isTransferable(x: unknown): x is Transferable {
    if (typeof x['postMessage'] === 'function')
        return true; // MessagePort
    return false;
}

function getTransferables(args: unknown[]): Transferable[] | undefined {
    let result: Transferable[] | undefined = undefined;
    for (let i = args.length - 1; i >= 0; i--) {
        const value = args[i];
        if (!isTransferable(value))
            break;

        if (!result)
            result = new Array<Transferable>(value);
        else
            result.push(value);
    }
    return result;
}

export function rpcServer(
    messagePort: MessagePort | Worker,
    serverImpl: object,
    onUnhandledMessage?: (event: MessageEvent<unknown>) => Promise<void>,
    onDispose?: () => void,
) : Disposable {
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    onUnhandledMessage ??= (event: MessageEvent<unknown>): Promise<void> => {
        throw `rpcServer: unhandled message.`;
    }

    const onMessage = async (event: MessageEvent<RpcCall>): Promise<void> => {
        const rpcCall = event.data;
        if (!rpcCall?.id) {
            await onUnhandledMessage(event);
            return;
        }
        debugLog?.log(`-> rpcServer.onMessage[#${rpcCall.id}]:`, rpcCall)
        let value: unknown = undefined;
        let error: unknown = undefined;
        try {
            // eslint-disable-next-line @typescript-eslint/ban-types
            const method = serverImpl[rpcCall.method] as Function;
            if (!method) {
                await onUnhandledMessage(event);
                return;
            }
            value = await method.apply(this, rpcCall.args);
        }
        catch (e) {
            error = e;
        }
        const result = new RpcResult(rpcCall.id, value, error);
        debugLog?.log(`<- rpcServer.onMessage[#${rpcCall.id}]:`, result)
        messagePort.postMessage(result);
    }

    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const onMessageError = (event: MessageEvent): Promise<void> => {
        throw `rpcServer: couldn't deserialize the message.`;
    }

    let isDisposed = false;
    const oldOnMessage = messagePort.onmessage;
    const oldOnMessageError = messagePort.onmessageerror;
    messagePort.onmessage = onMessage;
    messagePort.onmessageerror = onMessageError;

    return {
        dispose() {
            if (!isDisposed) {
                isDisposed = true;
                messagePort.onmessage = oldOnMessage;
                messagePort.onmessageerror = oldOnMessageError;
                if (onDispose)
                    onDispose();
            }
        }
    }
}

export function rpcClient<TService extends object>(
    messagePort: MessagePort | Worker,
    timeoutMs = 5000,
    onDispose?: () => void,
) : TService & Disposable {
    const onMessage = (event: MessageEvent<RpcResult>): void => {
        if (isDisposed)
            return;

        const message = event.data;
        if (message.id)
            void completeRpc(message);
    }

    const onMessageError = (event: MessageEvent<RpcResult>): void => {
        if (isDisposed)
            return;

        errorLog?.log('rpcClient.onMessageError:', event);
    }

    const proxyMethodCache = new Map<string, ((...args: unknown[]) => RpcPromise<unknown>)>();

    function getProxyMethod(method: string): ((...args: unknown[]) => RpcPromise<unknown>) {
        let result = proxyMethodCache.get(method);
        if (!result) {
            result = (...args: unknown[]): RpcPromise<unknown> => {
                if (isDisposed)
                    throw 'rpcClient.call: already disposed.';

                const rpcPromise = new RpcPromise<unknown>();
                if (timeoutMs)
                    rpcPromise.setTimeout(timeoutMs);

                const message = new RpcCall(rpcPromise.id, method, args);
                const transferables = getTransferables(args);
                debugLog?.log(`rpcClient.call:`, message, ', transfer:', transferables);
                messagePort.postMessage(message, transferables);
                return rpcPromise;
            }
            proxyMethodCache[method] = result;
        }

        return result;
    }

    const proxyTarget: Disposable = {
        dispose(): void {
            if (!isDisposed) {
                isDisposed = true;
                messagePort.onmessage = oldOnMessage;
                messagePort.onmessageerror = oldOnMessageError;
                if (onDispose)
                    onDispose();
            }
        }
    }
    const proxy = new Proxy<TService & Disposable>(proxyTarget as (TService & Disposable), {
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        get(target: TService, p: string | symbol, receiver: unknown): unknown {
            const ownValue = target[p] as unknown;
            if (ownValue || typeof(p) !== 'string')
                return ownValue;
            return getProxyMethod(p);
        }
    })

    let isDisposed = false;
    const oldOnMessage = messagePort.onmessage;
    const oldOnMessageError = messagePort.onmessageerror;
    messagePort.onmessage = onMessage;
    messagePort.onmessageerror = onMessageError;

    return proxy;
}

export function rpcClientServer<TClient extends object>(
    messagePort: MessagePort | Worker,
    serverImpl: object,
    timeoutMs?: number,
    onUnhandledMessage?: (event: MessageEvent<unknown>) => Promise<void>,
) : TClient & Disposable {
    const oldOnMessage = messagePort.onmessage;
    const oldOnMessageError = messagePort.onmessageerror;

    const onDispose = () => {
        server.dispose();
        messagePort.onmessage = oldOnMessage;
        messagePort.onmessageerror = oldOnMessageError;
    }

    const client = rpcClient<TClient>(messagePort, timeoutMs, onDispose);
    const clientOnMessage = messagePort.onmessage;
    const server = rpcServer(messagePort, serverImpl, onUnhandledMessage);
    const serverOnMessage = messagePort.onmessage;

    messagePort.onmessage = async (event: MessageEvent<RpcCall | RpcResult>): Promise<void> => {
        const data = event.data;
        if (data['method'])
            await serverOnMessage.call(messagePort, event);
        else
            await clientOnMessage.call(messagePort, event);
    }
    return client;
}

// This function is used only in tests below
async function whenNextMessage<T>(messagePort: MessagePort | Worker, timeoutMs?: number) : Promise<MessageEvent<T>> {
    const result = new PromiseSourceWithTimeout<MessageEvent<T>>();
    if (timeoutMs)
        result.setTimeout(timeoutMs);

    const oldOnMessage = messagePort.onmessage;
    messagePort.onmessage = (event: MessageEvent<T>) => result.resolve(event);
    try {
        return await result;
    }
    finally {
        messagePort.onmessage = oldOnMessage;
    }
}


// Self-test

if (mustRunSelfTest) {
    const testLog = errorLog;
    if (!testLog)
        throw 'testLog == null';
    void (async () => {
        return;
        // Basic test

        let rpcPromise = rpc<string>(() => undefined);
        testLog.assert(!rpcPromise.isCompleted());
        void completeRpc(RpcResult.value(rpcPromise.id, 'x'));
        testLog.assert(rpcPromise.isCompleted());
        testLog.assert('x' == await rpcPromise);

        rpcPromise = rpc<string>(() => undefined);
        testLog.assert(!rpcPromise.isCompleted());
        void completeRpc(RpcResult.error(rpcPromise.id, 'Error'));
        testLog.assert(rpcPromise.isCompleted());
        try {
            await rpcPromise;
            testLog.log('rpcPromise.Error is undefined.');
        }
        catch (error) {
            testLog.assert(error == 'Error', 'error != "Error"');
        }

        // RpcServer & rpcClient test

        interface TestService {
            mul(x: number, y: number): Promise<number>;
            ping(reply: string, port: MessagePort): Promise<void>;
        }

        class TestServer implements TestService {
            mul(x: number, y: number): Promise<number> {
                if (x === 1 || y === 1)
                    throw '1';
                return Promise.resolve(x * y);
            }

            ping(reply: string, port: MessagePort): Promise<void> {
                port.postMessage(reply);
                return ResolvedPromise.Void;
            }
        }

        const channel = new MessageChannel();
        const client = rpcClient<TestService>(channel.port1, 500);
        const server = rpcServer(channel.port2, new TestServer());

        // Normal call
        testLog.assert(await client.mul(3, 4) == 12);

        // Normal call w/ transferable
        const pingChannel = new MessageChannel();
        await client.ping('Pong', pingChannel.port2);
        const sideResult = (await whenNextMessage<string>(pingChannel.port1, 500)).data;
        debugLog?.log('Side channel result:', sideResult);
        testLog.assert(sideResult === 'Pong');

        // Error call
        try {
            await client.mul(1, 5);
            testLog.assert(false);
        }
        catch (e) {
            testLog.assert(e === '1');
        }

        // Post-dispose call
        client.dispose();
        try {
            await client.mul(3, 5);
            testLog.assert(false);
        }
        catch (e) {
            testLog.assert(!!e);
        }
    })();
}
