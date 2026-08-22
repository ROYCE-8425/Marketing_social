# Ma trận trách nhiệm DX-OS (RACI)

Nguồn: `PhanTich_HeThong_Marketing_DXOS (1).docx` (báo cáo 2.0, 21/08/2026).  
Áp dụng cho **Giai đoạn 1.5 — Lead đến CPL (nội bộ)**. Không phải toàn bộ tầm nhìn (ads live, Zalo 1 chạm, doanh thu).

**Chú giải:** **R** làm · **A** chịu trách nhiệm cuối · **C** được hỏi · **I** được biết · **—** không tham gia · **✗** bị cấm

| Vai trò trên bảng | Header `X-DXOS-Role` |
|---|---|
| Chủ DN | `Owner` |
| Chuyên viên | `Marketer` |
| Nội dung | `Content` |
| Kinh doanh | `Sales` |
| Hệ thống / Elsa | `System` |

---

## 1. Động cơ phê duyệt nhiều lớp

| Việc | Chủ DN | Chuyên viên | Nội dung | Kinh doanh | Hệ thống |
|---|---|---|---|---|---|
| Tạo nháp chiến dịch | I | **A/R** | C | — | I (stub copy) |
| Kiểm tra từ cấm / format (Brand lite) | I | I | C | — | **R** |
| Chuyển duyệt (Draft → PendingReview → PendingApproval) | ✗ | **A/R** | — | — | ✗ |
| Gửi thẳng chủ DN (`send-to-owner`) | ✗ | **A/R** | — | — | ✗ |
| Phê duyệt phát hành nội bộ | **A/R** | ✗ | — | — | **✗ cấm** |
| Từ chối + **bắt buộc lý do** | **A** | **R** | — | — | ✗ |
| Undo 15 phút sau duyệt | **A/R** | I | — | — | I (đếm giờ) |
| Đẩy Facebook / TikTok / Google | — | — | — | — | **không làm GĐ 1.5** |
| Nhật ký ai / lúc nào / lý do | I | I | I | — | **R** |

Poka-yoke: Chủ DN **không** duyệt khi còn Nháp hoặc Chờ chuyên viên. System **không** approve.

---

## 2. Chấm điểm & phân luồng lead

| Việc | Chủ DN | Chuyên viên | Nội dung | Kinh doanh | Hệ thống |
|---|---|---|---|---|---|
| Tiếp nhận form / tin nhắn / gọi | I | I | — | I | **R** |
| Chuẩn hóa SĐT (+84 / 0xxxxxxxxx) | — | — | — | — | **R** |
| Loại trùng SĐT hoặc email | I | I | — | I | **A/R** |
| Chấm 5 yếu tố (hành vi, kênh, chiến dịch, giờ, ý định) | I | C | — | I | **R** |
| Phân loại Nóng 80–100 / Ấm 50–79 / Lạnh 20–49 / Rác 0–19 | I | I | — | I | **R** |
| Round-robin Sales | I | — | — | I | **A/R** |
| Nhận lead (claim) | ✗ | ✗ | ✗ | **A/R** | I (SLA) |
| SLA Nóng 5 phút / Ấm 30 phút / Lạnh không gán Sales | I | I | — | **R** | **A** (thu hồi) |
| Từ chối lead + lý do → chuyển Sales khác | I | I | — | **R** | **A** (route lại) |
| Hàng đợi khi Sales offline (tin chào nội bộ) | I | I | — | I | **R** |
| Đóng đơn / ghi doanh thu | — | — | — | — | **không làm GĐ 1.5** |

---

## 3. Ngân sách & CPL (chưa kéo sàn ads)

| Việc | Chủ DN | Chuyên viên | Nội dung | Kinh doanh | Hệ thống |
|---|---|---|---|---|---|
| Nhập snapshot lưu lượng (impression, click, visit, ₫) | **A** | **R** | — | — | **R** (Elsa ingest) |
| Tính CPL = thực chi ÷ số lead | I | I | — | I | **R** |
| Pacing: ngày hết ngân sách, lead dự kiến | I | C | — | I | **R** |
| Đề xuất chuyển ngân sách (stub, không gọi API ads) | **A** (duyệt/từ chối) | C | — | I | **R** (đề xuất) |
| Tự tắt ads / kéo Facebook | — | — | — | — | **cấm GĐ 1.5** |

`adsLive` luôn `false`.

---

## 4. Brand Guardian (bản nhẹ)

| Việc | Chủ DN | Chuyên viên | Nội dung | Kinh doanh | Hệ thống |
|---|---|---|---|---|---|
| Danh sách từ cấm / mẫu copy | **A** | C | **R** | — | I |
| Chặn copy vi phạm trước khi chuyển duyệt | I | I | C | — | **R** |
| AI tạo hình / Vision / P.A.R.A wiki đầy đủ | — | — | — | — | **sau** |

---

## 5. Đã có trên code (không xây lại)

Chiến dịch Draft→PendingReview→PendingApproval→Published|Rejected · send-to-owner · điểm 80/50/20 (SĐT/email) · SLA 15 phút phẳng · round-robin · traffic snapshot + Elsa · CPL từ spend đã lưu · demo seed · bảng `/` + `/board.html`.

## 6. GĐ 1.5 — Gemini được phép code

1. Lý do từ chối chiến dịch (bắt buộc).  
2. Undo 15 phút sau Owner duyệt (chưa đẩy ads).  
3. Snapshot copy bất biến khi gửi duyệt.  
4. Chấm 5 yếu tố + nhãn Nóng/Ấm/Lạnh/Rác + lý do điểm.  
5. Chuẩn hóa & loại trùng SĐT/email.  
6. SLA 5 phút (nóng) / 30 phút (ấm); lạnh không gán Sales.  
7. Sales từ chối lead + lý do + route lại.  
8. Hàng đợi Sales offline (welcome nội bộ, không Zalo).  
9. Đề xuất pacing stub: System đề xuất, Owner duyệt/từ chối, **không** gọi ads API.  
10. Brand lite: từ cấm trên copy trước submit.

## 7. Cấm Gemini (tầm nhìn, không phải GĐ 1.5)

Đăng Facebook/TikTok/Google · duyệt 1 chạm Zalo/Telegram · Identity/SSO · Brand Guardian Vision · CEO AI / Content AI tạo bài · tự tắt ads · chốt đơn / ROI doanh thu · tick OpenSpec R7.1–7.4.
