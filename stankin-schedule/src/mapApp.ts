import Phaser from 'phaser';
import { MapScene } from './mapScene';
import { TELEPORTS, TeleportDest } from './teleports';

const AUTO_DIST = 28;

interface Point {
  x: number;
  y: number;
}

interface RoomGroup {
  floorId: string;
  floorLabel: string;
  rooms: string[];
}

function parseFloor(file: string): Promise<{ names: string[]; points: Record<string, Point> }> {
  return fetch(file)
    .then(r => r.json())
    .then((json: any) => {
      const names: string[] = [];
      const points: Record<string, Point> = {};
      const layer = (json.layers || []).find((l: any) => l.type === 'objectgroup' && l.name === 'Trigger');
      for (const o of layer?.objects ?? []) {
        const props = o.properties ?? [];
        const boolProp = props.find((p: any) => typeof p.value === 'boolean');
        const label = boolProp ? boolProp.name : o.name;
        if (!label) continue;
        names.push(label);
        points[label] = { x: o.x, y: o.y };
      }
      return { names, points };
    });
}

export function mapApp() {
  return {
    floors: [
      { id: 'ksu', label: 'Кафедра КСУ', file: '/maps/ksu.json' },
      { id: '3', label: '3 этаж', file: '/maps/3_floor.json' },
      { id: '4', label: '4 этаж', file: '/maps/4_floor.json' },
    ],
    currentFloor: 0,
    floorData: {} as Record<string, { names: string[]; points: Record<string, Point> }>,
    room: null as string | null,
    trigger: null as any,
    skipTrigger: null as string | null,
    search: '',
    picking: true,
    game: null as Phaser.Game | null,
    scene: null as MapScene | null,

    get mapFile(): string {
      return this.floors[this.currentFloor].file;
    },

    isTriggerName(name: string): boolean {
      return !!TELEPORTS[name] || /^(leave|enter)_/.test(name);
    },

    get locationText(): string {
      const floorLabel = this.floors[this.currentFloor].label;
      if (this.trigger) return `${floorLabel} · ${this.trigger.icon} ${this.trigger.label}`;
      if (this.room) return `${floorLabel} · ${this.room}`;
      return floorLabel;
    },

    get currentFloorData() {
      return this.floorData[this.floors[this.currentFloor].id] ?? { names: [], points: {} };
    },

    get groupedRooms(): RoomGroup[] {
      const q = this.search.trim().toLowerCase();
      return this.floors
        .map(f => ({
          floorId: f.id,
          floorLabel: f.label,
          rooms: (this.floorData[f.id]?.names ?? [])
            .filter(n => !this.isTriggerName(n))
            .filter(r => !q || r.toLowerCase().includes(q)),
        }))
        .filter(g => g.rooms.length > 0);
    },

    isDestAvailable(dest: TeleportDest): boolean {
      if (this.floors.findIndex(f => f.id === dest.floor) < 0) return false;
      return !!this.floorData[dest.floor]?.points[dest.at];
    },

    async init() {
      const out: Record<string, { names: string[]; points: Record<string, Point> }> = {};
      await Promise.all(this.floors.map(async (f) => {
        try {
          out[f.id] = await parseFloor(f.file);
        } catch (e) {
          console.error('parseFloor', f.id, e);
          out[f.id] = { names: [], points: {} };
        }
      }));
      this.floorData = out;
    },

    onNear(label: string | null) {
      if (this.skipTrigger && label !== this.skipTrigger) this.skipTrigger = null;

      if (label && TELEPORTS[label]) {
        if (label === this.skipTrigger) return;
        const trig = TELEPORTS[label];
        if (trig.to.length === 1) {
          if (this.distTo(label) < AUTO_DIST) this.go(trig.to[0]);
          return;
        }
        if (trig.to.length > 1 && this.trigger !== trig) {
          this.trigger = trig;
        }
      } else {
        if (this.trigger !== null) this.trigger = null;
        this.room = label;
      }
    },

    distTo(label: string): number {
      const pt = this.currentFloorData.points[label];
      const p = this.scene?.player;
      if (!pt || !p) return Infinity;
      return Math.hypot(p.x - pt.x, p.y - pt.y);
    },

    openPicker() {
      this.picking = true;
    },

    closePicker() {
      if (this.game) this.picking = false;
    },

    pickRoom(floorId: string, room: string) {
      this.go({ floor: floorId, at: room, label: room });
      this.picking = false;
      this.search = '';
    },

    randomSpawn() {
      if (!this.game) this.createGame();
      this.skipTrigger = null;
      this.game!.registry.set('spawnPoint', null);
      this.picking = false;
      this.search = '';
    },

    createGame() {
      const el = this.$refs.canvas as HTMLElement | undefined;
      if (!el || this.game) return;

      this.game = new Phaser.Game({
        type: Phaser.AUTO,
        parent: el,
        transparent: true,
        scale: { mode: Phaser.Scale.RESIZE },
        scene: [MapScene],
      });

      (window as any).__game = this.game;
      this.game.registry.set('mapFile', this.mapFile);
      this.game.registry.set('onNear', (label: string | null) => this.onNear(label));
      const triggerLabels: Record<string, string> = Object.fromEntries(
        Object.entries(TELEPORTS).map(([k, v]) => [k, v.icon])
      );
      for (const f of this.floors) {
        for (const n of this.floorData[f.id]?.names ?? []) {
          if (/^(leave|enter)_/.test(n) && !triggerLabels[n]) triggerLabels[n] = '🚪';
        }
      }
      this.game.registry.set('triggerLabels', triggerLabels);
      this.game.registry.set('onSceneReady', (s: MapScene) => {
        this.scene = s;
      });
    },

    go(dest: TeleportDest) {
      const floorIdx = this.floors.findIndex(f => f.id === dest.floor);
      const point = this.floorData[dest.floor]?.points[dest.at];
      if (floorIdx < 0 || !point) return;

      const prevFloor = this.currentFloor;
      this.currentFloor = floorIdx;
      this.skipTrigger = TELEPORTS[dest.at] ? dest.at : null;

      if (!this.game) {
        this.createGame();
      } else if (floorIdx !== prevFloor) {
        this.game.registry.set('mapFile', this.mapFile);
        this.game.registry.set('spawnPoint', point);
        this.game.scene.scenes[0].scene.restart();
      } else {
        this.scene?.teleportToPoint(point);
      }

      this.game.registry.set('mapFile', this.mapFile);
      this.game.registry.set('spawnPoint', point);
      this.trigger = null;
      this.room = null;
    },
  };
}
