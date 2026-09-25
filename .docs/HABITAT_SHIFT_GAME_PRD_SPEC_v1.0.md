# HABITAT SHIFT — GAME PRD & GAMEPLAY SPECIFICATION

**Tài liệu:** PRD + Gameplay Contract + Technical/Visual Handoff  
**Phiên bản:** 1.0 — hợp nhất theo các quyết định trong dự án đến ngày 19/09/2026  
**Sản phẩm:** Habitat Shift (tên làm việc; chưa xác nhận tên thương mại/đăng ký trên Google Play)  
**Nền tảng mục tiêu:** Android / Google Play  
**Trạng thái:** **SPEC BASELINE** — mô tả mục tiêu sản phẩm, *không* xác nhận APK hiện tại đã triển khai hoặc kiểm thử đầy đủ từng yêu cầu.

> **Quy tắc ưu tiên quan trọng:** Gameplay contract **direct manipulation, multi-segment drag** trong tài liệu này thay thế mọi ghi chép cũ mô tả “thả tay rồi tự trượt đến khi gặp vật cản”, “khóa một trục cho toàn gesture”, hoặc “mỗi ô = một move”. Nếu code, solver, level data hay tài liệu cũ mâu thuẫn, phải đối chiếu và sửa theo contract mới; không âm thầm giữ logic cũ.

---

## 1. Product overview

### 1.1. Tầm nhìn

Habitat Shift là game giải đố không gian trên ô lưới: người chơi **chạm và kéo các Habitat đa ô** để thu thập những Sproutling cùng màu. Đường đi cần tính đến hình dạng Habitat, chướng ngại, hướng thu thập (Directional Intake), thứ tự di chuyển và những sinh vật xuất hiện từ feeder. Trải nghiệm ưu tiên cảm giác **cầm và kéo trực tiếp**, dễ đọc trên màn hình điện thoại, không ép người chơi nhìn hoạt ảnh tự trượt sau khi nhấc tay.

### 1.2. Khác biệt cốt lõi

- **Continuous direct drag:** Habitat phản hồi ngay khi ngón tay di chuyển qua các ô hợp lệ.
- **Multi-corner in one gesture:** được đổi hướng phải → lên → trái trong lúc vẫn giữ tay, nếu đường đi hợp lệ.
- **One gesture = one logical move:** toàn bộ đường kéo chỉ tính một move khi release có thay đổi state.
- **Directional Intake:** cùng màu không đủ; sinh vật phải được tiếp cận qua cạnh/hướng intake đang mở.
- **Spatial planning:** hình dạng polyomino, footprint, blocker, feeder và lộ trình thu thập tạo bài toán logic.

### 1.3. Mục tiêu và nguyên tắc sản phẩm

- Game độc lập có visual identity riêng, **không sao chép nhân vật, màn chơi, UI hoặc asset** từ game tham khảo.
- Gameplay-first, trạng thái UI rõ ràng, hiệu ứng hỗ trợ phản hồi nhưng không thay đổi logic.
- Không có nút giả, setting giả, level giả, âm thanh giả; mọi thành phần được hiển thị phải có chức năng thật.
- Thiết kế cho mobile Android; trạng thái tiến trình được lưu, có thể tiếp tục chơi sau khi thoát app.

### 1.4. Phạm vi

**Core game:** Home, Level Select, gameplay board, tutorial/how-to-play, Pause, Win/Result, Undo, Restart, progression, persistent preferences, haptic phù hợp, asset/FX runtime. **Không mặc định bao gồm:** quảng cáo, IAP, leaderboard, tài khoản/cloud sync, live-ops, mode có đồng hồ, hint trả phí hoặc audio chưa tích hợp thật. Các phần ngoài phạm vi cần quyết định riêng.

---

## 2. Thuật ngữ và thực thể

| Khái niệm | Định nghĩa |
|---|---|
| Board | Ma trận ô (có thể có ô không chơi được), hiển thị toàn bộ puzzle. |
| Playable cell | Ô hợp lệ để vật thể có thể chiếm; khác với ô bị cắt khỏi irregular board. |
| Habitat | Vật thể đa ô có màu, footprint, số lượng cần thu thập/capacity và các cạnh intake. |
| Sproutling | Sinh vật/token có màu ở một ô trên board hoặc trong feeder queue. |
| Footprint | Tập offset ô cố định của một Habitat; di chuyển tịnh tiến không tự đổi hình. |
| Directional Intake | Ràng buộc thu thập theo cạnh/hướng tiếp xúc đã mở. |
| Feeder | Nguồn bên ngoài board với hàng đợi sinh vật, xuất hiện theo quy tắc deterministic. |
| Unit transition | Một bước dịch chuyển Habitat đúng 1 ô lưới trong drag. |
| DragSession | Bản ghi tạm trước commit, gồm pre-drag state, path, preview state và pointer state. |
| Committed state | Trạng thái game sau lần thả tay hợp lệ gần nhất hoặc sau Undo/Restart. |
| Logical move | Một drag có thay đổi state và đã commit tại release. |
| Solver | Bộ tìm kiếm lời giải dùng chính transition contract của gameplay. |

---

## 3. Core gameplay — quy tắc authoritative

### 3.1. Mục tiêu màn chơi

Thu thập đúng màu qua intake của các Habitat cho tới khi các yêu cầu của màn chơi được giải quyết. **Điều kiện thắng của baseline engine**: tất cả Habitat bắt buộc hoàn thành/được loại khỏi board, không còn Sproutling bắt buộc chưa giải quyết trên board, và không còn feeder queue bắt buộc chưa xử lý. Bộ dữ liệu level cần nêu rõ thành phần nào là *mandatory*; không đánh dấu Win chỉ vì một Habitat biến mất nếu còn mục tiêu bắt buộc.

### 3.2. Điều khiển kéo thả — LOCKED

1. **Pointer DOWN:** xác định Habitat đang chọn, lưu toàn bộ pre-drag committed state; không tính move và không kích hoạt feeder.
2. **Pointer MOVE:** Habitat bám theo ngón tay bằng **visual offset liên tục**; logic preview tiến qua từng cell hợp lệ khi pointer vượt ngưỡng lưới. Phần offset không cho phép vật thể xuyên ô cấm.
3. **Đổi hướng:** **không khóa một trục cho toàn gesture**. Sau các đoạn thẳng có thể rẽ vuông góc, đi ngược trên cùng trục hoặc chọn hướng khác trong cùng lần giữ tay; từng đoạn phải có đường đi hợp lệ.
4. **Blocker:** khi đường hiện tại bị chặn, không hủy gesture; Habitat dừng ở vị trí hợp lệ gần nhất và người chơi có thể kéo theo hướng khác khi vẫn giữ tay.
5. **Thu thập:** khi bước preview được công nhận hợp lệ, kiểm tra frontier/collision/intake/capacity và cập nhật trạng thái tạm. Không tăng moveCount, không advance feeder theo từng bước.
6. **Pointer UP:** commit toàn bộ preview/path thành **đúng 1 logical move nếu có thay đổi gameplay state**. Nếu không thay đổi, không tăng move.
7. **Pointer CANCEL:** hủy preview, khôi phục committed state trước drag, không tính move và không kích hoạt feeder.
8. **Undo:** hoàn nguyên nguyên tử về trạng thái trước toàn bộ drag, bao gồm vị trí, sinh vật, counter, feeder, move count và các hiệu ứng logic liên quan.

**Người chơi được phép thả tại bất kỳ vị trí hợp lệ trung gian**, không phải kéo đến tận vật cản. Không tự chạy slide sau release. Không bắt buộc thả tay giữa các góc rẽ.

**Chú ý phần giao diện:** snap logic theo ô không đồng nghĩa render nhảy ô. Cần có hai lớp: `continuous visual follower` và `authoritative cell-by-cell preview`. Offset trực quan phải luôn được giới hạn bởi đường đi hợp lệ.

### 3.3. Hình dạng và va chạm

- Mỗi Habitat có tập cell-offsets cố định, ví dụ 1×2, 1×3, 2×2, L, T, zigzag; shape là dữ liệu, không hardcode chỉ một hình.
- Khi tịnh tiến 1 ô, đánh giá **toàn bộ frontier** (ô mới được footprint chiếm), tránh hiện tượng một phần Habitat đi xuyên vật cản.
- Không được vào ngoài board, ô không chơi được, ô blocker, hoặc vị trí đang bị Habitat khác chiếm; ô chứa sinh vật chỉ hợp lệ nếu thu thập đúng quy tắc.
- Không sửa state một phần nếu unit transition thất bại. Các event phát ra theo thứ tự deterministic.
- Cần bảo đảm preview và committed state dùng cùng bộ quy tắc vật lý/logic, không có hai cách tính va chạm khác nhau.

### 3.4. Thu thập, màu và intake

- Habitat chỉ có thể thu thập Sproutling cùng màu.
- Màu đúng nhưng tiếp cận từ **cạnh/hướng đóng** vẫn bị chặn; màu khác cũng bị chặn.
- Mỗi lần thu thập cập nhật remaining/capacity đúng số lượng; không clamp âm hoặc cho thu thập vượt capacity.
- Khi remaining đạt 0, Habitat hoàn thành theo quy tắc level và phát event `HabitatCompleted`; phải xử lý đúng nếu hoàn thành **giữa drag** (không cho đi tiếp bằng vật thể đã biến mất).
- Asset mở/đóng là hình hiển thị trạng thái authoritative; không được lấy trạng thái open/closed từ hiệu ứng hoặc animation.

**Cần xác nhận ở level schema:** active intake là các cạnh cố định trong tọa độ board hay các cạnh thay đổi theo trạng thái; tài liệu trong dự án mô tả hướng được cấu hình trên Habitat nhưng chưa có đủ bằng chứng chốt cơ chế xoay.

### 3.5. Feeder

- Một feeder gồm entry cell và queue có thứ tự rõ ràng; spawn/advance deterministic theo level definition.
- Các unit transition khi người chơi **đang giữ tay không kích hoạt lượt feeder**. Nếu drag commit thành một move hợp lệ, feeder được xử lý theo quy tắc post-move đúng một lượt, không theo số cell di chuyển hoặc số đoạn rẽ.
- Entry bị chiếm cần có quy tắc xử lý xác định và không ghi đè sinh vật/Habitat khác.
- Undo phải phục hồi queue, trạng thái entry và mọi spawn đã xảy ra trong lần move.
- Khi level load, trạng thái feeder khởi tạo phải tái hiện được bằng seed/level data.

**Cần xác nhận trước release:** chi tiết ưu tiên spawn khi nhiều feeder cùng cạnh tranh entry, điều kiện queue bắt buộc trong Win, và cách hiển thị next piece; không được tự thiết kế khác giữa solver và runtime.

### 3.6. Move count, hiệu ứng và thắng/thua

- Chỉ tăng moveCount **một lần sau release hợp lệ**; blocked-at-origin/cancel không tăng.
- Capture, clear, spawn là game events; FX chỉ hiển thị, không trì hoãn/đổi kết quả transition.
- Win được kiểm tra từ state đã giải quyết đầy đủ, không dựa vào kết thúc animation.
- Deadlock/không còn lời giải: UI có thể cung cấp Restart/Undo; việc phát hiện tuyệt đối phụ thuộc độ đầy đủ của solver và budget, không nhầm search-limit với UNSOLVABLE.

---

## 4. Solver, level validator và test parity

### 4.1. Authoritative transition

Engine nên cung cấp cơ chế thuần Kotlin, deterministic tương đương:

```text
startDrag(committedState, habitatId)
applyUnitStep(previewState, direction) -> StepResult
applyDragPath(committedState, habitatId, directions[]) -> PreviewResult
commitDrag(previewState) -> newCommittedState / no-op
cancelDrag() -> preDragState
undo() -> priorCommittedState
```

Tên hàm minh họa; API thực tế có thể khác. **Bắt buộc:** UI, validator, replay và solver chia sẻ chính sách transition, không để solver tiếp tục chỉ tạo các action trượt đường thẳng.

### 4.2. Không gian tìm kiếm

- Một action cost=1 là **toàn bộ một gesture có thể gồm nhiều segment** (không phải một step, không phải chỉ một đoạn thẳng).
- Search phải sinh/tìm các điểm dừng và đường kéo hợp lệ; nếu dùng BFS cần tránh vòng lặp trạng thái và giới hạn search-budget rõ ràng.
- Nếu generator chưa hỗ trợ đầy đủ multi-segment, phải báo **SOLVER PARITY INCOMPLETE**, không tuyên bố độ khó hoặc optimal moves là đúng.
- Canonical state key bao gồm các thuộc tính quyết định gameplay (Habitat, Sproutling, feeder, counters...) nhưng không dùng số move đã đi làm danh tính state nếu không ảnh hưởng tính hợp lệ.
- Kết quả riêng biệt: `SOLVED`, `UNSOLVABLE`, `SEARCH_LIMIT_REACHED` (hoặc tương đương). Không gộp timeout với vô nghiệm.

### 4.3. Level validator

Kiểm tra kích thước board, playable mask, shape hợp lệ/connected, unique IDs, bounds, không overlap, mục tiêu/nguồn màu, intake, capacity, feeder entry và đủ tài nguyên. Level cần có ít nhất một action đầu tiên khi thiết kế yêu cầu. Kiểm tra solver replay đến Win trên **cùng authoritative transition** trước khi phát hành.

### 4.4. Test matrix tối thiểu

| Case | Kết quả mong đợi |
|---|---|
| Kéo 1 ô rồi thả | 1 move |
| Kéo 3 ô rồi thả ở ô giữa đường | 1 move, dừng đúng vị trí |
| Kéo RIGHT → UP → LEFT chưa thả | Một DragSession; commit 1 move |
| Bị chặn rồi đổi hướng khi giữ tay | Không hủy gesture; không xuyên blocker |
| Kéo ngược trên cùng trục | Theo đường đi hợp lệ; state nhất quán |
| Kéo nhưng không đổi state | 0 move; feeder không chạy |
| CANCEL | Hoàn nguyên pre-drag; 0 move |
| Thu thập màu đúng qua intake mở | Capture/counter chính xác |
| Đúng màu, intake đóng | Block, không thu thập |
| Sai màu | Block, không thu thập |
| Hoàn thành giữa drag | Clear chính xác; không đi tiếp bằng Habitat đã clear |
| Feeder sau drag nhiều cell/góc | Advance tối đa một lượt theo hợp đồng post-move |
| Undo sau capture + feeder | Khôi phục toàn bộ pre-drag state |
| Replay lời giải solver trong gameplay engine | Cùng states và Win |
| Solver hết budget | SEARCH_LIMIT, không gán UNSOLVABLE |

**Lưu ý lịch sử:** các giá trị optimal moves của 18 level từng được tính theo **auto-slide đường thẳng** không còn là baseline để chấp nhận game hiện tại. Phải tính lại sau khi solver parity multi-segment hoàn thành.

---

## 5. Level design và progression

### 5.1. Content scope

Baseline dự án từng đặt **18 level** để có bản chơi được. Đây là **content target**, không phải xác nhận tất cả 18 level hiện tại đều đạt chuẩn gameplay mới. Không sử dụng bản đồ sao chép từ game tham khảo; level data cần được tạo và validator kiểm chứng.

### 5.2. Progression đề xuất kế thừa từ plan

| Level | Nội dung chính |
|---|---|
| 01–04 | Học kéo trực tiếp, bắt màu, nhiều sinh vật, hiểu một gesture = một move. |
| 05–07 | Blocker/geometry, phụ thuộc thứ tự, Habitat đa ô. |
| 08–10 | Mở khóa Directional Intake; phân biệt hướng đúng/sai. |
| 11–12 | Feeder và delayed feeder. |
| 13–15 | Board không đều, đường đi dài, L-shape. |
| 16–18 | Nhiều phụ thuộc, kết hợp feeder + intake + multi-corner, thử thách tổng hợp. |

Bảng là **định hướng nội dung**, cần QA lại từng level sau thay đổi contract. Không chấp nhận level nhân bản hình thức hoặc obstacle không tác động đến puzzle. Hệ thống phải lưu best moves của người chơi tách biệt với `optimalMoves` từ solver.

### 5.3. Level schema — dữ liệu cần có

```json
{
  "id": "L08",
  "board": {
    "width": 0,
    "height": 0,
    "playableCells": [],
    "blockers": []
  },
  "habitats": [
    {
      "id": "H1",
      "color": "sage",
      "origin": {"x": 0, "y": 0},
      "shapeOffsets": [],
      "remaining": 0,
      "activeIntakeDirections": []
    }
  ],
  "sproutlings": [],
  "feeders": [],
  "tutorialFlags": [],
  "solverMetadata": {
    "status": "NOT_VALIDATED",
    "optimalMoves": null
  }
}
```

Đây là **schema minh họa cấu trúc**, **không phải một level hợp lệ**; đơn vị tọa độ, intake mapping và cấu trúc feeder phải khớp class/schema của engine hiện tại khi triển khai.

---

## 6. UI/UX specification

### 6.1. Screen flow

```text
Launch → Home → Level Select → Gameplay
                              ├─ Pause → Resume / Restart / Level Select
                              ├─ Win/Result → Next Level / Replay / Level Select
                              └─ How to Play (contextual)
Home / Pause → Settings (chỉ hiển thị setting hoạt động thật)
```

Navigation Back phải xử lý phù hợp theo screen, không làm mất tiến trình ngoài ý muốn. Gameplay có safe insets; nếu bật immersive, chỉ ẩn navigation bar trong gameplay theo thiết kế đã thử, khôi phục khi ra màn khác; cần QA gesture và 3-button navigation.

### 6.2. Gameplay layout và readability

- Board giữ **tỷ lệ grid thực**, không kéo dãn toàn chiều cao tạo ô méo.
- Habitat/sinh vật phải đúng kích thước relative với cell và shape footprint.
- Counter `remaining`, màu và trạng thái intake luôn đủ dễ đọc trong tình huống chơi.
- Feeder queue/next piece phải hiển thị khi mechanic active.
- Bottom controls (Undo/Restart/Pause) không chồng vào navigation bar hoặc board.
- Không bake text/số vào asset; HUD cập nhật từ state thật.
- Hướng intake không được trông như icon “dấu cộng” hoặc điều khiển giả.
- Tutorial phải hướng dẫn kéo liên tục, rẽ nhiều góc khi giữ tay, thả để chốt 1 move, và intake.

### 6.3. Drag feedback

- Vừa chạm: trạng thái chọn rõ nhưng không che board.
- Đang kéo: follower trơn + preview snap, không lag đợi release.
- Hợp lệ/bị chặn: phản hồi đúng vị trí, không làm mất quyền đổi hướng.
- Thu thập: hiệu ứng tại cell/event, counter cập nhật nhất quán; animation không chặn input hay thay logic.
- Invalid: chỉ phản hồi ngắn, không dịch chuyển xuyên blocker.
- Undo: về đúng trạng thái và tránh phát lại FX đã xảy ra.

### 6.4. Accessibility và device

Touch target hợp lý; màu không là tín hiệu duy nhất (shape/counter/intake state); text đủ tương phản, animation có thể giảm cường độ nếu cấu hình thật. Kiểm tra các kích thước điện thoại, tỷ lệ màn hình và trường hợp orientation nếu được hỗ trợ. **Chưa chốt** tablet/landscape là scope release bắt buộc.

---

## 7. Visual design & asset specification

### 7.1. Visual identity đã phát triển

- Chủ đề: **Pocket Habitat Workshop** — sáng, ấm, nature-inspired casual puzzle.
- Board: ivory/cream tactile, stone, botanical props, frame và fence.
- Sproutlings: sinh vật thực vật dễ thương, lớp material mềm, đọc được từ trên xuống.
- Habitat trays: viền rỗng theo footprint polyomino, **không vách ngăn giữa** ở biến thể được chọn; lòng rỗng alpha để board và Sproutling hiển thị bên dưới.
- Feeder/FX: đá, rêu, lá/hoa, lõi phát sáng; có phân cấp độ chi tiết phù hợp kích thước runtime.
- Camera: true top-down / orthographic gameplay, không dùng góc 3/4 hoặc frontal khiến footprint sai.

### 7.2. Naming / palette

6 màu runtime chủ đạo của các tray/feeder: `coral`, `cobalt`, `sage`, `saffron`, `lavender`, `teal`. Trước đây character prompt cũng có `peach`, `moss`; **chưa mặc định hai màu này là playable color bổ sung** cho level hiện tại.

| Family | Quy ước |
|---|---|
| Sproutling | `sproutling_{color}_{open|closed}.png` |
| Habitat tray | `habitat_{shape}_{color}_empty.png` |
| Feeder | `feeder_port_{color}.png` |
| Intake | `intake_{open|closed}_{color}.png` |
| FX | `capture_sage_01..06.png`, `spawn_sage_01..06.png`, `clear_sage_01..06.png` |
| Preview | `placement_valid.png`, `placement_invalid.png` |

### 7.3. Chất lượng asset — gate thực tế

Mọi asset phải là **file độc lập từ nguồn sạch**, không tách từ concept/presentation sheet có nền rồi tuyên bố đạt chỉ nhờ alpha min/max. QA cần: full contour không cắt thiếu, alpha đúng trong vùng cần rỗng, không matte/halo trên nền trắng/đen/xám/checker, không dính hàng/cell khác, sắc nét ở kích thước runtime, khớp camera, shape và style approved; có visual human review trước khi promote.

**Lưu ý về trạng thái:** các lần trước đã từng đánh dấu PASS kỹ thuật nhưng người dùng phát hiện crop/alpha lỗi. Vì vậy trạng thái `APPROVED` trên manifest cũ **không tự động chứng minh** final build đã được nghiệm thu bằng mắt hoặc đã tích hợp đúng; Sprint 6 phải QA asset trên APK thực tế.

---

## 8. Runtime rendering & animation

### 8.1. Thứ tự lớp

1. Board/environment
2. Habitat trays
3. Sproutlings
4. Intake indicators
5. Capture/spawn/clear FX
6. HUD/UI

### 8.2. Anchor và sizing

| Asset | Normalized anchor |
|---|---|
| Sproutling | (0.5, 0.78) — thiên về chân/đáy |
| Habitat, board tile, feeder, intake, FX | (0.5, 0.5) |

Dùng board-cell logical size để scale; **64dp/cell, Sproutling ~72%, tray padding ~6%, FX overscan ~35%** là các giá trị khởi tạo đề xuất trong Sprint 5, **không phải thông số đã được kiểm nghiệm trên mọi thiết bị**. Tránh dùng kích thước PNG raw để quyết định logic footprint.

### 8.3. FX baseline timing

| Sequence | Frames | Duration mỗi frame (ms) | Event |
|---|---|---|---|
| Capture Sage | 6 | 55, 55, 65, 70, 80, 95 | `onSproutlingCaptured` |
| Spawn Sage | 6 | 70, 70, 75, 80, 90, 110 | `onFeederSpawn` |
| Clear Sage | 6 | 65, 65, 70, 75, 85, 100 | `onHabitatCompleted` |

FX cần non-blocking, cancellable. Các frame trong pack v2 trước đây được tạo bằng scale/alpha từ master riêng; **cần kiểm tra motion quality thực tế**, không coi sáu biến thể đó mặc nhiên là sáu keyframe thủ công đạt chuẩn.

---

## 9. Technical architecture (Android)

**Ngôn ngữ / UI:** Kotlin, Jetpack Compose và Canvas/custom rendering cho board. **State:** ViewModel + StateFlow, immutable core state. **Persistence:** DataStore cho tiến trình và preferences thực. **Engine:** Pure Kotlin deterministic, tách khỏi animation. **Solver/Validator:** dùng cùng transition engine. **Animation:** renderer nhận gameplay events không sửa rules.

Gợi ý module/trách nhiệm:

```text
app/
  ui/navigation/
  ui/screens/{home,levels,gameplay,result,settings}/
  ui/rendering/{board,habitat,sproutling,feeder,fx}/
  ui/input/DragSessionController
  viewmodel/GameViewModel
  domain/model/
  domain/engine/
  domain/solver/
  domain/level/
  data/PlayerRepository
  assets/AssetCatalog + AnimationSpecs
```

Đây là **cấu trúc logic mục tiêu**; không được mặc định repo đang có đúng thư mục/tên class trên. Navigation 3 hoặc thư viện adaptive chỉ nên giữ nếu code hiện tại cần và stable; không thêm dependency chỉ để “đẹp architecture”. Audio/BGM là hạng mục cần chức năng thật, nếu chưa build thì ẩn control tương ứng thay vì toggle giả.

### 9.1. Persistence

Lưu unlocked/completed levels, best moves, player settings thật và trạng thái gameplay nếu hỗ trợ resume giữa level. Undo history cần chính xác trong session; khi lưu giữa session phải quyết định có lưu cả history hay chỉ committed state. Không để snapshot sinh state không tương thích schema/engine sau update.

### 9.2. Versioning

Gắn `levelSchemaVersion`, `gameplayContractVersion` và `assetManifestVersion` nếu cần migration; đặc biệt **không dùng metadata optimalMoves từ solver auto-slide cũ** khi contract đã đổi.

---

## 10. Production QA & release acceptance

### 10.1. Definition of Done theo nhóm

| Nhóm | Điều kiện PASS |
|---|---|
| Gameplay | Direct-drag multi-segment hoạt động trên máy thật, không auto-slide, không xuyên blocker; counter, feeder, Undo đúng. |
| Solver | Parity với gameplay, replay thắng; nếu chưa đầy đủ phải nêu giới hạn. |
| Levels | Mỗi level valid, có lời giải, progression có chủ đích, không duplicate puzzle một cách vô nghĩa. |
| Visual | Asset riêng, alpha sạch ở runtime, không crop sai, đúng footprint/top-down, không placeholder. |
| Animation | Đúng event/timing/layer, không block input, không replay lỗi sau Undo. |
| UI | Home/levels/game/pause/result/settings/tutorial điều hướng thật; không dead control. |
| Android | Build/test pass, không crash, safe insets, kiểm tra gesture + 3-button nav. |
| Persistence | Tiến trình/settings phục hồi sau restart; migration không mất dữ liệu. |
| Release | App identity và asset nguyên gốc, store assets/permission/privacy theo chức năng thật. |

### 10.2. Bằng chứng nghiệm thu

Codex cần cung cấp commit/diff, build/test log, footage trên máy hoặc emulator cho drag nhiều góc, capture, feeder, Undo, pause/resume, win và screenshot so sánh visual. Báo cáo mỗi task theo: `STATUS PASS / REWORK / BLOCKED`, file giữ/loại, QA chính, bước tiếp theo.

---

## 11. Trạng thái thực tế và các điểm chưa khóa

### 11.1. Những gì tài liệu này xác nhận

- Product direction: game giải đố Habit Shift độc lập, mechanic màu + Directional Intake + feeders + polyomino.
- **Input contract cuối:** direct manipulation, multi-corner within one gesture, one commit on release.
- Visual direction: Sproutlings, board environment, hollow Habitat trays, botanical feeder/FX.
- Có các ZIP asset và Sprint 5 integration manifest được tạo trong quá trình làm việc; **chưa đồng nghĩa mã Android hiện tại đã tích hợp tất cả**.

### 11.2. Những gì không nên tuyên bố khi chưa kiểm tra repo/APK mới nhất

- Toàn bộ 18 level đã solver-validated theo multi-segment semantics.
- Full parity solver/gameplay, optimal moves chính xác.
- Bản Sprint 4 FX đã được nghiệm thu visual trên runtime; mọi màu đã thống nhất 100% với source master.
- Toàn bộ asset đã được áp vào bản build, animation đúng timing, APK đã sẵn sàng Google Play.
- Settings/audio/haptics/back navigation hoạt động đầy đủ.

### 11.3. Decision log cần xác nhận khi triển khai tiếp

1. Tên thương mại/package cuối và số level release.
2. Exact feeder spawn arbitration và các target mandatory.
3. Intake có cố định theo hướng toàn cục, có thể đổi/rotate, hay do level config quyết định.
4. Chính sách Undo sau drag đã trở về cùng vị trí nhưng có event thu thập trong lúc đi/về — định nghĩa “state changed” cần dựa vào full state chứ không chỉ vị trí cuối.
5. Phạm vi hint, audio, haptics, accessibility, landscape/tablet.
6. Những asset nào đã được người dùng nghiệm thu visual sau đợt rework, thay vì chỉ có QA số học.

---

## 12. Handoff cho Codex — trình tự đề xuất

1. Đọc PRD/spec này và đối chiếu repo Android **trước khi thay code**; ghi nhận mọi contract mismatch.
2. Khóa gameplay engine/direct-drag rồi hoàn tất multi-segment solver parity; test Unit/Undo/Feeder.
3. Validate/rework level theo solver mới, cập nhật metadata optimal moves.
4. Kiểm kê và chỉ nhập asset đúng nguồn, đúng trạng thái QA; audit alpha và shape trong runtime.
5. Tích hợp render layers, pivot, animations/events theo Sprint 5 manifest; không hardcode kích thước PNG làm rules.
6. Chạy Gradle tests + APK trên emulator/máy thật, ghi video kiểm tra visual/gesture; chỉ đánh PASS có bằng chứng.
7. Hoàn thiện navigation, persistence, settings/audio/haptics nếu thuộc scope release; production audit và Google Play readiness.

---

## 13. Tài liệu nguồn trong dự án

- `MOBILE_GAME_FACTORY_MASTER_CONTEXT.md` — quy chuẩn factory, gameplay-first, UI/UX, production audit.
- `plan(1).md` — plan sớm của Android game; chứa một số giả định **đã cũ** (ví dụ tên FluffyDrop) và không override input contract cuối.
- `HABITAT_SHIFT_RUNTIME_INTEGRATION_PACKAGE_v1/03_docs/RUNTIME_INTEGRATION_CONTRACT.md`
- `HABITAT_SHIFT_RUNTIME_INTEGRATION_PACKAGE_v1/01_manifest/{runtime_conventions,animation_manifest,gameplay_event_mapping}.json`
- `HABITAT_SHIFT_APPROVED_EMPTY_TRAYS_v1.zip`
- `HABITAT_SHIFT_APPROVED_BOARD_ASSETS_v1.zip`
- `HABITAT_SHIFT_APPROVED_SPRINT4_INDIVIDUAL_v2.zip`
- Các quyết định và kết quả Codex được trao đổi trong chuỗi dự án Habitat Shift/Fluffy Drop.

**Thứ tự ưu tiên khi mâu thuẫn:** quyết định mới nhất của người dùng về gameplay/visual → PRD này → source-specific manifest đã kiểm tra → implementation hiện tại → các plan/mockup lịch sử. Không làm lại hành vi auto-slide hoặc khóa trục chỉ vì tài liệu cũ còn ghi như vậy.
