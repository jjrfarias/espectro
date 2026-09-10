/** Janela fixa simples: limita quantas chamadas passam por segundo, por conexão. */
export class FixedWindowRateLimiter {
  private windowStart: number;
  private count = 0;

  constructor(
    private readonly limit: number,
    private readonly windowMs: number,
    now = Date.now(),
  ) {
    this.windowStart = now;
  }

  tryConsume(now = Date.now()): boolean {
    if (now - this.windowStart >= this.windowMs) {
      this.windowStart = now;
      this.count = 0;
    }
    if (this.count >= this.limit) return false;
    this.count += 1;
    return true;
  }
}
