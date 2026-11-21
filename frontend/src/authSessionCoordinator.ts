export interface RefreshableSession { accessToken: string; expiresAt: string; user: { id: number } }
export interface SessionSnapshot { generation: number; accessToken: string | null; userId?: number }
export class StaleSessionError extends Error { constructor() { super('The logical session changed while the request was pending.'); this.name = 'StaleSessionError'; } }
export class AccountChangedError extends Error { constructor() { super('The cookie belongs to another account.'); this.name = 'AccountChangedError'; } }

/** Gate both response paths before an old request can update a newer account's UI/cache. */
export function assertSessionGeneration(requestGeneration: number | undefined, currentGeneration: number): void {
  if (requestGeneration !== currentGeneration) throw new StaleSessionError();
}

/** No browser/HTTP dependencies: one refresh per logical session, with late-response guards. */
export class SessionCoordinator<T extends RefreshableSession> {
  private state: SessionSnapshot = { generation: 0, accessToken: null };
  private flight: { generation: number; promise: Promise<T> } | null = null;
  snapshot(): SessionSnapshot { return { ...this.state }; }
  replace(accessToken: string | null, userId?: number): number {
    this.state = { generation: this.state.generation + 1, accessToken, userId };
    return this.state.generation;
  }
  isCurrent(generation: number): boolean { return this.state.generation === generation; }
  accept(result: T, generation: number): void {
    if (!this.isCurrent(generation)) throw new StaleSessionError();
    if (this.state.userId !== undefined && result.user.id !== this.state.userId) throw new AccountChangedError();
    this.state = { ...this.state, accessToken: result.accessToken, userId: result.user.id };
  }
  refresh(request: (expectedUserId?: number) => Promise<T>, serialize: (operation: () => Promise<T>) => Promise<T>, applied: (result: T) => void): Promise<T> {
    const snapshot = this.snapshot();
    if (this.flight?.generation === snapshot.generation) return this.flight.promise;
    const pending: { generation: number; promise: Promise<T> } = { generation: snapshot.generation, promise: Promise.resolve(null as unknown as T) };
    pending.promise = serialize(async () => {
      if (!this.isCurrent(snapshot.generation)) throw new StaleSessionError();
      const result = await request(snapshot.userId);
      this.accept(result, snapshot.generation); applied(result); return result;
    }).finally(() => { if (this.flight === pending) this.flight = null; });
    this.flight = pending; return pending.promise;
  }
}

const excluded = new Set(['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout', '/auth/session/logout', '/auth/change-password']);
export function allowsSessionRetry(input: { path?: string; status?: number; retried?: boolean; requestGeneration?: number; currentGeneration: number; hasBearer: boolean }): boolean {
  const path = (input.path ?? '').split('?')[0].replace(/^\/api(?=\/)/, '');
  return input.status === 401 && !excluded.has(path) && !input.retried && input.hasBearer && input.requestGeneration === input.currentGeneration;
}
