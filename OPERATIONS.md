# Vận hành W Studio

## Bí mật và cấu hình

- Không ghi API key, mật khẩu SMTP hoặc mật khẩu cơ sở dữ liệu vào `appsettings*.json`, source control hay log.
- Khi phát triển cục bộ, dùng User Secrets (project đã khai báo `UserSecretsId`):

  ```powershell
  dotnet user-secrets set "Ai:Providers:0:Name" "Gemini main" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:0:Type" "Gemini" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:0:Model" "gemini-3-flash-preview" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:0:ApiKey" "YOUR_GEMINI_API_KEY" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:1:Name" "Groq backup" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:1:Type" "Groq" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:1:Model" "YOUR_GROQ_MODEL_ID" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:1:Endpoint" "https://api.groq.com/openai/v1/chat/completions" --project .\WebApplication1.csproj
  dotnet user-secrets set "Ai:Providers:1:ApiKey" "YOUR_GROQ_API_KEY" --project .\WebApplication1.csproj
  ```

- Production dùng secret store/biến môi trường dạng `Ai__Providers__0__Type`, `Ai__Providers__0__Model`, `Ai__Providers__0__Endpoint`, `Ai__Providers__0__ApiKey`; chỉ `ApiKey` là bí mật. Các mục được thử theo thứ tự số tăng dần. `Type` nhận `Gemini`, `Groq`, `OpenAI` hoặc `OpenAICompatible`; với API tương thích OpenAI, đặt URL đầy đủ của endpoint chat completions. Với Groq, ứng dụng bỏ `response_format` và tắt reasoning output để giữ token cho câu trả lời JSON cuối; đầu ra vẫn được kiểm tra trước khi trả lời. Cấu hình cũ `Ai__ApiKey`/`Ai__Model` vẫn được dùng như một provider Gemini dự phòng cuối danh sách.
- Email thông báo cần cấu hình SMTP qua secret store/biến môi trường: `Email__Smtp__Host`, `Email__Smtp__Port` (mặc định 587), `Email__Smtp__Username`, `Email__Smtp__Password`, `Email__Smtp__From`, `Email__Smtp__EnableSsl` (mặc định true), và `Email__StaffRecipients` (địa chỉ nhân viên, phân tách bằng dấu phẩy hoặc chấm phẩy). Nếu chưa cấu hình, đơn hàng vẫn được lưu; ứng dụng ghi log cảnh báo, khách theo dõi trạng thái trong tài khoản và nhân viên xem đơn trong bàn làm việc. Chức năng quên mật khẩu cũng cần SMTP để gửi liên kết đặt lại; thông báo trên trang luôn giữ chung chung để không tiết lộ email có tài khoản hay không.
- Sau khi đổi secret/config, khởi động lại ứng dụng để nạp cấu hình mới.

## Sao lưu và khôi phục

- W Studio uses one SQLite database for the storefront, customer accounts, admin catalog, staff inventory, orders, reviews, and support chat. For a durable non-demo deployment, back up SQLite with SQLite Online Backup API or stop the app before copying the database; do not copy only the main file while WAL is active. Back up uploaded product images and customer avatars alongside the database.
- Render Free has ephemeral storage and can reset the SQLite database and uploaded files on sleep, restart, or redeploy. This is acceptable only for a demo; do not store important customer data there.
- Đánh giá sản phẩm được tạo thêm idempotent khi ứng dụng khởi động, chỉ dành cho sản phẩm thuộc đơn đã giao; mỗi sản phẩm chỉ được đánh giá một lần trong một đơn hàng. Nhân viên và quản trị viên có thể phản hồi/cập nhật phản hồi; khách xem phản hồi trong lịch sử đơn hàng của mình.
- Định kỳ thử khôi phục database cùng ảnh upload vào môi trường riêng để `ImageId`/`AvatarImageId` không trỏ tới file thiếu.
- Không đặt file backup trong `wwwroot` hoặc một thư mục được phục vụ công khai.

## Kiểm tra trước khi phát hành

- Xác nhận tài khoản admin/nhân viên/khách hàng đúng vai trò; các action thay đổi dữ liệu phải tiếp tục có `[Authorize]` theo vai trò và antiforgery.
- Chạy build và kiểm thử các luồng đăng nhập, đặt/hủy đơn, chuyển trạng thái, tồn kho, upload ảnh, email (SMTP sandbox), và chat/fallback AI.
- Không dùng key production trong máy developer hoặc pipeline log; giới hạn quyền của từng key và xoay key khi nghi bị lộ.
- Hiện chưa có project kiểm thử tự động trong repository. Hãy bổ sung test project qua solution đang mở trong Visual Studio trước khi giao việc tiếp theo liên quan kiểm thử.
