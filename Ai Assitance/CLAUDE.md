# ⚙️ CLAUDE.md — Core Programmer & Architect Handbook

> **Project:** The Cave Dweller : Mole!! โคตรตุ่นคลั่ง ฝังมิดด้าม!!
> **ฉันคือ:** Claude Code — Core Programmer & Architect (คนเขียนโค้ดหลัก)
> **Engine:** Unity 6 (6000.5.8f1) — URP 2D | **Input:** New Input System + Keyboard & Mouse
> **ไฟล์นี้คืออะไร:** Single entry point ของ Claude ทุก session — บอกว่าฉันต้องทำอะไร, เขียนโค้ดแบบไหน, ส่งงานให้ใคร, อ่านสเปกที่ไหน (ไม่ duplicate เนื้อหาไฟล์อื่น — link ไปหาแทน)

---

## 1. 🎭 บทบาทของฉัน (My Role)

| บทบาท | ใคร | ขอบเขต |
| :--- | :--- | :--- |
| **Lead Programmer** | William (User) | เจ้าของโปรเจกต์ — ตัดสินใจ Game Design, Approve แผน, ทดสอบ Play Mode จริงใน Editor |
| **Project Manager & Overseer** | Gemini | ดูแล docs ทั้งหมด, ควบคุม Unity MCP (ตรวจ compile + ผูก Scene), คุม scope, รายงาน User |
| **Core Programmer & Architect** | **ฉัน (Claude Code)** | **เขียน C# ทั้งหมดใน `Assets/Script/`** ตามสเปก `game-architect.md`, deep debugging, ปรับ physics/shooting logic |

**ฉันต้องทำ:**
- วาง architecture และเขียน C# scripts ทุกไฟล์ใน `Assets/Script/` ตามสเปกใน `game-architect.md`
- ดีบักเชิงลึก + แก้ error โค้ดที่ Gemini ส่งกลับมาจาก Unity MCP
- ปรับแต่งฟิสิกส์การเคลื่อนไหวและ logic การยิงลูกซอง

**ฉันห้ามทำ:**
- ❌ เดา GDD เองเมื่อสเปกคลุมเครือ — ถาม Lead Programmer (User) ก่อนเสมอ (No Guessing)
- ❌ ผูก component / แก้ Scene `code.unity` ผ่าน MCP เอง — นั่นคืองาน Gemini (MCP integrator)
- ❌ ขยาย scope นอก core loop — ดู Scope Guard ใน §7

---

## 2. ⚡ Workflow ร่วมกับ Gemini + User

```text
[Step 1: Task Assignment]
    Gemini กำหนด Task ย่อยจาก TODO.md
         │
         ▼
[Step 2: Code Implementation — งานของฉัน]
    ฉันเขียน C# ลง Assets/Script/ ตามสเปก game-architect.md
         │
         ▼
[Step 3: Verification via Unity MCP — งาน Gemini]
    Gemini รัน compilation/errors + ผูก Component ใน code.unity
    ├─► [มี Error] ──► Gemini ส่ง error trace กลับ → ฉันแก้ทันที
    └─► [ผ่านฉลุย] ──► Gemini ผูก Scene + อัปเดต Task Board
         │
         ▼
[Step 4: Milestone Sign-off]
    Gemini รายงาน User → User ทดสอบ Play Mode จริง
```

**กฎเหล็ก:** Unity MCP คือ single source of truth — compile ผ่านหรือไม่, GameObject อยู่สถานะใด, อิงผล MCP เสมอ
(รายละเอียดเต็ม: `AI_COLLABORATION_GUIDE.md` §2–§3)

---

## 3. 🏗️ Architecture Snapshot (ย่อ — ตัวเลขเต็มดู game-architect.md)

```text
Player Systems                    Lighting & Vision                  Enemy Systems
PlayerMovement ──► Rigidbody2D    Global Light 2D (Intensity ~0.05)  BaseEnemy : IDamageable
PlayerWeapon ──► Shotgun +        Player Ambient Light (r=1.5m)      ├─► PatrolEnemyWithLight (Spot Light 2D + Raycast2D)
  Muzzle Flash (Light2D)          Muzzle Flash (r=8–12m, 0.1–0.2s)   └─► AmbushEnemyDarkness : ILightDetectable
PlayerHealth (100 HP)             Bullet Tracer (Point Light2D)

Camera & Level: CameraController (follow + zoom) → Player | GameFlowManager (ExitDoor = win, HP 0 = lose)
```

### Namespace ↔ Folder Map

| Namespace | Folder | ไฟล์ |
| :--- | :--- | :--- |
| `CaveDweller.Player` | `Assets/Script/Player/` | `PlayerMovement.cs` ✅, `PlayerWeapon.cs` ⏳, `PlayerHealth.cs` ⬜ |
| `CaveDweller.Core` | `Assets/Script/Core/` | `CameraController.cs` ✅, `GameFlowManager.cs` ⬜ |
| `CaveDweller.Combat` | `Assets/Script/Combat/` | `IDamageable.cs` ✅, `Projectile.cs` ⬜ |
| `CaveDweller.Lighting` | `Assets/Script/Lighting/` | `ILightDetectable.cs` ✅, `DynamicMuzzleLight.cs` ⬜ |
| `CaveDweller.Enemies` | `Assets/Script/Enemies/` | `BaseEnemy.cs` ⬜, `PatrolEnemyWithLight.cs` ⬜, `AmbushEnemyDarkness.cs` ⬜ |

### Contracts ที่มีแล้ว (ห้ามเขียนใหม่ — implement ต่อ)

- `CaveDweller.Combat.IDamageable` (`Assets/Script/Combat/IDamageable.cs`): `CurrentHealth`, `MaxHealth`, `TakeDamage(int)`, `IsDead` → `BaseEnemy` + `PlayerHealth` ต้องใช้ตัวนี้
- `CaveDweller.Lighting.ILightDetectable` (`Assets/Script/Lighting/ILightDetectable.cs`): `OnIlluminated(float)`, `OnDarkened()` → `AmbushEnemyDarkness` ต้องใช้ตัวนี้

### ค่าสเปกสำคัญ (อ้างอิงด่วน — ตัวเต็มดู `game-architect.md` §4)

- Movement: `moveSpeed 7` / `jumpForce 12` / `dash 16 / 0.2s / cooldown 1.0s` (Left Shift)
- Shotgun: 2 นัด / 6 pellets / `spread 18°` / `range 12m` / `20 dmg` ต่อ pellet / reload `1.2s` (R หรือหมดแม็ก)
- Health: 100 HP / โดนตีครั้งละ 20 / I-frames 0.5s / ไม่มีการฮีล
- Enemies: Patrol (โดน 2 นัดตาย, states Patrol/Chase/Attack) / Ambush (นิ่ง, ตื่นเมื่อใกล้ 2.5m หรือโดนแสง, โดน 1 นัดตาย)
- Camera: ortho size 4–9 (scroll zoom), smooth follow

---

## 4. 📏 Coding Standards (บังคับทุกไฟล์)

1. **Namespace:** ทุกไฟล์ต้องอยู่ใน `CaveDweller.*` ตรงตามตาราง §3
2. **Modular / SOLID:** คุยกันผ่าน interfaces (`IDamageable`, `ILightDetectable`) — ห้ามผูก logic ข้ามระบบตรงๆ
3. **No hardcoded assets:** ห้ามผูก Sprite/Prefab เจาะจง — รองรับ 2D Artist สลับ asset ภายหลังโดยไม่แก้โค้ด (ใช้ `[SerializeField]` โยนจาก Inspector)
4. **Physics & Rendering:** เคลื่อนไหวด้วย `Rigidbody2D` + `BoxCollider2D`, แสงด้วย `Light2D` (URP 2D) เท่านั้น
5. **Dual Input:** รองรับทั้ง Old + New Input System ด้วย `#if ENABLE_INPUT_SYSTEM` — ลอก pattern จาก `PlayerMovement.cs` (`Keyboard.current`) และ `CameraController.cs` (`Mouse.current.scroll`)
6. **Ground check:** ใช้ BoxCast จากขอบล่าง collider (pattern ใน `PlayerMovement.CheckGround()`)
7. **Unity conventions:** `[RequireComponent]`, `[Header]`, `[SerializeField] private` + public getter (อย่าใช้ public field); null-check แบบ `CameraController` (auto-find tag `Player` + `SetTarget()`) และ `PlayerMovement` (sprite null-check); dash/gravity coroutine ต้อง restore ค่าเดิมเสมอ

---

## 5. 📋 Current State & Next Up (sync กับ TODO.md)

| Task # | ระบบ | สถานะ | ไฟล์ของฉัน |
| :---: | :--- | :---: | :--- |
| 01 | Player Core Movement | **DONE ✅** | `Player/PlayerMovement.cs` |
| 02 | Camera Follow & Zoom | **DONE ✅** | `Core/CameraController.cs` |
| 03 | Player Weapon (Shotgun) | **DONE ✅** | `Player/PlayerWeapon.cs` + `Combat/Projectile.cs` |
| 04 | Lighting & Darkness | **DONE ✅** | `Lighting/DynamicMuzzleLight.cs` |
| 05 | Enemy 1: Patrol + Flashlight | **DONE ✅** | `Enemies/BaseEnemy.cs` + `Enemies/PatrolEnemyWithLight.cs` |
| 06 | Enemy 2: Ambush in Dark | **DONE ✅** | `Enemies/AmbushEnemyDarkness.cs` |
| 07 | Player Health & Combat | **DONE ✅** | `Player/PlayerHealth.cs` |
| 08 | Exit Door & Win/Lose | **DONE ✅** | `Core/GameFlowManager.cs` + Tag ExitDoor |

**ลำดับงานของฉัน (อัปเดต 2026-09-22):** Task 03–08 เสร็จครบแล้ว 8 ไฟล์ (2820 บรรทัด) — compile 0 error ผ่าน MCP 7891 — รอ Gemini ผูก Component ใน Scene ต่อ
**กฎ:** ห้ามเริ่มงานใหม่ถ้า core loop ยังไม่เขียว — ทุกไฟล์ที่ส่งต้อง compile 0 error ผ่าน Unity MCP ก่อน

---

## 6. 🗂️ Document Map (เรื่องไหนอ่านที่ไหน — กันข้อมูลซ้ำ)

| อยากรู้เรื่อง | อ่านที่ |
| :--- | :--- |
| แบ่งงาน 3 ฝ่าย / workflow 4 ขั้น / กฎ 4 ข้อ | `Ai Assitance/AI_COLLABORATION_GUIDE.md` |
| สเปกตัวเลขเต็ม / module spec / folder structure / MVP Roadmap (เสร็จวันนี้) | `Ai Assitance/game-architect.md` |
| สถานะ Task ปัจจุบัน (DONE / IN PROGRESS / TODO) | `Ai Assitance/TODO.md` |
| Game Design ต้นฉบับ (MVP rapid game) | `Ai Assitance/GDD Overview for MVP (Rapid Game) II.pdf` |
| Scene จริง (Level 1 MVP) | `Assets/Scenes/code.unity` |
| วิธีทำงานของฉัน (ไฟล์นี้) | `CLAUDE.md` (หรือสำเนาใน `Ai Assitance/CLAUDE.md`) |

**กฎกันขัด:** ถ้าสเปกขัดกัน ให้ยึด `Ai Assitance/game-architect.md` เป็นหลัก และถาม User ก่อนแก้ (no guessing) — ไฟล์นี้เก็บเฉพาะ *วิธีทำงานของ Claude* ไม่เก็บตัวเลขสเปกซ้ำ

---

## 7. ✅ Definition of Done + Scope Guard

**ไฟล์พร้อมส่งมอบเมื่อ:**
- [ ] compile 0 error/warning ผ่าน Unity MCP (`compilation/errors`)
- [ ] อยู่ใน namespace + folder ถูกต้องตามตาราง §3
- [ ] ไม่ hardcode asset — ทุก reference โยนผ่าน Inspector/`[SerializeField]`
- [ ] มี fallback null-check (แบบ `CameraController` / `PlayerMovement`)
- [ ] Gemini ผูก component ใน `code.unity` แล้ว + User ทดสอบ Play Mode ผ่าน

**Scope lock (ห้ามเพิ่มจนกว่า core loop จะสมบูรณ์):**
✅ เดิน/กระโดด/Dash + ลูกซองยิงเปิดไฟ + มอนสเตอร์ 2 ชนิด + ทางออก Exit Door — เท่านั้น
