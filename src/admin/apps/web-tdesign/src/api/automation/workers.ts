import { requestClient } from '#/api/request';

export namespace AutomationApi {
  export type WorkerStatus = 'active' | 'disabled' | 'maintenance';
  export type WorkerType = 'codex';
  export type EmployeeType = 'audit' | 'development' | 'operations' | 'product' | 'test';
  export type SessionStatus =
    | 'auth_required'
    | 'busy'
    | 'error'
    | 'expired'
    | 'idle'
    | 'offline'
    | 'starting';

  export type WorkerCommandType =
    | 'cancel_current_run'
    | 'reload_config'
    | 'smoke'
    | 'start_bug'
    | 'start_task'
    | 'stop_after_current';

  export interface DigitalWorker {
    agentTokenId?: string;
    agentUserId: string;
    code: string;
    createTime: string;
    createdBy: string;
    description?: string;
    endpointIds: string[];
    employeeType: EmployeeType;
    heartbeatTimeoutSeconds: number;
    idleMaxIntervalSeconds: number;
    codexHome: string;
    codexModel: string;
    codexProvider: string;
    configVersion: number;
    id: string;
    maxConcurrentRuns: number;
    maxRunMinutes: number;
    name: string;
    openAiBaseUrl?: string;
    pollIntervalSeconds: number;
    projectIds: string[];
    runSmokeOnStartup: boolean;
    runsRoot: string;
    sandboxMode: string;
    skillIds: string[];
    smokePrompt?: string;
    status: WorkerStatus;
    updateTime?: string;
    workspaceRoot: string;
    workerType: WorkerType;
  }

  export interface DigitalWorkerDetail {
    currentRun?: WorkerRun;
    latestSession?: WorkerSession;
    pendingCommands: WorkerCommand[];
    worker: DigitalWorker;
  }

  export interface SaveDigitalWorkerRequest {
    agentTokenId?: string;
    agentUserId: string;
    code?: string;
    description?: string;
    endpointIds?: string[];
    employeeType?: EmployeeType;
    heartbeatTimeoutSeconds?: number;
    idleMaxIntervalSeconds?: number;
    codexHome?: string;
    codexModel?: string;
    codexProvider?: string;
    maxConcurrentRuns?: number;
    maxRunMinutes?: number;
    name: string;
    openAiBaseUrl?: string;
    pollIntervalSeconds?: number;
    projectIds?: string[];
    runSmokeOnStartup?: boolean;
    runsRoot?: string;
    sandboxMode?: string;
    skillIds?: string[];
    smokePrompt?: string;
    status?: WorkerStatus;
    workspaceRoot?: string;
    workerType?: WorkerType;
  }

  export interface WorkerSession {
    codexHome?: string;
    codexVersion?: string;
    configTomlExists: boolean;
    containerId?: string;
    currentRunId?: string;
    dotnetVersion?: string;
    errorSummary?: string;
    gitVersion?: string;
    hostName?: string;
    id: string;
    instanceId: string;
    lastHeartbeatAt?: string;
    nodeVersion?: string;
    runsRoot?: string;
    startedAt: string;
    status: SessionStatus;
    stoppedAt?: string;
    workerId: string;
    workspaceRoot?: string;
  }

  export interface WorkerCommand {
    ackedAt?: string;
    commandType: WorkerCommandType;
    completedAt?: string;
    createTime: string;
    createdBy: string;
    error?: string;
    expiresAt?: string;
    id: string;
    payloadJson?: string;
    resultJson?: string;
    sessionId?: string;
    startedAt?: string;
    status: string;
    workerId: string;
  }

  export interface WorkerRun {
    commandId?: string;
    completedAt?: string;
    error?: string;
    exitCode?: number;
    finalPath?: string;
    id: string;
    manifestPath?: string;
    promptPath?: string;
    runType: string;
    sessionId: string;
    startedAt: string;
    status: string;
    stderrPath?: string;
    stdoutPath?: string;
    targetId?: string;
    targetType?: string;
    timedOut: boolean;
    workerId: string;
    workspacePath?: string;
  }

  export interface WorkerEvent {
    createTime: string;
    eventType: string;
    id: string;
    level: string;
    message: string;
    payloadJson?: string;
    runId?: string;
    sessionId?: string;
    workerId: string;
  }
}

function normalizeQuery(params?: Record<string, string | undefined>) {
  if (!params) {
    return undefined;
  }

  return Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== undefined && value !== ''),
  );
}

export function listDigitalWorkersApi(params?: {
  keyword?: string;
  status?: string;
  workerType?: string;
}) {
  return requestClient.get<AutomationApi.DigitalWorker[]>('/workers', {
    params: normalizeQuery(params),
  });
}

export function createDigitalWorkerApi(data: AutomationApi.SaveDigitalWorkerRequest) {
  return requestClient.post<AutomationApi.DigitalWorker>('/workers', data);
}

export function updateDigitalWorkerApi(id: string, data: AutomationApi.SaveDigitalWorkerRequest) {
  return requestClient.put<AutomationApi.DigitalWorker>(`/workers/${id}`, data);
}

export function getDigitalWorkerDetailApi(id: string) {
  return requestClient.get<AutomationApi.DigitalWorkerDetail>(`/workers/${id}/detail`);
}

export function setDigitalWorkerStatusApi(id: string, status: AutomationApi.WorkerStatus) {
  return requestClient.post<AutomationApi.DigitalWorker>(`/workers/${id}/status`, { status });
}

export function createWorkerCommandApi(data: {
  commandType: AutomationApi.WorkerCommandType;
  expiresAt?: string;
  payloadJson?: string;
  sessionId?: string;
  workerId: string;
}) {
  return requestClient.post<AutomationApi.WorkerCommand>('/workers/commands', data);
}

export function listWorkerSessionsApi(params?: { status?: string; workerId?: string }) {
  return requestClient.get<AutomationApi.WorkerSession[]>('/workers/sessions', {
    params: normalizeQuery(params),
  });
}

export function listWorkerRunsApi(params?: {
  sessionId?: string;
  status?: string;
  targetId?: string;
  targetType?: string;
  workerId?: string;
}) {
  return requestClient.get<AutomationApi.WorkerRun[]>('/workers/runs', {
    params: normalizeQuery(params),
  });
}

export function listWorkerEventsApi(params?: {
  eventType?: string;
  runId?: string;
  sessionId?: string;
  workerId?: string;
}) {
  return requestClient.get<AutomationApi.WorkerEvent[]>('/workers/events', {
    params: normalizeQuery(params),
  });
}
