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

- Production dùng biến môi trường dạng `Ai__Providers__0__Type`, `Ai__Providers__0__Model`, `Ai__Providers__0__Endpoint`, `Ai__Providers__0__ApiKey`; provider tiếp theo dùng chỉ số `1`. Với Groq, đặt `Ai__Providers__1__Type=Groq`, `Ai__Providers__1__Model` bằng model ID còn hoạt động, `Ai__Providers__1__Endpoint=https://api.groq.com/openai/v1/chat/completions`, và `Ai__Providers__1__ApiKey` bằng secret mới. Tên `Ai__ApiKey_Groq` không được ứng dụng đọc. Các provider được thử theo thứ tự chỉ số; cấu hình cũ `Ai__ApiKey`/`Ai__Model` vẫn dùng như Gemini dự phòng cuối danh sách. Groq request bỏ `response_format` và tắt reasoning output.
- Đăng nhập tạo cookie persistent hạn 30 ngày, gia hạn trượt khi người dùng tiếp tục hoạt động. Khóa ASP.NET Core Data Protection được lưu trong bảng `DataProtectionKeys` của DB để cookie còn hợp lệ qua deploy/restart; giới hạn này vẫn áp dụng theo thời hạn cookie và trạng thái tài khoản. Chỉ tài khoản của thiết bị bạn dùng; đăng xuất sẽ xóa cookie trên thiết bị hiện tại.
- Email thông báo cần cấu hình SMTP qua secret store/biến môi trường: `Email__Smtp__Host`, `Email__Smtp__Port` (mặc định 587), `Email__Smtp__Username`, `Email__Smtp__Password`, `Email__Smtp__From`, `Email__Smtp__EnableSsl` (mặc định true), và `Email__StaffRecipients` (địa chỉ nhân viên, phân tách bằng dấu phẩy hoặc chấm phẩy). Nếu chưa cấu hình, đơn hàng vẫn được lưu; ứng dụng ghi log cảnh báo, khách theo dõi trạng thái trong tài khoản và nhân viên xem đơn trong bàn làm việc. Chức năng quên mật khẩu cũng cần SMTP để gửi liên kết đặt lại; thông báo trên trang luôn giữ chung chung để không tiết lộ email có tài khoản hay không.
- Đăng nhập Google/Facebook dùng `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, `Authentication__Facebook__AppId`, `Authentication__Facebook__AppSecret`. Chỉ cấu hình đủ từng cặp; trang đăng nhập hiển thị trạng thái chưa cấu hình cho provider còn thiếu. Đăng ký callback URL HTTPS trên Google/Facebook đúng domain: `/signin-google` và `/signin-facebook`. Khóa chỉ đặt trong User Secrets/secret store, không commit vào source.
- Đăng nhập xã hội mới chỉ tạo tài khoản Customer. Tài khoản hiện hữu chỉ tự liên kết khi Google xác nhận email và tài khoản W Studio đã xác nhận email; không tự nâng quyền theo thông tin từ nhà cung cấp. Facebook không được dùng để tự liên kết tài khoản email đã có.
- Sau khi đổi secret/config, khởi động lại ứng dụng để nạp cấu hình mới.

## Sao lưu và khôi phục

- W Studio uses one configured database for the storefront, customer accounts, admin catalog, staff inventory, orders, reviews, fashion-video metadata, and support chat. Local development uses SQLite; Render uses PostgreSQL. Back up the database through the provider's supported backup/export tools. Back up uploaded product images, customer avatars, and video objects separately.
- Render Free web-service files are ephemeral, so local uploads can be lost on redeploy/restart. The Render blueprint allows fashion-video uploads to local storage even without S3; configure S3-compatible object storage or a persistent disk to retain them. PostgreSQL data is external to the web service; its availability, retention, limits, and backups depend on the selected database provider and plan.
- The first PostgreSQL deployment creates a new schema but does not automatically import data from an existing SQLite file. Export/import records before switching if there is data to preserve; never point the app to an unverified database and assume accounts have been migrated.
- Đánh giá sản phẩm được tạo thêm idempotent khi ứng dụng khởi động, chỉ dành cho sản phẩm thuộc đơn đã giao; mỗi sản phẩm chỉ được đánh giá một lần trong một đơn hàng. Nhân viên và quản trị viên có thể phản hồi/cập nhật phản hồi; khách xem phản hồi trong lịch sử đơn hàng của mình.
- Định kỳ thử khôi phục database cùng ảnh upload vào môi trường riêng để `ImageId`/`AvatarImageId` không trỏ tới file thiếu.
- Không đặt file backup trong `wwwroot` hoặc một thư mục được phục vụ công khai.

## Lưu video trên object storage

- Video thời trang có thể lưu trong bucket S3-compatible Supabase thay vì ổ đĩa tạm của Render. Tạo bucket `fashion-videos`; nếu muốn phát trực tiếp từ Supabase CDN, bật **Public** (video và URL object sẽ công khai). Trên Render đặt `Storage__S3__Endpoint` (`https://<project-ref>.storage.supabase.co/storage/v1/s3`), `Storage__S3__Bucket=fashion-videos`, `Storage__S3__AccessKey`, `Storage__S3__SecretKey`, và `Storage__S3__Region` theo trang S3 Configuration. Để phát trực tiếp public, đặt thêm `Storage__S3__PublicBaseUrl=https://<project-ref>.supabase.co/storage/v1/object/public/fashion-videos`; app sẽ chuyển video sang URL này để trình duyệt lấy từ Supabase. Để giữ bucket private, bỏ `Storage__S3__PublicBaseUrl`; app sẽ stream qua server và hỗ trợ HTTP Range. Credentials S3 chỉ dùng server-side, không đưa vào source control hoặc trình duyệt.
- Supabase Free hiện giới hạn mỗi file tối đa 50 MB, tổng file storage 1 GB và egress 5 GB + 5 GB cached egress theo trang pricing; app giới hạn video 50 MB. Hãy theo dõi Usage, vì vượt mức hoặc đổi chính sách có thể phát sinh chi phí/gián đoạn. Nếu dùng nhà cung cấp khác như R2, cũng giữ giới hạn 50 MB hiện tại của form trừ khi app được đổi.
- Trên production, nếu không cấu hình S3, ứng dụng vẫn nhận upload vào local storage. Đặt `Storage__PersistentPath` trỏ tới ổ đĩa persistent đã gắn và bật `Storage__AllowLocalVideoStorage=true` để giữ video qua deploy/restart; trên Render Free, local video có thể mất. Nếu chỉ cấu hình một phần S3, ứng dụng dừng khởi động để tránh lưu file nhầm nơi. Video cũ chỉ được giữ nếu tệp local vẫn còn; nếu đã mất, cần tải bản gốc lên lại.
- Avatar khách hàng có thể lưu bền vững riêng trong Supabase Storage. Tạo bucket public `avatars`, sau đó cấu hình trên Render `Storage__Avatars__S3__Endpoint` (S3 endpoint, dạng `https://<project-ref>.storage.supabase.co/storage/v1/s3`), `Storage__Avatars__S3__Bucket=avatars`, `Storage__Avatars__S3__AccessKey`, `Storage__Avatars__S3__SecretKey`, `Storage__Avatars__S3__Region` (giá trị trong trang S3 của project), và `Storage__Avatars__S3__PublicBaseUrl` (dạng `https://<project-ref>.supabase.co/storage/v1/object/public/avatars`). Keys chỉ được dùng phía server; không đưa chúng vào mã nguồn hoặc trình duyệt. Khi đủ cấu hình, avatar mới tải lên Supabase; avatar cũ lưu local vẫn tiếp tục hiển thị nếu file còn tồn tại.
- Ảnh sản phẩm và video yêu cầu trả hàng vẫn dùng local storage và có thể mất khi Render thay máy chủ.

## Kiểm tra trước khi phát hành

- Nhập kho theo lô nhận nhiều sản phẩm trong một giao dịch; mỗi dòng có số lượng và giá vốn riêng. Có thể dán ba cột `mã sản phẩm; số lượng; giá nhập` từ bảng tính, tối đa 200 dòng mỗi phiếu; giá chấp nhận dạng `250000`, `250.000`, `250,000` hoặc `250.000,50`. Có thể nhập lặp mã sản phẩm ở nhiều dòng; nếu phiếu bị từ chối, danh sách đã nhập được khôi phục để sửa. Tạo đối tác tại mục Đối tác trước khi ghi phiếu: nhập mã đối tác duy nhất, tên, địa chỉ, số điện thoại và email tùy chọn; có thể sửa thông tin hoặc ngừng sử dụng mà không mất lịch sử. Mã và tên đối tác được hiển thị trong lựa chọn nhà cung cấp và bản ghi nhập hàng. Mã phiếu `PN-xxxxxx` được cấp tự động và dùng chung cho mọi dòng trong cùng phiếu.
- Tạo sản phẩm riêng lẻ trong tab Sản phẩm; để ghi nhận hàng nhập và chi phí vốn, dùng phiếu nhập kho thay vì nhập tồn ban đầu trực tiếp.
- Xác nhận tài khoản admin/nhân viên/khách hàng đúng vai trò; các action thay đổi dữ liệu phải tiếp tục có `[Authorize]` theo vai trò và antiforgery.
- Chạy build và kiểm thử các luồng đăng nhập, đặt/hủy đơn, chuyển trạng thái, tồn kho, upload ảnh, email (SMTP sandbox), và chat/fallback AI.
- Không dùng key production trong máy developer hoặc pipeline log; giới hạn quyền của từng key và xoay key khi nghi bị lộ.
- Hiện chưa có project kiểm thử tự động trong repository. Hãy bổ sung test project qua solution đang mở trong Visual Studio trước khi giao việc tiếp theo liên quan kiểm thử.
