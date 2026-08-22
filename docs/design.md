# DX-OS — hệ thống thiết kế bảng điều hành

Nguồn: copy token và không khí từ **Học cùng Royce** (`study.trannhuy.online`, repo `ROYCE-8425/hoc-cung-royce`, branch `chore/olp-foss-readiness`).

File gốc không có `DESIGN.md`. Token dưới đây lấy từ:

- `frontend/src/index.css` (`:root` HSL)
- `frontend/tailwind.config.js` (Inter, radius, shadcn)
- Trang live: CTA emerald, lưới nền trắng, logo não tím

Sản phẩm vẫn là **DX-OS** (Lead → CPL). Logo Học cùng Royce được dùng làm mark trên thanh nav vì chủ DN yêu cầu giao diện **thật giống** site đó. Giao diện vận hành: **tiếng Việt**. Tiền: **VND**.

Áp dụng cho `src/DXOS.Api/wwwroot/`.

---

## 1. Không khí

SaaS sáng, sạch, Tailwind-like — không Asana xám, không terminal, không neon cyber.

- Nền mint rất nhạt + **lưới 48px** + halo emerald trên đầu trang.
- CTA **emerald** (`#059669` → `#047857`).
- Phần Elsa / workflow dùng accent **tím** (cùng vai trò “AI” trên site gốc).
- Nút **pill** (bo tròn 9999px), shadow mềm.
- Logo: `wwwroot/logos/hoc-cung-royce-logo.png` + chữ DX-OS.

## 2. Token (copy từ `frontend/src/index.css`)

```yaml
source: "https://study.trannhuy.online/"
sourceRepo: "https://github.com/ROYCE-8425/hoc-cung-royce"
sourceFile: "frontend/src/index.css"

locale: vi-VN
currency: VND
currencyDisplay: "1.500.000 ₫"

# HSL triplets — nguyên văn :root Học cùng Royce
hsl:
  background: "150 30% 99%"      # #f7fcf9 mint-white
  foreground: "150 20% 10%"
  card: "0 0% 100%"
  primary: "152 69% 40%"         # ≈ #20ac6b
  primaryForeground: "0 0% 100%"
  secondary: "210 40% 96.1%"
  muted: "210 40% 96.1%"
  mutedForeground: "215.4 16.3% 46.9%"
  destructive: "0 84.2% 60.2%"
  border: "214.3 31.8% 91.4%"
  ring: "152 69% 40%"
  radius: "0.5rem"

# Hex CTA như nhìn thấy trên landing (Tailwind emerald)
cta:
  primary: "#059669"
  primaryHover: "#047857"
  primaryDeep: "#065f46"

aiAccent:
  violet: "#7c3aed"
  violetSoft: "#f5f3ff"
  usage: "Elsa workflow, PendingApproval, badge kỹ thuật — không dùng cho nút chính"

typography:
  fontFamily: "Inter, system-ui, sans-serif"
  display: { size: 30px, weight: 800, letterSpacing: "-0.04em" }
  heading: { size: 17px, weight: 600 }
  body: { size: 15px, weight: 400, lineHeight: 1.5 }
  label: { size: 12px, weight: 600 }

spacing:
  base: 8px
  scale: [8, 12, 16, 24, 32, 48]

radius:
  sm: "calc(0.5rem - 4px)"
  md: "0.5rem"
  lg: 16px
  pill: 9999px

shadow:
  sm: "0 1px 2px rgba(15,23,42,0.05)"
  card: "0 4px 16px rgba(15,23,42,0.06)"
  cta: "0 8px 20px hsl(152 69% 40% / 0.28)"

utilitiesCopied:
  gradientPrimary: "linear-gradient(135deg, hsl(primary) 0%, hsl(primary / 0.8) 100%)"
  gradientText: "linear-gradient(135deg, hsl(152 69% 40%) 0%, hsl(160 84% 39%) 100%)"
  glass: "background hsl(background / 0.8) + backdrop-filter blur(12px)"
  grid: "48px line grid + radial emerald/violet wash"
```

Nút chính: gradient `#059669` → `#047857`, chữ trắng, pill, cao ≥ 40px.  
Ô nhập: viền `hsl(214.3 31.8% 91.4%)`, focus ring emerald 3px.  
Thẻ: nền trắng 92% opacity, bán kính 16px, shadow mềm.  
Nav: sticky glass.

## 3. Ngôn ngữ & tiền

| Khái niệm API | Hiển thị UI |
|---|---|
| Draft | Nháp |
| PendingReview | Chờ chuyên viên |
| PendingApproval | Chờ chủ doanh nghiệp |
| Published | Đã phát hành (nội bộ) |
| Rejected | Từ chối |
| Marketer | Chuyên viên |
| Owner | Chủ doanh nghiệp |
| Sales | Kinh doanh |
| System | Hệ thống |
| NOT_READY | CHƯA SẴN SÀNG PHÁT HÀNH |
| spend / CPL | Chi phí quảng cáo / Chi phí trên mỗi lead (₫) |
| traffic | Lưu lượng |

Định dạng số: `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })`.

## 4. Bố cục một trang

1. Nav sticky: logo Học cùng Royce + DX-OS + probe API/CSDL.  
2. Banner chưa sẵn sàng phát hành (emerald, không đỏ báo động).  
3. Chọn vai trò vận hành.  
4. Lưới 2 cột: chiến dịch · lưu lượng Elsa.  
5. Lưới 2 cột: lead · CPL/pacing.  
6. Nhật ký thao tác — panel xám nhạt, **không** giả terminal.

## 5. Cấm trên UI này

- USD, `$`, `Ad Spend ($)`.
- Copy marketing tiếng Anh trên nút/nhãn vận hành.
- Tuyên bố ads đang chạy (`adsLive` luôn hiện **Chưa kết nối sàn quảng cáo**).
- Terminal đen, neon cyan, robot “AI console”.
- Đổi tên sản phẩm thành Học cùng Royce (chỉ **mượn** visual + logo mark).
