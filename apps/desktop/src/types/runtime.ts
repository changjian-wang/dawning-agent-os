export interface HealthResponse {
  status: string;
  serverTimeUtc: string;
}

export interface PermissionDecisionResponse {
  level: string;
  requiresConfirmation: boolean;
  reason: string;
}

export interface RuntimeStatusResponse {
  status: string;
  dataDirectory: string;
  contentModificationDecision: PermissionDecisionResponse;
  serverTimeUtc: string;
}