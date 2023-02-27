import { RpcNoWait } from 'rpc';

export interface AudioVadWorker {
    create(artifactVersions: Map<string, string>): Promise<void>;
    init(workletPort: MessagePort, encoderWorkerPort: MessagePort): Promise<void>;
    reset(): Promise<void>;

    onFrame(buffer: ArrayBuffer, noWait?: RpcNoWait): Promise<void>;
}
