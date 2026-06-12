/**
 * LockstepClient — deterministic turn-based command sequencer.
 *
 * SP (LoopbackTransport): ticksPerTurn=1, inputDelay=0 → zero latency.
 * MP (WsTransport):       ticksPerTurn=4, inputDelay=2 → ~266ms at 30Hz.
 *
 * Usage:
 *   1. Human commands: lockstepClient.issue(cmd)   [Selection / HUD]
 *   2. AI commands:    commandBus.issue(cmd)        [direct, deterministic]
 *   3. Each sim tick:  const { stalling, commands } = lockstepClient.tick()
 *                      if (!stalling) {
 *                        bus.advanceTick();
 *                        for (cmd of commands) bus.issue(cmd);
 *                        executor.execute(bus.drain());
 *                      }
 */
import type { Transport, WireCommand } from './Transport';
import type { CommandInput } from '../sim/CommandBus';

export interface LockstepOptions {
  ticksPerTurn: number;
  inputDelay: number;
  myTeamId: number;
}

export const SP_OPTIONS: LockstepOptions = { ticksPerTurn: 1, inputDelay: 0, myTeamId: 0 };

export class LockstepClient {
  private readonly _transport: Transport;
  private readonly _opts: LockstepOptions;

  private _simTick = 0;
  private _pendingInput: WireCommand[] = [];
  private _receivedTurns = new Map<number, CommandInput[]>();

  constructor(transport: Transport, opts: LockstepOptions = SP_OPTIONS) {
    this._transport = transport;
    this._opts = opts;
    transport.onMessage = (msg) => {
      if (msg.type === 'turn') {
        const cmds: CommandInput[] = msg.inputs.flatMap(inp =>
          inp.commands.map(c => c as unknown as CommandInput)
        );
        this._receivedTurns.set(msg.turn, cmds);
      }
    };
  }

  /** Issue a human command (buffered until turn boundary, then sent to server). */
  issue(cmd: CommandInput): void {
    this._pendingInput.push(cmd as unknown as WireCommand);
  }

  /**
   * Advance one sim tick. Returns the commands that should be executed this tick.
   * Returns { stalling: true } if sim must pause waiting for server turn data.
   */
  tick(): { commands: CommandInput[]; stalling: boolean } {
    const { ticksPerTurn, inputDelay } = this._opts;
    const isAtBoundary = this._simTick % ticksPerTurn === 0;
    const currentTurn = Math.floor(this._simTick / ticksPerTurn);

    // At turn boundary: flush buffered input to transport
    if (isAtBoundary) {
      const cmds = this._pendingInput.splice(0);
      this._transport.send({ type: 'turn_input', turn: currentTurn, commands: cmds });
      // LoopbackTransport.send() → calls onMessage synchronously → populates _receivedTurns[turn]
    }

    // Compute which execution turn this tick belongs to
    const execTurn = currentTurn - inputDelay;

    // At turn boundary with a valid execTurn: need server data before advancing
    if (isAtBoundary && execTurn >= 0 && !this._receivedTurns.has(execTurn)) {
      return { stalling: true, commands: [] };
    }

    this._simTick++;

    // Return commands for execution only at turn boundary (first tick of the turn)
    if (isAtBoundary && execTurn >= 0) {
      const commands = this._receivedTurns.get(execTurn) ?? [];
      this._receivedTurns.delete(execTurn);
      return { stalling: false, commands };
    }

    return { stalling: false, commands: [] };
  }

  get simTick(): number { return this._simTick; }
  get isConnected(): boolean { return this._transport.connected; }
}
