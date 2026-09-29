# PLAN.md — Tích hợp Level 19–23 (candidate đã duyệt) vào production

STATUS: DONE (static + build verified; device smoke test pending device reconnection)

## USER APPROVAL APPLIED
- `candidateLevels` ids 19–23 trong `.docs/HabitatShift_CurrentGame_UnityLevels_v1/Habitat_Shift_R9_2_2_level_editor_project.json`
  là nguồn sự thật, đã được người dùng chơi tay và phê duyệt (2026-09-28).
- `approvals` rỗng, `solutionWitnesses` L20 chưa verified, `evidenceStatus/UNKNOWN` không phải gate — chỉ là provenance.
- Không dùng `sourceLevels`, không trộn hai lớp, không thay thế ngầm.
- `approvalMode = user-approved-manual-playtest-2026-09-28-r9-2-2-levels-19-23` (không còn ngữ nghĩa "provisional").

## COLOR POLICY
- Màu của bản đồ đã duyệt (candidate 19–23): blue, cyan, green, orange, pink, purple, red, **white**, yellow.
- **white → ivory** là màu mới bắt buộc (5 habitat, 11 sproutling, 2 queue item).
- **brown không thuộc bản đồ đã duyệt** → không thêm walnut. Ghi nhận provenance: 4 entity brown→green
  (`20_B`, `20t1`, `20t3`, `21_B`), 3 entity brown không còn bản tương ứng (`19_B`, `19t5`, `21t9`),
  48 thao tác `bulk recolor` trong changeLog (L20 08:07–08:13Z, L21 08:43–08:46Z, L22 09:17–09:35Z, L23 09:54–10:02Z).
- Nếu sau này bản đồ duyệt có brown thì phải mở task riêng thêm walnut đủ 8 điểm mapping + asset.

## SHAPE ASSET DELTA
- 27 cặp `(family, shapeKey)` của L19–23 đã có PNG.
- 20 tray PNG thiếu: 16 cho màu cũ (rose/saffron/teal/cobalt/sage/peach) + 4 cho ivory
  (`tray_ivory_0_0-0_1-1_0`, `tray_ivory_0_0-1_0`, `tray_ivory_0_1-1_0-1_1`, `tray_ivory_0_0-1_0-2_0`).
- Sinh **missing-only**; 62 tray cũ phải byte-identical sau khi sinh.

## PORT CLASSIFICATION
- ACTIVE (11): L19 `E19_R8_10`,`E19_R8_11` · L20 `P20_TOP`,`P20_BOTTOM` · L21 `P21_TOP`,`E21_R8_22`,`E21_R81_4`,`E21_R81_1`
  · L22 `P22_TOP`,`P22_BOTTOM` · L23 `P23_TOP`.
- PLACEHOLDER (1): L22 `E22_R8_1` (queue rỗng, trùng edge/entryCell với `P22_TOP`) → không import.
- `direction` chỉ là presentation (chọn sprite `board_elevator_<direction>`); thu thập là omnidirectional,
  `ContinuousSession` không dùng `direction` cho logic.

## CATALOG CHANGES
- Giữ `schema = habitat-shift-levels-v4`, `rulesetVersion = continuous-core-v2`, `rulesetFingerprint` không đổi.
- `catalogRevision = m7-20260928-levels-19-23`; `levelCount = 23`; ids 1..23 liên tục.
- `canonicalLevelsFingerprint = sha256(json.dumps(levels, separators=(',',':'), sort_keys=True))`.
- `CatalogLoader.ExpectedLevelCount = 23` là nguồn duy nhất cho validate/level select/result/audit/tests.
- Ghi byte-identical vào `Assets/StreamingAssets/HabitatShift/approved_levels_v4.json` và `.docs/.../data/approved_levels_v4.json`;
  cập nhật 6 file tài liệu hardcode 18.

## PROTECTED
- L1–18 không đổi (assert semantic + giữ nguyên raw text từng level).
- `ruleset_manifest_v1.json` không đổi; không đổi collection threshold, settle, assist, loader Android.
- Không đụng L24–50; không thêm brown/walnut; không thêm cơ chế gameplay mới.

## KNOWN LIMITATIONS
1. `releaseRule: INITIAL_AND_ON_ENTRY_AVAILABLE`, `blockedBehavior: WAIT_AUTORETRY`, `maxActivePerPort`, `previewSlots`
   không được runtime biểu diễn — L19–23 chạy theo hành vi Elevator hiện tại.
2. L22 `E22_R8_1` bị loại → runtime có 11 Elevator, editor có 12.
3. `sproutling_ivory_v1.png` là recolor từ `sproutling_peach_v1.png`, không phải art render mới.
4. `GameplayFxController` prewarm 8 capture-ghost; assist thu >8 sproutling cùng lúc sẽ thiếu ghost (không crash).
5. `title/profile/difficulty/challenge` sinh tự động theo luật vì editor JSON không có các trường này.
6. Level Select 23 card = 8 hàng → phải cuộn.

## FILES
- Mới: `PLAN.md`, `.docs/LevelTools/convert_r9_levels.py`, `.docs/LevelTools/recolor_sproutling.py`
- Dữ liệu: 2 catalog (StreamingAssets + .docs copy)
- Runtime: `HabitatGameCore.cs`, `HabitatBootstrap.cs`, `HabitatPresentation.cs`, `GameplayFxController.cs`,
  `UiLayoutAudit.cs`, `BoardArtCapture.cs`
- Tests: `GameRulesTests.cs`
- Art: `generate_board_art.py` (+FAMILIES ivory, +`--missing-only`), 20 tray PNG mới, `sproutling_ivory_v1.png`
- Docs: `schema/approved_levels_v4.schema.json`, `README.md`, `validation/SHA256SUMS.txt`,
  `validation/PACKAGE_MANIFEST.json`, `validation/VALIDATION_REPORT.md`, `unity-handoff/LEVEL_CATALOG_FIELD_REFERENCE.md`

## TEST PLAN
- A. Converter: L1–18 không đổi; absolute footprint trước == sau; deterministic; fingerprint tái lập.
- B. Catalog: levelCount 23, ids 1..23, revision/hash đúng, preflight PASS.
- C. Color/art: white→ivory đủ mapping; mọi habitat L19–23 có tray; 62 tray cũ byte-identical.
- D. Runtime smoke: đủ 5 màn L19–L23 trên máy thật (mở màn, kéo/thả, settle, thu sproutling, elevator, không UI audit error).
- E. Regression: core tests không hồi quy; không sửa test settle đang fail.

## ASSUMPTIONS
- Bản đồ đã duyệt = candidate 19–23 trạng thái cuối (10:09Z).
- `.meta` cho PNG mới do Unity tự sinh.
- Build Development APK để smoke test; AAB/release ngoài phạm vi.
- Save cũ vẫn hợp lệ; test nhanh bằng cách tiêm save `highest=23` qua adb.


## FOLLOW-UP FIX 2026-09-29 - RootTrim "ORIGIN CELL BLOCKED"

- Bug: RootTrim thu habitat ve o local (0,0) = o anchor va gia dinh o nay thuoc habitat. 7 habitat dang chu L cua
  L20-L23 (L20 `20_O`; L21 `21_G`, `21_Y`, `H21_R81_1`; L22 `22_G3`; L23 `23_Y`, `23_R`) co goc bounding box rong,
  nen o anchor khong phai o habitat dang chiem -> 4 truong hop bi sproutling khac mau chan (bao "ORIGIN CELL BLOCKED"),
  3 truong hop con lai lam habitat nhay 1 o.
- Fix: them `TrimTarget(h)` = o tren-trai nho nhat dang chiem (`OrderBy(y).ThenBy(x)`) + anchor; `CanUseAssist` kiem tra
  `Legal` tai o do; `UseAssist` doi `h.anchor` theo roi moi thu shape ve `[(0,0)]`.
- Tuong thich nguoc: moi habitat co `(0,0)` duoc chiem (toan bo L1-L18) giu nguyen hanh vi cu.
- Tests: `RootTrim_KeepsAnchorWhenOriginCornerOccupied`, `RootTrim_TrimsToTopLeftOccupiedCell_WhenAnchorCornerIsEmpty`.
- Tai lieu: `unity-handoff/LEVEL_CATALOG_FIELD_REFERENCE.md` (anchor/shape convention + trim rule),
  `validation/VALIDATION_REPORT.md` (runtime follow-up). Catalog 23 man khong doi.
