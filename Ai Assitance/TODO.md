# 📋 Today's MVP Task Board: The Cave Dweller - Mole!!

> **Target Delivery:** TODAY (Playable Prototype)  
> **Lead Architect & Coder:** Claude Code  
> **Project Manager & MCP Integrator:** Gemini  
> **Lead Programmer & Director:** William  

---

## 📌 Status Dashboard ภาพรวม

| Task # | ระบบ / ฟีเจอร์ | ผู้รับผิดชอบ | สถานะ | หมายเหตุ |
| :---: | :--- | :---: | :---: | :--- |
| **01** | **Player Core Movement** (Walk, Jump, Dash) | Gemini / Claude | **DONE ✅** | `PlayerMovement.cs` ติดตั้งและผูกใน Scene แล้ว |
| **02** | **Camera Controller** (Follow & Scroll Zoom) | Gemini / Claude | **DONE ✅** | `CameraController.cs` ติดตั้งและผูกใน Scene แล้ว |
| **03** | **Player Weapon** (Shotgun 2-round, 360° Mouse Aim) | **Claude** | **DONE ✅** | `PlayerWeapon.cs` + `Projectile.cs` เสร็จ, compile 0 error |
| **04** | **Lighting & Darkness System** (Global Light & Flash) | Claude / Gemini | **DONE ✅** | `DynamicMuzzleLight.cs` + Spot Light 2D เสร็จ |
| **05** | **Enemy 1: Patrol with Flashlight** | **Claude** | **DONE ✅** | `BaseEnemy.cs` + `PatrolEnemyWithLight.cs` เสร็จ, Patrol HP 140 |
| **06** | **Enemy 2: Ambush in the Dark** | **Claude** | **DONE ✅** | `AmbushEnemyDarkness.cs` เสร็จ, proximity 2.5m + ILightDetectable |
| **07** | **Player Health & Combat Feedback** (100 HP, -20 DMG) | **Claude** | **DONE ✅** | `PlayerHealth.cs` เสร็จ, I-frames 0.5s, เรียก GameFlowManager |
| **08** | **Level Loop: Exit Door & Win/Lose State** | Gemini / Claude | **DONE ✅** | `GameFlowManager.cs` ตาย 3 วิรีสตาร์ต, Win Sequence |
| **09** | **Animation Consolidation & Sprite Scaling** | Gemini | **DONE ✅** | รวม 11 ท่าลง Controller เดียว, ปรับสเกล PPU 1300, แก้ Walk สลับเฟรม |
| **10** | **Map Transition: First_MAP ➔ Winning_Map** | Gemini / Earth | **DONE ✅** | `Teleport_Trigger` (`MapTeleporter`) ใน `First_MAP` ชี้ `Winning_Map` แล้ว (2026-10-04) |
| **11** | **Game VFX: แสงตะเกียง (Lantern Light)** | Gemini | **TODO ⬜** | เพิ่ม Point Light 2D โทนส้มอุ่น + Flicker Effect ให้ตัวละคร/ผนังถ้ำ |
| **12** | **UI HUD: Bullet Icon & เครื่องหมาย Infinity** | Warm / Gemini | **TODO ⬜** | รอไฟล์ภาพไอคอนกระสุน หรือสร้าง UI แสดง ∞ บนหน้าจอ |
| **13** | **UI Text ตอนตาย "You die my boy"** | Gemini | **TODO ⬜** | ปรับ Text ใน GameOverPanel ให้เป็น "You die my boy" |
| **14** | **Sound & SFX จริงจาก PDM** | PDM | **WAITING ⏳** | รอไฟล์เสียงจริง (.wav/.mp3) / ปัจจุบันใช้ระบบเสียงสังเคราะห์ในโค้ด |

---

## 🎯 ตารางเจาะลึก 5 หมวดหมู่ตามเช็กลิสต์ทีม (Team Breakdown)

### 1. UI หน้า MainMenu & HUD [Warm]
- [x] **ปุ่ม Start / Exit:** เสร็จแล้ว (`Main_Menu.unity` เชื่อมปุ่ม Start เข้า First_MAP และปุ่ม Exit ปิดเกม)
- [x] **Player HP BAR:** เสร็จแล้ว (`First_MAP.unity` มีหลอดเลือด + สไปรต์ `HP_100.png` และ `HP_0.png`)
- [ ] **ICON bullet กับเครื่องหมาย Infinity:** ยังไม่ครบ (ขาดไฟล์รูปไอคอนจาก Warm / โค้ดยิงลูกซอง 2 นัดรีโหลดไม่จำกัดพร้อมแล้ว)
- [ ] **icon Text ตอน player dead "You die my boy":** มีภาพ `Player_Text_dead.png` แล้ว แต่ข้อความ Text ยังเขียนว่า "YOU DIED IN THE DARK..." ต้องแก้เป็น "You die my boy"
- [x] **special win wallpaper:** เสร็จแล้ว (`Winning_Map.unity` มีภาพวอลเปเปอร์ออกจากถ้ำแสดงเต็มจอ)

### 2. Sound in game [PDM]
- [x] **เสียง เพลง ตอนชนะและจบเกมออกจากถ้ำ [Earth]:** เสร็จแล้ว 100% (ติดตั้งไฟล์ `Assets/Music_Game/Win_End_Game.mp3` ผูกในฉาก Winning_Map แล้ว)
- [ ] **เสียงปืน:** โค้ด SoundManager มีเสียงสังเคราะห์เล่นได้ทันที (รอไฟล์จริงจาก PDM)
- [ ] **เสียง Monster 2 ตัว:** โค้ด SoundManager มีเสียงสังเคราะห์การโจมตี (รอไฟล์จริงจาก PDM)
- [ ] **เสียง เดิน กระโดด dash:** โค้ด SoundManager มีเสียงสังเคราะห์ฝีเท้าและพุ่ง (รอไฟล์จริงจาก PDM)
- [ ] **เสียง ambient ฉากถ้ำ:** โค้ด SoundManager มีเสียงบรรยากาศถ้ำสังเคราะห์ (รอไฟล์จริงจาก PDM)
- [ ] **เสียง click icon mainmenu:** โค้ด SoundManager มีเสียงคลิกสังเคราะห์ (รอไฟล์จริงจาก PDM)
- [ ] **เสียง BG MainMenu:** ยังไม่มีไฟล์เพลงสำหรับหน้า Main Menu

### 3. Game Coding and Game Visual [William , Earth]
- [x] **ตอน Player ตายให้ขึ้น Text แล้ว 3 วิ กลับด่านแรก เริ่มใหม่ [William]:** เสร็จแล้ว 100% (`GameFlowManager.cs` มีเลขนับถอยหลัง 3..2..1 รีสตาร์ต First_MAP กด R ข้ามได้)
- [ ] **ตอนชนะให้ player ย้ายไปฉากที่มี wallpaper ออกจากถ้ำ + เพลงขึ้น [Earth]:** ฉากชนะพร้อมเพลงเสร็จแล้ว 95% แต่จุดวาร์ป `Teleport_Trigger` ใน `First_MAP` ยังตั้งเป้าไป `Second_Map` ต้องเปลี่ยนเป็น `Winning_Map`

### 4. Game Animation [Achi]
- [x] **player เดิน กระโดด dash (หน้า,หลัง) ถือปืน/ยิง:** เสร็จแล้ว 100% (รวม 11 ท่าเข้า Controller, PPU 1300 ขนาดสมส่วน, แก้คลิป `Walk.anim` ให้สลับเฟรมก้าวขาเดิน)
- [x] **Monster เดิน ยิงหิน โจมตี:** เสร็จแล้ว 100% (Controller มอนสเตอร์มีทั้งท่าเดิน `WolkEnemy`, โจมตี `EnemyAttack`, ยิงหิน `EnemyShoot` ครบ)

### 5. Game VFX
- [ ] **แสงตะเกียง:** ยังไม่ครบ (มีไฟฉายและ Ambient Light แต่ยังขาด Point Light 2D สีส้มอุ่น + แสงไหว Flickering)
- [x] **แสงกระสุน:** เสร็จแล้ว 100% (`DynamicMuzzleLight.cs` แสงวาบปากกระบอกปืน + เม็ดกระสุนมี Light2D และ Trail ส่องสว่างแหวกความมืด)

---

## 🚀 Action Items ที่เตรียมลุยต่อทันที:
1. **แก้จุดวาร์ปใน `First_MAP`:** เปลี่ยน `nextSceneName` จาก `Second_Map` ➔ `Winning_Map`
2. **แก้ข้อความตอนตาย:** เปลี่ยนข้อความใน `GameOverText` ให้เป็น `"You die my boy"`
3. **เพิ่มแสงตะเกียง (Lantern VFX):** ติดตั้ง Point Light 2D สีส้มอุ่น + สคริปต์แสงกระพริบไหว (Flicker) ให้ตัวละคร
4. **ทำ Bullet Icon + เครื่องหมาย ∞:** เสริมไอคอนกระสุนและสัญลักษณ์อินฟินิตี้บน HUD

---

## 📝 บันทึกความคืบหน้าล่าสุด:
- ✅ **Friend's New Walk & Monster Animations Integrated (2026-10-04):**
  - เชื่อมภาพเดินใหม่ของเพื่อน (`Wolk(F)`, `Wolk(B-F)`, `Wolk(B)`) เข้าสู่ `Animator Controller Character.controller` และซิงก์ทั้ง `Wolk.anim` และ `Walk.anim` ให้เล่นแอนิเมชันก้าวขาเดิน 4 จังหวะสมบูรณ์
  - ปรับสเกล `spritePixelsToUnits` ของภาพเดินใหม่ทั้ง 3 รูปจาก 100 เป็น 1300 ป้องกันตัวละครขยายร่างยักษ์ผิดสัดส่วน
  - เชื่อมแอนิเมชันเดินมอนสเตอร์ `EnemyWolk.anim` (`EnemyWolk`, `EnemyWolk2`, `EnemyWolk3`) พร้อมปรับสเกล PPU จาก 100 เป็น 1150
  - สร้างคลิป `idleEnemy.anim` สำหรับมอนสเตอร์ และแก้ Missing Motion ใน `มอนตีใกล้(Animator Controller).controller`
  - อัปเดต `AnimationIntegrator.cs` ให้รองรับคลิปใหม่ทั้งหมดและเพิ่มเมนูช่วยรีสตาร์ต MCP Server
- ✅ **Walk Animation Fixed (2026-09-30):**
  - แก้ไขไฟล์ `Walk.anim` จากเดิมที่เป็นระบบกระดูก 2D Bone Rigging (`bone_1/bone_2`) ซึ่งไม่มีภาพสไปรต์ ให้กลายเป็นระบบ Sprite Frame Swap สลับระหว่างรูป `หันขวา` และ `มองขวา` (Loop 0.6s) ตอนนี้เวลากดเดินตัวละครขยับขาเดินก้าวสลับมุมเรียบร้อย ไม่ยืนแข็งทื่อแล้ว
- ✅ **Cleaned & Consolidated Animation System (2026-09-30):**
  - รวมคลิปแอนิเมชัน Player ทั้งหมด 11 ท่าเข้าสู่ `Animator Controller Character.controller` (Idle, Walk, Jump, Dash, Dash ถอยหลัง, ยิงปกติ, กระโดดยิง, พุ่งยิง, ถอยหลังยิง, พุ่งกระโดดยิง, ถอยหลังกระโดดยิง)
  - ปรับปรุงโค้ด `PlayerMovement.cs` และ `PlayerWeapon.cs` ให้ trigger ท่า Dash และยิงกลางอากาศครบทุกทิศทาง
  - แก้บั๊กตัวละครยักษ์บังมิดจอ: ปรับ `spritePixelsToUnits` จาก 100 เป็น 1300 (ขนาดตัวเหลือ ~1.95m พอดีกับ BoxCollider2D) และตั้งค่า Pivot กึ่งกลางตัว `(0.5, 0.5)` ทุกสไปรต์ ท่าไม่เด้งหลุดตำแหน่ง
  - ลบ Ghost/Dummy Preview GameObjects ในฉาก `First_MAP.unity` ที่เพื่อนลากทิ้งไว้บนฟ้า (y=45) ออกทั้งหมด 11 ตัว
  - กำจัดไฟล์ Controller ขยะ 4 ไฟล์ (`Rapid2(หันขวา)`, `Rapid2(หันขวา)_0 (7)`, `หันขวา`, `หันขวา(ส่วนว่าง)`) และ Orphan Clips ขยะ 4 ไฟล์เกลี้ยงโปรเจกต์
  - โปรเจกต์สะอาด 100% ไม่มี Missing Controller warning และ C# Build 0 errors!
- ✅ **Upgraded Bullet System (2026-09-23):** เปลี่ยนเป็น Moving Glowing Pellets พร้อม `Light2D` (รัศมี 3m, ความเร็ว 30m/s, TrailRenderer) พุ่งส่องเปิดพื้นที่ถ้ำ และปลุก `ILightDetectable` ตามเส้นทางบินจริง
- ✅ **Enemy Prefabs Created (2026-09-23):**
  - `Assets/Prefabs/Enemy_Light.prefab`: ศัตรูประเภทเดินตรวจการณ์พร้อม Spot Light 2D Flashlight (HP 140)
  - `Assets/Prefabs/Enemy_Dark.prefab`: ศัตรูประเภทซุ่มโจมตีในความมืดพร้อม AmbushEyesLight ดวงตาสีแดงเรืองแสง (HP 20)
