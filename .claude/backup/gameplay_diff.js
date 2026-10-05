// Property-level diff for every non-TileMap/BG GameObject between two scenes.
const fs = require('fs');
const decode = s => s && s.replace(/^"|"$/g, '').replace(/\\u([0-9a-fA-F]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)));

function parse(path) {
  const objs = new Map();
  for (const d of fs.readFileSync(path, 'utf8').split(/\n(?=--- !u!)/)) {
    const m = d.match(/^--- !u!(\d+) &(-?\d+)( stripped)?\r?\n(\w+):/);
    if (m) objs.set(m[2], { type: m[4], stripped: !!m[3], body: d });
  }
  return objs;
}
const field = (b, k) => (b.match(new RegExp('\\n  ' + k + ': (.*)')) || [])[1]?.trim();

// Flatten a component body into "key: value" lines (top-level + nested) so we can diff properties
function props(body) {
  const out = new Map();
  const lines = body.split(/\r?\n/).slice(2);
  const stack = [];
  for (const line of lines) {
    const m = line.match(/^( *)(- )?([A-Za-z_][\w.]*): ?(.*)$/);
    if (!m) continue;
    const depth = m[1].length / 2;
    stack.length = depth;
    stack[depth - 1] = m[3];
    const key = stack.filter(Boolean).join('.');
    if (m[4] !== '') out.set(key, m[4]);
  }
  return out;
}

function goTable(objs) {
  const gos = new Map();
  for (const [id, o] of objs) if (o.type === 'GameObject')
    gos.set(id, { id, name: decode(field(o.body, 'm_Name')), comps: [...o.body.matchAll(/component: \{fileID: (-?\d+)\}/g)].map(x => x[1]) });
  return gos;
}
const compLabel = o => o.type === 'MonoBehaviour' ? 'MB:' + ((field(o.body, 'm_EditorClassIdentifier') || '').split('::').pop() || '?') : o.type;

const [, , aPath, bPath] = process.argv;
const A = parse(aPath), B = parse(bPath), GA = goTable(A), GB = goTable(B);
const skip = n => /^TileMap|^ฉากหลัง|^เลเยอร์/.test(n || '');
const IGNORE = /m_ObjectHideFlags|m_CorrespondingSourceObject|m_PrefabInstance|m_PrefabAsset|serializedVersion|m_EditorHideFlags|m_EditorClassIdentifier/;

for (const [id, a] of GA) {
  if (skip(a.name)) continue;
  const b = GB.get(id);
  if (!b) { console.log(`\n### ${a.name}  (ONLY IN EARTH)`); continue; }
  const lines = [];
  for (const c of new Set([...a.comps, ...b.comps])) {
    const oa = A.get(c), ob = B.get(c);
    if (!oa) { lines.push(`  + component only in CURRENT: ${compLabel(ob)}`); continue; }
    if (!ob) { lines.push(`  - component only in EARTH: ${compLabel(oa)}`); continue; }
    if (oa.body === ob.body) continue;
    const pa = props(oa.body), pb = props(ob.body);
    const keys = new Set([...pa.keys(), ...pb.keys()]);
    const d = [];
    for (const k of keys) {
      if (IGNORE.test(k)) continue;
      const va = pa.get(k), vb = pb.get(k);
      if (va !== vb) d.push(`      ${k}: ${va === undefined ? '(none)' : decode(va).slice(0, 70)}  ->  ${vb === undefined ? '(none)' : decode(vb).slice(0, 70)}`);
    }
    if (d.length) lines.push(`  ~ ${compLabel(oa)} (${d.length} props)\n` + d.slice(0, 14).join('\n') + (d.length > 14 ? `\n      ... +${d.length - 14} more` : ''));
  }
  if (lines.length) console.log(`\n### ${a.name}\n` + lines.join('\n'));
}
for (const [id, b] of GB) if (!GA.has(id) && !skip(b.name)) console.log(`\n### ${b.name}  (ONLY IN CURRENT)`);

// Prefab instances
const pi = X => [...X].filter(([, o]) => o.type === 'PrefabInstance').map(([id, o]) => id + ' src=' + (o.body.match(/m_SourcePrefab: \{[^}]*guid: (\w+)/) || [])[1]);
console.log('\nPrefabInstances EARTH:', pi(A));
console.log('PrefabInstances CURRENT:', pi(B));
