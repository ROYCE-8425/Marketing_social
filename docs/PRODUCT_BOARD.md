# DX-OS product board

Authoritative checkboxes live in OpenSpec:

[openspec/changes/lead-to-cpl-operator-ui/tasks.md](../openspec/changes/lead-to-cpl-operator-ui/tasks.md)
[openspec/changes/traffic-ingest-cpl/tasks.md](../openspec/changes/traffic-ingest-cpl/tasks.md)

| Sóng | Việc | Trạng thái |
| :--- | :--- | :--- |
| Đã có | API Lead-đến-CPL (duyệt chiến dịch, form lead, điểm, nhận lead, CPL JSON) | `1a4d3be` |
| Đã có | Bảng điều hành web tại `/` — tiếng Việt, tiền ₫ | `wwwroot/index.html` + [design.md](design.md) |
| Đã có | Ghi nhận lưu lượng qua Elsa Workflow & tính CPL từ thực chi | `POST /campaigns/{id}/traffic`, `GET /dashboard/cpl` |
| Sau | Ads live, SSO, Zalo, wiki, tự tắt ads, doanh thu | OpenSpec mục 7 `[~]` |

Bootstrap R7/R8 stay on [bootstrap-remediation-001](../openspec/changes/bootstrap-remediation-001/tasks.md). Do not tick 7.1–7.4 from product work.

Runtime for the API (when Compose is up): `http://127.0.0.1:8080` (JSON & Web UI).
