# PRD Habitat Shift — Hiện trạng sản phẩm v1

## 1. Tầm nhìn sản phẩm

Habitat Shift là game puzzle mobile thư giãn nhưng có chiều sâu không gian. Người chơi kéo trực tiếp các Habitat đa ô trên một board liên tục để thu toàn bộ Sproutling cùng màu. Trải nghiệm phải dễ hiểu khi chạm lần đầu, phản hồi tức thời, không tạo “motion debt”, nhưng vẫn giữ va chạm và hình học level nghiêm ngặt.

Đối tượng chính là người chơi mobile thích puzzle xúc giác, phiên chơi ngắn, hình ảnh Woodland Orthographic và tiến trình tăng dần từ thao tác cơ bản đến Elevator, assist và movement constraints.

## 2. Vòng lặp cốt lõi

1. Chọn level đã mở hoặc Continue theo tiến trình thật.
2. Chạm Habitat và kéo tự do trong board 2D.
3. Thu Sproutling cùng màu khi footprint bao phủ target hợp lệ.
4. Tránh Sproutling khác màu, Habitat khác, obstacle, mask và biên board.
5. Thả tay để settle về anchor nguyên strict-legal.
6. Hoàn tất mọi Habitat, toàn bộ Elevator queue bắt buộc và không còn Sproutling để thắng.
7. Xem celebration, Result, sau đó Next Level, Replay hoặc Level Select.

## 3. Màn hình và điều hướng

- **Splash:** nhận diện Habitat Shift, chuyển ngắn sang Home, không có thao tác giả.
- **Home:** Continue theo `lastSelected/unlocked`, Select Level và Settings.
- **Level Select:** hiển thị current/completed/unlocked/locked và difficulty thật của L1–L18.
- **Gameplay:** HUD Close–Level–Pause, board, Undo/Restart và ba assist.
- **Pause:** Resume, Restart, Settings và thoát level theo navigation hiện hành.
- **Settings:** ba điều khiển thật Music, Sound Effects và Haptics; cập nhật tức thời và persist.
- **Tutorial/Onboarding:** hướng dẫn theo ngữ cảnh cho drag, Elevator, assists và constraints; không thay đổi state gameplay.
- **Result:** chỉ xuất hiện sau Victory Sequence; cung cấp Next, Replay và Level Select.

Back phải giữ session khi luồng yêu cầu quay lại gameplay. Không tạo level unlock, reward, currency, điểm sao hoặc state giả.

## 4. Gameplay và progression

Catalog production gồm 18 level:

- L1–L6: continuous core, footprint và routing tăng dần.
- L7: giới thiệu Elevator.
- L8: mở Nest Bloom.
- L9–L11: queue và routing nhiều nguồn.
- L12: mở Garden Shift, supply bốn hướng.
- L13–L14: củng cố cơ chế kết hợp.
- L15: giới thiệu Horizontal/Vertical Movement Constraint.
- L16: mở Root Trim và kết hợp constraint/Elevator.
- L17: crossed deliveries.
- L18: mastery kết hợp toàn bộ hệ thống hiện hành.

Difficulty production gồm `TUTORIAL`, `EASY`, `NORMAL`, `HARD`, `SUPER_HARD`, `MASTERY`. Geometry và metadata trong catalog là authored truth, không được Unity tự rebalance.

## 5. Assist

- **Nest Bloom:** chọn một Habitat hợp lệ và thu các Sproutling cùng màu theo eligibility/runtime hiện hành.
- **Garden Shift:** chọn Sproutling rồi Habitat tương thích; thu target mà không dịch chuyển Habitat.
- **Root Trim:** chọn Habitat hợp lệ và giảm footprint theo contract; Restart phục hồi authored footprint.

Assist chỉ spend charge khi áp dụng thành công; thất bại/cancel không phát success feedback và không mất lượt. Production dùng unlock/charge persist thật. Debug có thể hiển thị `∞` nhưng không ghi charge/unlock vào persistence và vẫn phải kiểm tra eligibility.

## 6. Feedback, trình bày và chiến thắng

Visual direction hiện tại là **Woodland Orthographic**: nền rừng tối, gỗ walnut/oak, đá/rêu, chữ cream và emerald CTA. Habitat và Sproutling là nội dung động nổi bật trên board. UI giữ touch target tối thiểu 48dp-equivalent, hierarchy rõ và không để frame che entity biên.

Collection, blocked contact, assist, Elevator release, Habitat complete và Level complete phát VFX đúng một lần. Sound Effects, Haptics và VFX độc lập. Pause, Settings, navigation hoặc recomposition không replay event cũ.

Victory Sequence khóa input gameplay nhưng giữ board: celebration 2900 ms, hold 700 ms, rồi mới mở Result (tổng 3600 ms). Khi animation scale bằng 0, bỏ chờ cinematic dài và chỉ giữ confirmation ngắn.

## 7. Persistence

Persist: highest unlocked level, last selected/Continue level, best moves theo level, tutorial/onboarding keys, Music, Sound Effects, Haptics, assist unlock và charges.

Không persist: pointer, active drag, contact manifold, soft pose, settle animation, transient VFX, modal animation hoặc Victory Sequence timestamp.

## 8. Accessibility và hiệu năng

- Player-facing labels, state descriptions và thứ tự screen reader hợp lý; không lộ raw ID/debug enum.
- Text scaling không che board/control; contrast đủ trên nền rừng tối.
- Touch mapping dùng cùng transform với renderer.
- Mục tiêu 60 FPS trên thiết bị mục tiêu; không được chậm dần khi giữ contact hoặc kéo dài.
- Board đông L18 phải giữ phản hồi đổi hướng ở pointer/frame update kế tiếp.

## 9. Trạng thái và giới hạn

PRD này ghi nhận sản phẩm hiện tại, không tự tuyên bố M8/M9 frozen. L1–L18 là phạm vi production được export; L19–L50 và mechanic chưa approved nằm ngoài phạm vi. Unity port phải đạt behavior parity, không chỉ đọc được JSON.
