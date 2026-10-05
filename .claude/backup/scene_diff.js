// Compare two Unity scene YAML files by stable fileID.
const fs = require('fs');

function parse(path) {
  const text = fs.readFileSync(path, 'utf8');
  const docs = text.split(/\n(?=--- !u!)/);
  const objs = new Map();
  for (const d of docs) {
    const m = d.match(/^--- !u!(\d+) &(-?\d+)( stripped)?\r?\n(\w+):/);
    if (!m) continue;
    objs.set(m[2], { cls: m[1], type: m[4], stripped: !!m[3], body: d });
  }
  return objs;
}

function field(body, key) {
  const m = body.match(new RegExp('\\n  ' + key + ': (.*)'));
  return m ? m[1].trim() : undefined;
}

function decode(s) {
  if (!s) return s;
  return s.replace(/^"|"$/g, '').replace(/\\u([0-9a-fA-F]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)));
}

function gameObjects(objs) {
  // GameObject fileID -> info, plus transform lookup
  const gos = new Map();
  const tfByGo = new Map();
  for (const [id, o] of objs) {
    if (o.type === 'GameObject') {
      const comps = [...o.body.matchAll(/component: \{fileID: (-?\d+)\}/g)].map(x => x[1]);
      gos.set(id, { id, name: decode(field(o.body, 'm_Name')), active: field(o.body, 'm_IsActive'), tag: field(o.body, 'm_TagString'), comps });
    }
    if (o.type === 'Transform' || o.type === 'RectTransform') {
      const go = (field(o.body, 'm_GameObject') || '').match(/-?\d+/);
      if (go) tfByGo.set(go[0], { id, pos: field(o.body, 'm_LocalPosition'), father: (field(o.body, 'm_Father') || '').match(/-?\d+/)?.[0] });
    }
  }
  for (const [id, g] of gos) {
    g.tf = tfByGo.get(id);
    g.compTypes = g.comps.map(c => {
      const o = objs.get(c);
      if (!o) return '?';
      if (o.type === 'MonoBehaviour') {
        const ident = field(o.body, 'm_EditorClassIdentifier') || '';
        return 'MB:' + (ident.split('::').pop() || field(o.body, 'm_Script'));
      }
      return o.type;
    });
  }
  // full path
  const tfToGo = new Map([...gos.values()].filter(g => g.tf).map(g => [g.tf.id, g]));
  for (const g of gos.values()) {
    const parts = [g.name];
    let cur = g, guard = 0;
    while (cur && cur.tf && cur.tf.father && cur.tf.father !== '0' && guard++ < 50) {
      cur = tfToGo.get(cur.tf.father);
      if (cur) parts.unshift(cur.name);
    }
    g.path = parts.join('/');
  }
  return gos;
}

const [, , aPath, bPath] = process.argv;
const A = parse(aPath), B = parse(bPath);
const GA = gameObjects(A), GB = gameObjects(B);

const onlyA = [...GA.values()].filter(g => !GB.has(g.id));
const onlyB = [...GB.values()].filter(g => !GA.has(g.id));
const changed = [];
for (const [id, a] of GA) {
  const b = GB.get(id);
  if (!b) continue;
  const diffs = [];
  if (a.name !== b.name) diffs.push(`name ${a.name} -> ${b.name}`);
  if (a.active !== b.active) diffs.push(`active ${a.active} -> ${b.active}`);
  if (a.tf && b.tf && a.tf.pos !== b.tf.pos) diffs.push(`pos ${a.tf.pos} -> ${b.tf.pos}`);
  if (a.tf && b.tf && a.tf.father !== b.tf.father) diffs.push(`parent changed`);
  const ca = a.compTypes.join(','), cb = b.compTypes.join(',');
  if (ca !== cb) diffs.push(`comps [${ca}] -> [${cb}]`);
  // component body changes
  for (const c of new Set([...a.comps, ...b.comps])) {
    const oa = A.get(c), ob = B.get(c);
    if (oa && ob && oa.body !== ob.body) diffs.push(`~${oa.type === 'MonoBehaviour' ? 'MB:' + (field(oa.body, 'm_EditorClassIdentifier') || '').split('::').pop() : oa.type} body differs`);
  }
  if (diffs.length) changed.push({ path: a.path, diffs });
}

// Non-GameObject docs (prefab instances etc.)
const otherOnly = (X, Y) => [...X].filter(([id, o]) => !Y.has(id) && o.type !== 'GameObject' && !o.stripped).map(([id, o]) => o.type);
const count = arr => arr.reduce((m, t) => (m[t] = (m[t] || 0) + 1, m), {});

const label = (p) => p.split(/[\\/]/).pop();
console.log(`A = ${label(aPath)}  (${GA.size} GameObjects, ${A.size} docs)`);
console.log(`B = ${label(bPath)}  (${GB.size} GameObjects, ${B.size} docs)`);
console.log(`\n=== Only in A (${onlyA.length}) ===`);
for (const g of onlyA) console.log(`  ${g.path}  [${g.compTypes.join(', ')}]  pos=${g.tf?.pos}`);
console.log(`\n=== Only in B (${onlyB.length}) ===`);
for (const g of onlyB) console.log(`  ${g.path}  [${g.compTypes.join(', ')}]  pos=${g.tf?.pos}`);
console.log(`\n=== Changed (${changed.length}) ===`);
for (const c of changed) console.log(`  ${c.path}\n      ${c.diffs.join('\n      ')}`);
console.log(`\n=== Non-GO docs only in A ===`, count(otherOnly(A, B)));
console.log(`=== Non-GO docs only in B ===`, count(otherOnly(B, A)));
