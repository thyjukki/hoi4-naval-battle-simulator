// Frida agent for hoi4.exe 1.19.3 (SHA-256 in docs/reverse-engineering/index.md): logs every naval combat hour
// and every surface shot to the Python runner (run_battles.py). Addresses and offsets come from the index; they are
// valid for that build only. Values marked fx5 are the game's fixed point (raw / 100000).
'use strict';

const base = Process.getModuleByName('hoi4.exe').base;

const RVA = {
  gamestate: 0x332f260, // global pointer to the gamestate
  tick: 0x1dd370, // gamestate tick: date += 1 h, hourly update, daily update on day change
  resolveHandle: 0x221f310, // {type, id} handle -> object
  combatUpdate: 0x15c3600, // CNavalCombat::Update (vt+0x78)
  combatEndHour: 0x15c3690, // CNavalCombat::EndHour (vt+0x80): hours++, distance, end check
  shotProfile: 0x1970a90, // BuildShot (shooter, out, weapon, target, ...)
  shotLoop: 0x19740c0, // shot loop (shooterMember, targetMember, weapon, shooterShip, ...)
  applyDamage: 0xc3e0e0, // apply damage (ship, str, org, flag)
  randomFixed: 0x2233db0, // random_fixed (out, file, line): *out = 0..99999
};

const resolveHandle = new NativeFunction(base.add(RVA.resolveHandle), 'pointer', ['pointer']);

function s64(p) { return Number(BigInt.asIntN(64, BigInt(p.readS64().toString()))); }
function argS64(a) { return Number(BigInt.asIntN(64, BigInt(a.toString()))); }

function list(p, off) {
  const arr = p.add(off).readPointer();
  const n = p.add(off + 0xc).readS32();
  const out = [];
  if (arr.isNull() || n <= 0 || n > 100000) return out;
  for (let i = 0; i < n; i++) out.push(arr.add(8 * i).readPointer());
  return out;
}

// Ship (index §2): +0x8 handle type 51, +0xc id, +0x308 max HP, +0x6f8 HP, +0x700 org, +0x708 XP, +0x808/+0x80c manpower.
function shipState(ship) {
  return {
    id: ship.add(0xc).readU32(),
    hp: s64(ship.add(0x6f8)),
    maxHp: s64(ship.add(0x308)),
    org: s64(ship.add(0x700)),
    xp: s64(ship.add(0x708)),
    mp: ship.add(0x808).readS32(),
    maxMp: ship.add(0x80c).readS32(),
  };
}

// Fire-exchange member: +0x8 ship handle {type, id}, +0xe0 state, +0xe4 joining countdown, +0xe8 group,
// +0xf0 + 8 × slot weapon charge (0 heavy, 1 torpedo, 2 light), +0x12c sub reveal timer, +0x130 escape progress.
function memberState(member) {
  const handle = member.add(0x8);
  const ship = resolveHandle(handle);
  const st = ship.isNull() ? { id: handle.add(4).readU32(), gone: true } : shipState(ship);
  st.state = member.add(0xe0).readU32();
  st.joining = member.add(0xe4).readS32();
  st.charge = [s64(member.add(0xf0)), s64(member.add(0xf8)), s64(member.add(0x100))];
  st.reveal = member.add(0x12c).readS32();
  st.escape = s64(member.add(0x130));
  return st;
}

// Combatant (side): +0xe8 groups, +0x148 screening, +0x150 carrier screening, +0x158 positioning, +0x160 joined
// penalty, +0x170 fleet AA. Group: +0x8 members.
function sideState(side) {
  const ships = [];
  for (const group of list(side, 0xe8)) {
    for (const member of list(group, 0x8)) ships.push(memberState(member));
  }
  return {
    screening: s64(side.add(0x148)),
    carrierScreening: s64(side.add(0x150)),
    positioning: s64(side.add(0x158)),
    joinedPenalty: s64(side.add(0x160)),
    fleetAa: s64(side.add(0x170)),
    retreatOrder: side.add(0x140).readU8(),
    ships,
  };
}

let ticks = 0;
let current = null; // the combat being updated: { key, hour, thread, start, rolls, shots, damage }
let shooting = null; // the shot loop in progress

function guard(fn) {
  return function (...a) {
    try { return fn.apply(this, a); } catch (e) { send({ type: 'error', where: fn.name, message: e.stack || String(e) }); }
  };
}

Interceptor.attach(base.add(RVA.tick), {
  onLeave: guard(function tickLeave() {
    ticks++;
    send({ type: 'tick', ticks });
  }),
});

// The start-of-hour state (after the daily update) lets a replay set up each hour exactly as the game had it.
Interceptor.attach(base.add(RVA.combatUpdate), {
  onEnter: guard(function updateEnter(args) {
    const combat = args[0];
    current = {
      key: combat.toString(), hourAtUpdate: combat.add(0x44).readS32(), thread: this.threadId,
      start: [sideState(combat.add(0x28).readPointer()), sideState(combat.add(0x30).readPointer())],
      rolls: [], shots: [], damage: [],
    };
  }),
});

Interceptor.attach(base.add(RVA.combatEndHour), {
  onEnter: guard(function endHourEnter(args) {
    const combat = args[0];
    this.combat = combat;
    this.record = {
      type: 'hour',
      combat: combat.toString(),
      tick: ticks,
      hourAtUpdate: current && current.key === combat.toString() ? current.hourAtUpdate : null,
      hourAtEndHour: combat.add(0x44).readS32(),
      weather: combat.add(0xa8).readS32(),
      darkness: s64(combat.add(0xb8)),
      distance: s64(combat.add(0x118)),
      sides: [sideState(combat.add(0x28).readPointer()), sideState(combat.add(0x30).readPointer())],
      shots: current && current.key === combat.toString() ? current.shots : [],
      damage: current && current.key === combat.toString() ? current.damage : [],
      start: current && current.key === combat.toString() ? current.start : null,
      rolls: current && current.key === combat.toString() ? current.rolls : [],
    };
  }),
  onLeave: guard(function endHourLeave(retval) {
    if (!this.record) return;
    this.record.ended = (retval.toUInt32() & 0xff) !== 0;
    this.record.hourAfter = this.combat.add(0x44).readS32();
    send(this.record);
    current = null;
  }),
});

// Shot loop: one shooter, one target, one weapon slot; it fires while the slot's charge covers the cooldown.
Interceptor.attach(base.add(RVA.shotLoop), {
  onEnter: guard(function shotLoopEnter(args) {
    const weapon = args[2].toInt32();
    const shooterShip = args[3];
    shooting = {
      shooter: shooterShip.isNull() ? null : shooterShip.add(0xc).readU32(),
      target: args[1].add(0x8).add(4).readU32(),
      weapon,
      chargeBefore: s64(args[0].add(0xf0 + 8 * weapon)),
      profile: null,
      shots: [],
    };
  }),
  onLeave: guard(function shotLoopLeave(retval) {
    if (shooting && current) {
      shooting.sank = (retval.toUInt32() & 0xff) !== 0;
      current.shots.push(shooting);
    }
    shooting = null;
  }),
});

// Shot profile struct: +0x00 damage, +0x08 crit chance, +0x10 crit multiplier, +0x18 hit chance, +0x20 piercing,
// +0x28 target armor, +0x30 damage-reduction factor (-1 = none), +0x38 cooldown.
Interceptor.attach(base.add(RVA.shotProfile), {
  onEnter: guard(function profileEnter(args) { this.out = args[1]; }),
  onLeave: guard(function profileLeave() {
    if (!shooting || !this.out) return;
    const o = this.out;
    shooting.profile = {
      damage: s64(o), critChance: s64(o.add(0x8)), critMult: s64(o.add(0x10)), hitChance: s64(o.add(0x18)),
      piercing: s64(o.add(0x20)), armor: s64(o.add(0x28)), reduction: s64(o.add(0x30)), cooldown: s64(o.add(0x38)),
    };
  }),
});

// Every roll of the combat update on its own thread, in call order, by source line (the file argument does not
// point at a terminated file name), with the index of the shot loop it fell in (-1 outside one). Inside a shot loop
// a hit roll (naval_fire_exchange_member.cpp line 0x587) starts a new shot.
Interceptor.attach(base.add(RVA.randomFixed), {
  onEnter(args) {
    if (current === null || this.threadId !== current.thread) return;
    this.out = args[0];
    this.line = args[2].toInt32();
  },
  onLeave: guard(function randomLeave() {
    if (!this.out || current === null) return;
    const roll = { line: this.line, value: s64(this.out) };
    current.rolls.push({ line: roll.line, value: roll.value, loop: shooting === null ? -1 : current.shots.length });
    if (shooting === null) return;
    if (roll.line === 0x587 || shooting.shots.length === 0) shooting.shots.push({ rolls: [], hits: [] });
    shooting.shots[shooting.shots.length - 1].rolls.push(roll);
  }),
});

// Damage applied inside a shot loop belongs to its last shot; outside one (air strikes) it is listed per hour.
Interceptor.attach(base.add(RVA.applyDamage), {
  onEnter: guard(function damageEnter(args) {
    if (current === null) return;
    const ship = args[0];
    this.ship = ship;
    this.hit = { target: ship.add(0xc).readU32(), str: argS64(args[1]), org: argS64(args[2]), flag: args[3].toInt32(), before: shipState(ship) };
  }),
  onLeave: guard(function damageLeave() {
    if (!this.hit) return;
    this.hit.after = shipState(this.ship);
    if (shooting !== null && shooting.shots.length > 0) {
      shooting.shots[shooting.shots.length - 1].hits.push(this.hit);
    } else if (current !== null) {
      current.damage.push(this.hit);
    }
  }),
});

rpc.exports = {
  ping() { return { base: base.toString(), ticks }; },
};

send({ type: 'ready', base: base.toString() });
