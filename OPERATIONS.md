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

- W Studio uses one configured database for the storefront, customer accounts, admin catalog, staff inventory, orders, reviews, fashion-video metadata, and support chat. Local development uses SQLite; Render uses PostgreSQL. Back up the database through the provider's supported backup/export tools. Back up uploaded product images, customer avatars, and `videos/fashion` separately.
- Render Free web-service files are ephemeral, so uploaded media can be lost on redeploy/restart. PostgreSQL data is external to the web service; its availability, retention, limits, and backups depend on the selected database provider and plan. Do not rely on an unbacked-up free tier for important customer data.
- The first PostgreSQL deployment creates a new schema but does not automatically import data from an existing SQLite file. Export/import records before switching if there is data to preserve; never point the app to an unverified database and assume accounts have been migrated.
- Đánh giá sản phẩm được tạo thêm idempotent khi ứng dụng khởi động, chỉ dành cho sản phẩm thuộc đơn đã giao; mỗi sản phẩm chỉ được đánh giá một lần trong một đơn hàng. Nhân viên và quản trị viên có thể phản hồi/cập nhật phản hồi; khách xem phản hồi trong lịch sử đơn hàng của mình.
- Định kỳ thử khôi phục database cùng ảnh upload vào môi trường riêng để `ImageId`/`AvatarImageId` không trỏ tới file thiếu.
- Không đặt file backup trong `wwwroot` hoặc một thư mục được phục vụ công khai.

## Lưu video trên object storage

- Video thời trang có thể lưu trong bucket S3-compatible riêng tư thay vì ổ đĩa tạm của Render. Tạo bucket và API token có quyền đọc/ghi object tại nhà cung cấp (ví dụ Cloudflare R2), sau đó đặt các biến môi trường Render: `Storage__S3__Endpoint` (HTTPS endpoint), `Storage__S3__Bucket`, `Storage__S3__AccessKey`, và `Storage__S3__SecretKey`. Với R2, endpoint thường là `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`, vùng ký mặc định là `auto` (`Storage__S3__Region`). Không bật public access cho bucket; ứng dụng stream video qua endpoint có kiểm tra tên file và hỗ trợ HTTP Range để tua video. `render.yaml` khai báo bốn biến này để nhập dưới dạng secrets/config trong Render.
- Nếu thiếu bất kỳ biến bắt buộc nào, ứng dụng dùng local storage; nếu chỉ cấu hình một phần, ứng dụng dừng khởi động để tránh lưu file nhầm nơi. Sau khi cấu hình đủ, deploy lại rồi kiểm tra log `Durable object storage is enabled`. Video mới tải lên sẽ vào bucket. Video cũ chỉ được giữ nếu tệp local vẫn còn trên máy chủ cũ; nếu đã mất, cần tải bản gốc lên lại.
- Cấu hình này hiện áp dụng cho video fashion. Ảnh sản phẩm, ảnh đại diện và video yêu cầu trả hàng vẫn dùng local storage và có thể mất khi Render thay máy chủ.

## Kiểm tra trước khi phát hành

- Xác nhận tài khoản admin/nhân viên/khách hàng đúng vai trò; các action thay đổi dữ liệu phải tiếp tục có `[Authorize]` theo vai trò và antiforgery.
- Chạy build và kiểm thử các luồng đăng nhập, đặt/hủy đơn, chuyển trạng thái, tồn kho, upload ảnh, email (SMTP sandbox), và chat/fallback AI.
- Không dùng key production trong máy developer hoặc pipeline log; giới hạn quyền của từng key và xoay key khi nghi bị lộ.
- Hiện chưa có project kiểm thử tự động trong repository. Hãy bổ sung test project qua solution đang mở trong Visual Studio trước khi giao việc tiếp theo liên quan kiểm thử.
