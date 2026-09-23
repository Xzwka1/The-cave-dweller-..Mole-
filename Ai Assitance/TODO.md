# 📋 Today's MVP Task Board: The Cave Dweller - Mole!!

> **Target Delivery:** TODAY (Playable Prototype)  
> **Lead Architect & Coder:** Claude Code  
> **Project Manager & MCP Integrator:** Gemini  

---

## 📌 Status Dashboard

| Task # | ระบบ / ฟีเจอร์ | ผู้รับผิดชอบ | สถานะ | หมายเหตุ |
| :---: | :--- | :---: | :---: | :--- |
| **01** | **Player Core Movement** (Walk, Jump, Dash) | Gemini / Claude | **DONE ✅** | `PlayerMovement.cs` ติดตั้งและผูกใน Scene แล้ว |
| **02** | **Camera Controller** (Follow & Scroll Zoom) | Gemini / Claude | **DONE ✅** | `CameraController.cs` ติดตั้งและผูกใน Scene แล้ว |
| **03** | **Player Weapon** (Shotgun 2-round, 360° Mouse Aim) | **Claude** | **DONE ✅** | `PlayerWeapon.cs` + `Projectile.cs` เสร็จ, compile 0 error (MCP 7891) |
| **04** | **Lighting & Darkness System** (Global Light & Flash) | Claude / Gemini | **DONE ✅** | `DynamicMuzzleLight.cs` เสร็จ, compile 0 error |
| **05** | **Enemy 1: Patrol with Flashlight** | **Claude** | **DONE ✅** | `BaseEnemy.cs` + `PatrolEnemyWithLight.cs` เสร็จ, Patrol HP 140 (รอด 1 ชุด) |
| **06** | **Enemy 2: Ambush in the Dark** | **Claude** | **DONE ✅** | `AmbushEnemyDarkness.cs` เสร็จ, proximity 2.5m + ILightDetectable |
| **07** | **Player Health & Combat Feedback** (100 HP, -20 DMG) | **Claude** | **DONE ✅** | `PlayerHealth.cs` เสร็จ, I-frames 0.5s, เรียก GameFlowManager |
| **08** | **Level Loop: Exit Door & Win/Lose State** | Gemini / Claude | **DONE ✅** | `GameFlowManager.cs` + Tag ExitDoor เสร็จ, compile 0 error |

---

## 📝 บันทึกความคืบหน้าล่าสุด (อัปเดต 2026-09-22 — Gemini & Claude Full Integration):
- ✅ Task 01–08 ครบถ้วน 100% — ทั้งฝั่ง C# Core Logic (Claude) และ Scene Integration (Gemini)
- ✅ `Player`: ติดตั้ง `PlayerHealth` (100 HP), `PlayerWeapon` (Shotgun 2 นัด + 360° Mouse Aim), `DynamicMuzzleLight`, `PlayerAmbientLight` (Point Light 2D), ปรับ Ground Layer แก้บั๊กกระโดดไม่ได้
- ✅ `Enemies`:
  - `PatrolEnemy`: ติดตั้ง `PatrolEnemyWithLight` + Flashlight ส่องแสงลาดตระเวนจุด A-B + HP 140
  - `AmbushEnemy`: ติดตั้ง `AmbushEnemyDarkness` + ซุ่มบนแพลตฟอร์มมืด + ปลุกด้วย Proximity/แสง + HP 20
- ✅ `Lighting & Cave Level`:
  - `GlobalLight2D`: ความมืดในถ้ำ (Intensity 0.06)
  - `Level Geometry`: พื้น Floor, กำแพงซ้าย-ขวา, เพดาน, แพลตฟอร์ม 2 ชั้น
  - `ExitDoor`: ประตูทางออกสีทอง + ไฟนำทาง + BoxCollider2D Trigger (Win condition)
- ✅ `GameFlowManager` & UI Canvas:
  - เชื่อมโยง HUD (บอกปุ่มกด), GameOverPanel (แพ้), WinPanel (ชนะ) + ปุ่ม R รีสตาร์ต
  - แก้ EventSystem เป็น `InputSystemUIInputModule` รองรับ New Input System สมบูรณ์
- ✅ `Prefab`: สร้าง `Assets/Prefabs/BulletTracer.prefab` พ่วง LineRenderer + Light2D
- ✅ แก้ไขข้อผิดพลาด Unity Serialization (duplicate groundLayer ใน BaseEnemy/Patrol/Ambush) สำเร็จ
- ✅ Play Mode Tested: คอนโซล 0 error, 0 warning, ระบบแสงไฟฉายและฟิสิกส์ทำงานถูกต้องสมบูรณ์!
- ✅ **Upgraded Bullet System (2026-09-23):** เปลี่ยนจาก Hitscan แช่แสงที่ปลายปืน เป็น **Moving Glowing Pellets** พร้อม `Light2D` (รัศมี 3m, ความเร็ว 30m/s, TrailRenderer) พุ่งแหวกความมืดส่องเปิดพื้นที่ถ้ำ และปลุก `ILightDetectable` (Ambush Enemy) ตามเส้นทางบินจริง!
- ✅ **Enemy Prefabs Created (2026-09-23):**
  - `Assets/Prefabs/Enemy_Light.prefab`: ศัตรูประเภทเดินตรวจการณ์พร้อม Spot Light 2D Flashlight (HP 140, Patrol/Chase/Attack State Machine)
  - `Assets/Prefabs/Enemy_Dark.prefab`: ศัตรูประเภทซุ่มโจมตีในความมืดพร้อม AmbushEyesLight ดวงตาสีแดงเรืองแสง (HP 20, Dormant/Awakened/Lunge State Machine)
- ✅ **MCP Bridge Port:** ซิงก์พอร์ต 7891 รองรับการทำงานร่วมกับ Unity 6000.5.8f1 สมบูรณ์

