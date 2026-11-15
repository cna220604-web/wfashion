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

- **SQL Server:** thiết lập SQL Server Agent/maintenance plan để backup định kỳ database `FashionStore` ra nơi lưu trữ riêng; mã hóa bản backup, giới hạn quyền đọc và giữ bản sao ngoài máy chủ ứng dụng. Theo lịch, khôi phục thử vào database không production và xác minh sản phẩm, tài khoản, đơn hàng trước khi coi backup là dùng được.
- **SQLite:** dùng SQLite Online Backup API hoặc dừng ứng dụng trước khi sao chép database; không sao chép riêng file đang mở khi WAL đang hoạt động. Lưu cả ảnh sản phẩm trong `wwwroot/images/uploads` và ảnh đại diện khách hàng trong `wwwroot/images/avatars`.
- Đánh giá sản phẩm được tạo thêm idempotent khi ứng dụng khởi động, chỉ dành cho sản phẩm thuộc đơn đã giao; mỗi sản phẩm chỉ được đánh giá một lần trong một đơn hàng. Nhân viên và quản trị viên có thể phản hồi/cập nhật phản hồi; khách xem phản hồi trong lịch sử đơn hàng của mình.
- Với cả hai provider, backup ảnh upload cùng thời điểm với database để `ImageId`/`AvatarImageId` không trỏ tới file thiếu. Định kỳ thử khôi phục toàn bộ vào môi trường riêng.
- Không đặt file backup trong `wwwroot` hoặc một thư mục được phục vụ công khai.

## Kiểm tra trước khi phát hành

- Xác nhận tài khoản admin/nhân viên/khách hàng đúng vai trò; các action thay đổi dữ liệu phải tiếp tục có `[Authorize]` theo vai trò và antiforgery.
- Chạy build và kiểm thử các luồng đăng nhập, đặt/hủy đơn, chuyển trạng thái, tồn kho, upload ảnh, email (SMTP sandbox), và chat/fallback AI.
- Không dùng key production trong máy developer hoặc pipeline log; giới hạn quyền của từng key và xoay key khi nghi bị lộ.
- Hiện chưa có project kiểm thử tự động trong repository. Hãy bổ sung test project qua solution đang mở trong Visual Studio trước khi giao việc tiếp theo liên quan kiểm thử.
