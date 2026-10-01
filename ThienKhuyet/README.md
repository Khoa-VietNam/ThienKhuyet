# Thiên Khuyết

Thiên Khuyết là game hành động nhập vai 3D xianxia một người chơi, lấy bối cảnh Tây Cốc. Thế giới, nhân vật, vũ khí, hiệu ứng, giao diện và phần lớn âm thanh được dựng bằng mã/procedural trong dự án; không dùng tài sản do AI tạo.

## Yêu cầu

- Unity Editor **6000.6.1f1** (phiên bản được ghi trong `ProjectSettings/ProjectVersion.txt`).
- Mở thư mục chứa `Assets/`, `Packages/` và `ProjectSettings/` bằng Unity Hub. Unity Package Manager lấy các gói đã khai báo trong `Packages/manifest.json`.
- Scene khởi động: `Assets/ThienKhuyet/Scenes/Bootstrap.unity` (đã được thêm vào Build Settings).

## Chạy thử

1. Mở dự án bằng đúng Unity Editor ở trên và chờ Unity import/resolve package.
2. Mở scene `Bootstrap` nếu scene chưa được chọn, sau đó nhấn **Play**.
3. Chọn **Bắt đầu hành trình** ở màn hình tiêu đề. Có thể chọn tiếp tục từ một trong bốn ô lưu nếu đã có dữ liệu.

Thế giới được tạo theo seed; vị trí khởi đầu và tiến trình nhiệm vụ được lưu cùng dữ liệu người chơi. Ô số 0 được dùng cho autosave.

## Điều khiển mặc định

| Thao tác | Bàn phím / chuột | Tay cầm |
|---|---|---|
| Di chuyển / nhìn | WASD / chuột | Cần trái / cần phải |
| Đánh thường / đòn nặng | Chuột trái / chuột phải | Nút Tây / cò phải |
| Đỡ đòn / né / chạy | Q / Ctrl trái / Shift trái | Cò trái / nút Đông / nhấn cần trái |
| Tương tác / nhập định / hồi phục nhanh | E / G / R | Nút Bắc / D-pad xuống / vai trái |
| Khóa mục tiêu | T hoặc nhấn con lăn chuột | Nhấn cần phải |
| Dùng kỹ năng 1–4 | Phím 1–4 | D-pad lên, D-pad phải, vai phải, D-pad trái |
| Mở túi đồ, nhân vật, tu luyện, kỹ năng, nhiệm vụ, bản đồ | I, C, V, K, J, M | — |
| Mở menu tạm dừng / tiếp tục thoại | Tab / Space hoặc Enter | Start / nút Nam |

## Tiến trình truyện hiện có

- Mở đầu tại Tây Cốc; nói chuyện với Trưởng lão Vũ và chọn một trong bốn đạo lộ.
- Luyện tập ở sân võ, bảo vệ đường làng khỏi sói, điều tra Trại Hắc Phong và trở lại báo tin cho thợ săn Lục.
- Nhiệm vụ phụ của y sư Lan yêu cầu ba Thanh Tâm Thảo và có phần thoại giao dược riêng.
- Đột phá tại linh mạch mở các hồi tiếp theo: Phế Tích Vọng Nguyệt, Thạch Vệ, Lang Vương và Tâm Vực Thiên Khuyết.
- Cổng cuối chỉ mở sau khi hoàn tất thử thách và đạt Kim Đan; lựa chọn tại cổng dẫn tới hai kết cục.

Nhiệm vụ, hội thoại và cutscene nằm trong `Assets/ThienKhuyet/Resources/Story/`. Bản địa hóa tiếng Việt và tiếng Anh nằm trong `Assets/ThienKhuyet/Resources/Localization/`.

## Kiểm thử

Các kiểm thử EditMode ở `Assets/ThienKhuyet/Tests/EditMode/` bao gồm parser/điều kiện/effect, hình học và tạo thế giới, cùng việc kiểm tra tham chiếu giữa localization, nhiệm vụ, hội thoại và cutscene. Chạy chúng trong Unity Test Runner: **Window → General → Test Runner → EditMode → Run All**.

Trong môi trường thực hiện thay đổi lần này không có Unity Editor/Unity license để mở Play Mode, chạy Unity Test Runner hoặc tạo player build. Đã kiểm tra cú pháp 93 tệp C# bằng parser cú pháp, biên dịch và chạy riêng các parser C# cho localization/điều kiện/nhiệm vụ/hội thoại/cutscene với Unity API stub, kiểm tra chéo tài nguyên truyện (24 graph hội thoại, 9 nhiệm vụ, 3 cutscene) và kiểm tra `git diff --check`; các lượt kiểm tra tĩnh này không thay thế build hoặc runtime test trong Unity. Hãy chạy Test Runner và build dự án trong Unity 6000.6.1f1 trước khi phát hành.
