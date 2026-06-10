import { Room, Client } from "colyseus";

export interface JoinOptions {
  playerName?: string;
}

// Sunucunun tuttuğu oyun durumu (lockstep: sadece input'lar taşınır)
export class RtsRoomState {
  players: Map<string, PlayerState> = new Map();
  started: boolean = false;
  seed: number = 0;
  tick: number = 0;
}

export class PlayerState {
  name: string = "Player";
  team: number = 0;
  ready: boolean = false;
  connected: boolean = true;
}

export class RtsRoom extends Room<RtsRoomState> {
  maxClients = 4;
  private inputBuffer: Map<number, any[]> = new Map(); // tick -> inputs[]

  onCreate(options: any) {
    this.setState(new RtsRoomState());
    this.state.seed = Math.floor(Math.random() * 2147483647);

    // Lockstep input mesajını al — tüm clientlara broadcast et
    this.onMessage("input", (client, data) => {
      const tick: number = data.tick;
      if (!this.inputBuffer.has(tick)) this.inputBuffer.set(tick, []);
      this.inputBuffer.get(tick)!.push({ sessionId: client.sessionId, ...data });

      // Tüm bağlı oyunculardan input geldiyse broadcast et
      const activePlayers = [...this.state.players.values()].filter(p => p.connected).length;
      if (this.inputBuffer.get(tick)!.length >= activePlayers) {
        this.broadcast("tick_inputs", {
          tick,
          inputs: this.inputBuffer.get(tick)!,
        });
        this.inputBuffer.delete(tick);
      }
    });

    // Oyuncu hazır mesajı
    this.onMessage("ready", (client) => {
      const player = this.state.players.get(client.sessionId);
      if (player) player.ready = true;

      const allReady = [...this.state.players.values()].every(p => p.ready);
      if (allReady && this.state.players.size >= 2 && !this.state.started) {
        this.state.started = true;
        this.broadcast("game_start", {
          seed: this.state.seed,
          players: [...this.state.players.entries()].map(([id, p]) => ({
            sessionId: id,
            name: p.name,
            team: p.team,
          })),
        });
      }
    });

    // Checksum doğrulama (desync tespiti)
    this.onMessage("checksum", (client, data) => {
      // Basit: tüm checksumleri topla, farklıysa desync bildir
      (client as any)._lastChecksum = data;
      const checksums = [...this.clients].map(c => (c as any)._lastChecksum?.hash).filter(Boolean);
      if (checksums.length === this.clients.length) {
        const allSame = checksums.every(h => h === checksums[0]);
        if (!allSame) {
          this.broadcast("desync", { tick: data.tick });
        }
      }
    });

    console.log(`[RtsRoom] oda oluşturuldu. seed=${this.state.seed}`);
  }

  onJoin(client: Client, options: JoinOptions) {
    const teamCount = this.state.players.size;
    const player = new PlayerState();
    player.name = options?.playerName ?? `Oyuncu${teamCount + 1}`;
    player.team = teamCount; // 0,1,2,3
    this.state.players.set(client.sessionId, player);
    console.log(`[RtsRoom] katıldı: ${player.name} (team=${player.team})`);

    // Yeni oyuncuya mevcut durumu bildir
    client.send("joined", {
      sessionId: client.sessionId,
      team: player.team,
      seed: this.state.seed,
    });
    // Diğerlerine bildir
    this.broadcast("player_joined", { name: player.name, team: player.team }, { except: client });
  }

  onLeave(client: Client, consented: boolean) {
    const player = this.state.players.get(client.sessionId);
    if (player) {
      player.connected = false;
      console.log(`[RtsRoom] ayrıldı: ${player.name}`);
      this.broadcast("player_left", { sessionId: client.sessionId, team: player.team });
    }
  }

  onDispose() {
    console.log("[RtsRoom] oda kapatıldı.");
  }
}
