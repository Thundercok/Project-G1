# Project G1 — Art Bible
**Style:** Low Poly / Synty Studios · **Setting:** Half-Life Black Mesa Facility
 
---
 
## 1. Visual Identity
 
> "Sterile facility gone wrong. Clean geometry, dirty atmosphere."
 
Low poly không có nghĩa là đơn giản — mỗi mesh phải đọc rõ silhouette từ xa.
Không dùng normal map. Tất cả depth đến từ geometry và flat shading.
 
---
 
## 2. Color Palette
 
| Role | Hex | Dùng cho |
|---|---|---|
| Concrete Grey | `#8A9099` | Tường, sàn, trần |
| Industrial Green | `#4A5E4A` | Accents, pipe, machinery |
| Warning Orange | `#E8722A` | Hazard tape, emergency light |
| Blood Red | `#8B1A1A` | Damage, gore, emergency |
| Facility White | `#D4D8DC` | Clean surfaces, lab areas |
| Deep Shadow | `#1C1F22` | Ambient shadow, ceiling void |
| Neon Teal | `#2ABFBF` | Alien energy, portals, xen elements |
 
**Rule:** Không quá 3 màu trong 1 room. Palette thay đổi theo zone:
- **Lab Zone:** White + Grey dominant
- **Industrial Zone:** Green + Grey dominant  
- **Xen/Alien Zone:** Teal + Deep Shadow dominant
---
 
## 3. Modular Kit — Asset List cho Agent
 
### 3A. Wall Pieces (tất cả 4m × 3m grid)
```
wall_straight_concrete      — tường trơn
wall_straight_panel         — tường có panel ốp
wall_corner_inner           — góc trong 90°
wall_corner_outer           — góc ngoài 90°
wall_window_frame           — tường có cửa sổ reinforced
wall_door_frame             — tường có cửa (không gồm door)
wall_damaged_hole           — tường bị phá, có rubble
wall_pipe_mounted           — tường có pipe horizontal
```
 
### 3B. Floor / Ceiling
```
floor_tile_clean            — sàn gạch lab sạch
floor_tile_cracked          — sàn gạch nứt
floor_grate_metal           — sàn lưới kim loại (walkway)
floor_blood_decal           — decal máu (overlay)
ceiling_tile_clean          — trần lab sạch
ceiling_tile_broken         — trần vỡ, dây điện thõng
ceiling_light_strip         — đèn strip (baked light source)
ceiling_pipe_bundle         — bundle pipe trên trần
```
 
### 3C. Props — Lab
```
prop_computer_terminal      — máy tính desktop cũ
prop_monitor_stack          — chồng monitor CRT
prop_filing_cabinet         — tủ hồ sơ kim loại
prop_lab_table              — bàn lab stainless steel
prop_beaker_rack            — giá ống nghiệm
prop_whiteboard             — bảng trắng có viết
prop_locker_single          — tủ đồ cá nhân
prop_locker_bank_x4         — dãy 4 tủ đồ
prop_gurney                 — cáng bệnh viện
prop_fire_extinguisher      — bình cứu hỏa (interactable)
```
 
### 3D. Props — Industrial
```
prop_pipe_straight_h        — pipe ngang
prop_pipe_straight_v        — pipe dọc
prop_pipe_elbow             — pipe cong 90°
prop_valve_wheel            — van xoay tròn
prop_electrical_box         — hộp điện tường
prop_generator_large        — máy phát điện lớn
prop_barrel_metal           — thùng phuy kim loại
prop_crate_wooden           — thùng gỗ (breakable)
prop_crate_metal            — thùng kim loại
prop_forklift               — xe nâng hàng (static)
prop_catwalk_section        — đoạn sàn catwalk
prop_ladder_section         — đoạn thang leo
```
 
### 3E. Props — Hazard / Story
```
prop_hazmat_suit_hanging    — đồ hazmat treo móc
prop_warning_tape           — dây cảnh báo vàng đen
prop_bloodstain_wall        — vệt máu trên tường
prop_overturned_table       — bàn bị lật
prop_broken_glass_pile      — đống mảnh kính
prop_body_scientist         — xác nhà khoa học (static)
prop_body_soldier           — xác lính HECU (static)
prop_alien_pod              — vỏ kén alien
prop_alien_residue          — vệt chất nhầy alien (decal)
```
 
### 3F. Doors
```
door_sliding_auto           — cửa tự động trượt ngang
door_sliding_damaged        — cửa kẹt 1 nửa
door_heavy_vault            — cửa hầm thép dày
door_emergency_bulkhead     — cửa khẩn cấp đỏ
```
 
### 3G. Lights & FX Objects
```
light_emergency_rotating    — đèn xoay đỏ khẩn cấp
light_strip_flickering      — đèn strip nhấp nháy
light_desk_lamp             — đèn bàn
fx_spark_emitter            — nguồn tia lửa điện (particle anchor)
fx_steam_vent               — lỗ xả hơi nước
fx_alarm_siren              — còi báo động (audio source anchor)
```
 
---
 
## 4. Poly Budget per Asset
 
| Category | Max Tris |
|---|---|
| Wall piece | 200 |
| Floor/Ceiling | 100 |
| Small prop | 300 |
| Medium prop | 600 |
| Large prop | 1200 |
| Door | 400 |
| Character | 800–1200 |
 
**Rule:** Synty style = readable silhouette ưu tiên hơn detail. Nếu detail không đọc được từ 5m → cut nó.
 
---
 
## 5. Material Rules
 
- **Flat shading** toàn bộ — không smooth shading
- **Texture atlas** 1 sheet 512×512 per category (wall, prop, character)
- **Không dùng normal map, specular map** — chỉ albedo
- **Vertex color** cho variation (dirty/clean version của cùng mesh)
- **Emission map** chỉ dùng cho: đèn, alien element, screen glow
---
 
## 6. Zone Layout Guide
 
```
[SPAWN] → Lab Corridor → Control Room → Industrial Hall → 
Alien Breach Zone → [ESCAPE / BOSS]
```
 
**Mỗi zone có signature element:**
- Lab: ánh sáng trắng lạnh, clean walls
- Control Room: màn hình CRT, map displays
- Industrial: pipe dày đặc, steam vents, dim orange
- Alien Breach: tường vỡ, xen residue, ánh sáng teal
---
 
## 7. Prompt Template cho Agent
 
Khi yêu cầu agent làm model, dùng template này:
 
```
Tạo [asset_name] theo style Synty Studios low poly.
- Max [X] triangles
- Flat shading, không normal map
- Color palette: concrete grey #8A9099, industrial green #4A5E4A
- Grid unit: 1m = 1 Unity unit
- Export: FBX, pivot tại base center
- Không bake lighting vào mesh
```
