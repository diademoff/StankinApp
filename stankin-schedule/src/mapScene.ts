import Phaser from 'phaser';

const SPEED = 180;
const JOY_RADIUS = 50;
const NEAR_DIST = 48;
const HALF = 15;

interface RoomPoint {
  x: number;
  y: number;
  label: string;
  marker: Phaser.GameObjects.Arc;
}

export class MapScene extends Phaser.Scene {
  onNear: ((name: string | null) => void) | null = null;

  player!: Phaser.GameObjects.Image;
  private keys!: Record<string, Phaser.Input.Keyboard.Key>;
  private roomPoints: RoomPoint[] = [];
  private triggerLabels: Record<string, string> = {};
  private doorGid = -1;
  private floorLayer: Phaser.Tilemaps.TilemapLayer | null = null;
  private wallLayer: Phaser.Tilemaps.TilemapLayer | null = null;
  private tileSize = 32;
  private boundsW = 0;
  private boundsH = 0;
  private joyOrigin: Phaser.Math.Vector2 | null = null;
  private joyVec = new Phaser.Math.Vector2();
  private joyBase: Phaser.GameObjects.Arc | null = null;
  private joyKnob: Phaser.GameObjects.Arc | null = null;
  private mapKey = 'map';

  constructor() {
    super('map');
  }

  preload() {
    const file = this.registry.get('mapFile') as string;
    this.mapKey = 'map-' + file;
    this.load.tilemapTiledJSON(this.mapKey, file);
    this.load.image('floor-spritesheet', '/maps/floor-spritesheet.png');
    this.load.image('wall-spritesheet', '/maps/wall-spritesheet.png');
    this.load.image('student', '/maps/student.png');
  }

  create() {
    this.onNear = this.registry.get('onNear') ?? null;
    this.triggerLabels = this.registry.get('triggerLabels') ?? {};
    this.roomPoints = [];

    const map = this.make.tilemap({ key: this.mapKey });

    const cacheEntry = this.cache.tilemap.get(this.mapKey) as { data?: any } | undefined;
    const rawByName = new Map(
      (cacheEntry?.data?.tilesets ?? []).map((ts: any) => [ts.name, (ts.image ?? '').replace(/\.png$/i, '')])
    );
    const tilesets = map.tilesets
      .map(ts => map.addTilesetImage(ts.name, rawByName.get(ts.name)))
      .filter(Boolean) as Phaser.Tilemaps.Tileset[];
    if (tilesets.length === 0) return;
    const wallName = [...rawByName.entries()].find(([, k]) => k.includes('wall'))?.[0];
    const floorName = [...rawByName.entries()].find(([, k]) => k.includes('floor'))?.[0];
    const wallTs = tilesets.find(t => t.name === wallName);
    this.floorLayer = floorName && map.getLayer('Floor') ? map.createLayer('Floor', tilesets) : null;
    this.wallLayer = map.getLayer('Wall') ? map.createLayer('Wall', tilesets) : null;
    this.doorGid = wallTs ? wallTs.firstgid + 3 : -1;

    const objLayer = map.getObjectLayer('Trigger');
    for (const obj of objLayer?.objects ?? []) {
      const props = obj.properties as { name: string; value: unknown }[] | undefined;
      const boolProp = props?.find(p => typeof p.value === 'boolean');
      const label = boolProp ? boolProp.name : obj.name;
      if (!label) continue;

      const isTrigger = !!this.triggerLabels[label];
      const color = isTrigger ? 0x14b8a6 : 0x3b82f6;
      const marker = this.add.circle(obj.x, obj.y, isTrigger ? 8 : 7, color, 0.9)
        .setStrokeStyle(2, 0xffffff, 1)
        .setDepth(20);
      marker.setInteractive({
        hitArea: new Phaser.Geom.Circle(0, 0, 20),
        hitAreaCallback: Phaser.Geom.Circle.Contains,
        useHandCursor: true,
      });
      marker.on('pointerdown', () => this.onNear?.(label));

      this.add.text(obj.x, obj.y - 16, isTrigger ? (this.triggerLabels[label] ?? '🚪') : label, {
        fontFamily: 'sans-serif',
        fontSize: '12px',
        fontStyle: 'bold',
        color: '#111827',
        backgroundColor: '#ffffffcc',
        padding: { x: 3, y: 1 },
      }).setOrigin(0.5).setDepth(21);

      this.roomPoints.push({ x: obj.x, y: obj.y, label, marker });
    }

    this.tileSize = map.tileWidth;
    this.boundsW = map.widthInPixels;
    this.boundsH = map.heightInPixels;

    const spawn = (this.registry.get('spawnPoint') as { x: number; y: number } | null) ?? this.floorCenter(map);
    this.player = this.add.image(spawn.x, spawn.y, 'student').setDepth(30);

    this.cameras.main.setBounds(0, 0, this.boundsW, this.boundsH);
    this.cameras.main.startFollow(this.player, true, 0.15, 0.15);
    this.cameras.main.setRoundPixels(true);

    this.keys = this.input.keyboard!.addKeys('W,A,S,D,UP,DOWN,LEFT,RIGHT') as Record<string, Phaser.Input.Keyboard.Key>;

    this.input.on('pointerdown', (p: Phaser.Input.Pointer) => {
      this.joyOrigin = new Phaser.Math.Vector2(p.x, p.y);
      this.joyVec.set(0, 0);
      this.joyBase = this.add.circle(p.x, p.y, JOY_RADIUS, 0xffffff, 0.2)
        .setScrollFactor(0).setDepth(100).setStrokeStyle(2, 0xffffff, 0.5);
      this.joyKnob = this.add.circle(p.x, p.y, 20, 0xffffff, 0.4)
        .setScrollFactor(0).setDepth(101);
    });

    this.input.on('pointermove', (p: Phaser.Input.Pointer) => {
      if (!this.joyOrigin) return;
      const dx = p.x - this.joyOrigin.x;
      const dy = p.y - this.joyOrigin.y;
      const d = Math.hypot(dx, dy);
      const m = Math.min(d, JOY_RADIUS);
      const nx = d === 0 ? 0 : dx / d;
      const ny = d === 0 ? 0 : dy / d;
      this.joyVec.set(nx * (m / JOY_RADIUS), ny * (m / JOY_RADIUS));
      this.joyKnob?.setPosition(this.joyOrigin.x + nx * m, this.joyOrigin.y + ny * m);
    });

    this.input.on('pointerup', () => {
      this.joyOrigin = null;
      this.joyVec.set(0, 0);
      this.joyBase?.destroy();
      this.joyKnob?.destroy();
      this.joyBase = null;
      this.joyKnob = null;
    });

    this.registry.set('player', this.player);
    this.registry.set('scene', this);
    (this.registry.get('onSceneReady') as ((s: MapScene) => void) | null)?.(this);
  }

  update(_time: number, delta: number) {
    const k = this.keys;
    let dx = (k.D.isDown || k.RIGHT.isDown ? 1 : 0) - (k.A.isDown || k.LEFT.isDown ? 1 : 0);
    let dy = (k.S.isDown || k.DOWN.isDown ? 1 : 0) - (k.W.isDown || k.UP.isDown ? 1 : 0);
    dx += this.joyVec.x;
    dy += this.joyVec.y;
    const len = Math.hypot(dx, dy);
    if (len > 1) {
      dx /= len;
      dy /= len;
    }
    const dt = Math.min(delta, 50) / 1000;
    this.tryMove(dx * SPEED * dt, dy * SPEED * dt);
    if (dx < 0) this.player.flipX = true;
    else if (dx > 0) this.player.flipX = false;
    this.checkNearby();
  }

  teleportTo(name: string) {
    const rp = this.roomPoints.find(r => r.label === name);
    if (rp) this.teleportToPoint({ x: rp.x, y: rp.y });
  }

  teleportToPoint(p: { x: number; y: number }) {
    this.player.setPosition(p.x, p.y);
    this.cameras.main.centerOn(p.x, p.y);
    this.checkNearby();
  }

  private tryMove(dx: number, dy: number) {
    const nx = this.player.x + dx;
    if (this.clearAt(nx, this.player.y)) this.player.x = nx;
    const ny = this.player.y + dy;
    if (this.clearAt(this.player.x, ny)) this.player.y = ny;
  }

  private clearAt(x: number, y: number): boolean {
    return !this.blockedAt(x - HALF, y - HALF)
      && !this.blockedAt(x + HALF, y - HALF)
      && !this.blockedAt(x - HALF, y + HALF)
      && !this.blockedAt(x + HALF, y + HALF);
  }

  private blockedAt(x: number, y: number): boolean {
    if (x < HALF || y < HALF || x > this.boundsW - HALF || y > this.boundsH - HALF) return true;
    const tx = Math.floor(x / this.tileSize);
    const ty = Math.floor(y / this.tileSize);
    const wall = this.wallLayer?.getTileAt(tx, ty);
    if (wall && wall.index > 0 && wall.index !== this.doorGid) return true;
    const floor = this.floorLayer?.getTileAt(tx, ty);
    return !floor || floor.index <= 0;
  }

  private checkNearby() {
    let best: string | null = null;
    let bestD = NEAR_DIST;
    for (const rp of this.roomPoints) {
      const d = Phaser.Math.Distance.Between(this.player.x, this.player.y, rp.x, rp.y);
      if (d < bestD) {
        bestD = d;
        best = rp.label;
      }
    }
    this.onNear?.(best);
    for (const rp of this.roomPoints) rp.marker.setScale(rp.label === best ? 1.4 : 1);
  }

  private floorCenter(map: Phaser.Tilemaps.Tilemap): { x: number; y: number } {
    const floorLayer = this.floorLayer;
    if (!floorLayer) return { x: this.boundsW / 2, y: this.boundsH / 2 };

    let minX = map.width, minY = map.height, maxX = 0, maxY = 0;
    floorLayer.forEachTile((t) => {
      if (t.index > 0) {
        if (t.x < minX) minX = t.x;
        if (t.x > maxX) maxX = t.x;
        if (t.y < minY) minY = t.y;
        if (t.y > maxY) maxY = t.y;
      }
    });
    const cx = (minX + maxX) >> 1;
    const cy = (minY + maxY) >> 1;

    let best: Phaser.Tilemaps.Tile | null = null;
    let bestD = Infinity;
    floorLayer.forEachTile((t) => {
      if (t.index <= 0) return;
      const wall = this.wallLayer?.getTileAt(t.x, t.y);
      if (wall && wall.index > 0 && wall.index !== this.doorGid) return;
      const d = (t.x - cx) ** 2 + (t.y - cy) ** 2;
      if (d < bestD) {
        bestD = d;
        best = t;
      }
    });
    if (!best) return { x: cx * map.tileWidth, y: cy * map.tileHeight };
    return { x: best.x * map.tileWidth + map.tileWidth / 2, y: best.y * map.tileHeight + map.tileHeight / 2 };
  }
}
