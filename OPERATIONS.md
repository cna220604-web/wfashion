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
- Đăng nhập Google/Facebook dùng `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, `Authentication__Facebook__AppId`, `Authentication__Facebook__AppSecret`. Chỉ cấu hình đủ từng cặp; trang đăng nhập hiển thị trạng thái chưa cấu hình cho provider còn thiếu. Đăng ký callback URL HTTPS trên Google/Facebook đúng domain: `/signin-google` và `/signin-facebook`. Khóa chỉ đặt trong User Secrets/secret store, không commit vào source.
- Đăng nhập xã hội mới chỉ tạo tài khoản Customer. Tài khoản hiện hữu chỉ tự liên kết khi Google xác nhận email và tài khoản W Studio đã xác nhận email; không tự nâng quyền theo thông tin từ nhà cung cấp. Facebook không được dùng để tự liên kết tài khoản email đã có.
- Sau khi đổi secret/config, khởi động lại ứng dụng để nạp cấu hình mới.

## Sao lưu và khôi phục

- W Studio uses one configured database for the storefront, customer accounts, admin catalog, staff inventory, orders, reviews, fashion-video metadata, and support chat. Local development uses SQLite; Render uses PostgreSQL. Back up the database through the provider's supported backup/export tools. Back up uploaded product images, customer avatars, and `videos/fashion` separately.
- Render Free web-service files are ephemeral, so uploaded media can be lost on redeploy/restart. W Studio currently enables `Storage__AllowLocalVideoStorage=true` in the Render blueprint at the user's request; uploaded fashion videos can therefore be lost when the service restarts or deploys. Configure S3-compatible storage for durable video retention. PostgreSQL data is external to the web service; its availability, retention, limits, and backups depend on the selected database provider and plan.
- The first PostgreSQL deployment creates a new schema but does not automatically import data from an existing SQLite file. Export/import records before switching if there is data to preserve; never point the app to an unverified database and assume accounts have been migrated.
- Đánh giá sản phẩm được tạo thêm idempotent khi ứng dụng khởi động, chỉ dành cho sản phẩm thuộc đơn đã giao; mỗi sản phẩm chỉ được đánh giá một lần trong một đơn hàng. Nhân viên và quản trị viên có thể phản hồi/cập nhật phản hồi; khách xem phản hồi trong lịch sử đơn hàng của mình.
- Định kỳ thử khôi phục database cùng ảnh upload vào môi trường riêng để `ImageId`/`AvatarImageId` không trỏ tới file thiếu.
- Không đặt file backup trong `wwwroot` hoặc một thư mục được phục vụ công khai.

## Lưu video trên object storage

- Video thời trang có thể lưu trong bucket S3-compatible riêng tư thay vì ổ đĩa tạm của Render. Tạo bucket và API token có quyền đọc/ghi object tại nhà cung cấp (ví dụ Cloudflare R2), sau đó đặt các biến môi trường Render: `Storage__S3__Endpoint` (HTTPS endpoint), `Storage__S3__Bucket`, `Storage__S3__AccessKey`, và `Storage__S3__SecretKey`. Với R2, endpoint thường là `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`, vùng ký mặc định là `auto` (`Storage__S3__Region`). Không bật public access cho bucket; ứng dụng stream video qua endpoint có kiểm tra tên file và hỗ trợ HTTP Range để tua video. `render.yaml` khai báo bốn biến này để nhập dưới dạng secrets/config trong Render.
- Nếu không cấu hình S3, ứng dụng dùng local storage. Render blueprint bật `Storage__AllowLocalVideoStorage=true` theo yêu cầu để cho phép tải video dù chưa có ổ đĩa persistent; trên Render Free, video có thể mất sau restart hoặc deploy. Nếu chỉ cấu hình một phần S3, ứng dụng dừng khởi động để tránh lưu file nhầm nơi. Video cũ chỉ được giữ nếu tệp local vẫn còn trên máy chủ cũ; nếu đã mất, cần tải bản gốc lên lại.
- Cấu hình này hiện áp dụng cho video fashion. Ảnh sản phẩm, ảnh đại diện và video yêu cầu trả hàng vẫn dùng local storage và có thể mất khi Render thay máy chủ.

## Kiểm tra trước khi phát hành

- Nhập kho theo lô nhận nhiều sản phẩm trong một giao dịch; mỗi dòng có số lượng và giá vốn riêng. Có thể dán ba cột `mã sản phẩm; số lượng; giá nhập` từ bảng tính, tối đa 200 sản phẩm mỗi phiếu. Tạo đối tác tại mục Đối tác trước khi ghi phiếu: nhập mã đối tác duy nhất, tên, địa chỉ, số điện thoại và email tùy chọn; có thể sửa thông tin hoặc ngừng sử dụng mà không mất lịch sử. Mã và tên đối tác được hiển thị trong lựa chọn nhà cung cấp và bản ghi nhập hàng. Mã phiếu `PN-xxxxxx` được cấp tự động và dùng chung cho mọi dòng trong cùng phiếu. Tab Nhập hàng có form tạo nhanh mặt hàng mới (tồn ban đầu 0).
- Tab Sản phẩm cho phép dán tối đa 500 dòng theo thứ tự `mã; tên; danh mục; giá bán; màu; kích cỡ; tồn đầu; mô tả tùy chọn`, phân tách bằng tab hoặc dấu chấm phẩy. Sản phẩm nạp hàng loạt dùng ảnh mẫu theo danh mục; tồn đầu được ghi trực tiếp, không tạo phiếu nhập tài chính.
- Xác nhận tài khoản admin/nhân viên/khách hàng đúng vai trò; các action thay đổi dữ liệu phải tiếp tục có `[Authorize]` theo vai trò và antiforgery.
- Chạy build và kiểm thử các luồng đăng nhập, đặt/hủy đơn, chuyển trạng thái, tồn kho, upload ảnh, email (SMTP sandbox), và chat/fallback AI.
- Không dùng key production trong máy developer hoặc pipeline log; giới hạn quyền của từng key và xoay key khi nghi bị lộ.
- Hiện chưa có project kiểm thử tự động trong repository. Hãy bổ sung test project qua solution đang mở trong Visual Studio trước khi giao việc tiếp theo liên quan kiểm thử.
