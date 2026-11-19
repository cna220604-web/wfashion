from PIL import Image, ImageDraw, ImageFont
from pptx import Presentation
from pptx.util import Inches
import os

ROOT=r'D:\aac\wfashion'
OUT=os.path.join(ROOT,'Thuyet-trinh-W-Studio-Slide-Anh.pptx')
SLIDE_DIR=os.path.join(ROOT,'session-state','files','wstudio-slide-images')
os.makedirs(SLIDE_DIR,exist_ok=True)

W,H=1600,900
CREAM=(248,242,233); BG=(31,27,25); PANEL=(43,38,35); WINE=(119,60,61); GOLD=(215,180,133); TEXT=(250,247,240); MUTED=(193,181,167); LINE=(104,91,81)
font_regular=r'C:\Windows\Fonts\segoeui.ttf'
font_bold=r'C:\Windows\Fonts\segoeuib.ttf'
font_italic=r'C:\Windows\Fonts\segoeuii.ttf'

def font(size,bold=False):
    return ImageFont.truetype(font_bold if bold else font_regular,size)

def round_box(d,box,fill=PANEL,outline=LINE,radius=24,width=2):
    d.rounded_rectangle(box,radius=radius,fill=fill,outline=outline,width=width)

def wrap_text(d,text,box,base_font,max_lines=None,fill=TEXT,align='left',spacing=8):
    x1,y1,x2,y2=box
    words=text.split(); lines=[]; cur=''
    for word in words:
        test=(cur+' '+word).strip()
        if d.textbbox((0,0),test,font=base_font)[2] <= x2-x1:
            cur=test
        else:
            if cur: lines.append(cur)
            cur=word
    if cur:lines.append(cur)
    if max_lines:lines=lines[:max_lines]
    line_height=base_font.size+spacing
    y=y1
    for line in lines:
        b=d.textbbox((0,0),line,font=base_font)
        tw=b[2]-b[0]
        tx=x1 if align=='left' else (x1+(x2-x1-tw)//2)
        d.text((tx,y),line,font=base_font,fill=fill)
        y+=line_height
    return y

def base(kicker,title_text,number):
    im=Image.new('RGB',(W,H),BG);d=ImageDraw.Draw(im)
    d.text((70,38),kicker.upper(),font=font(20,True),fill=GOLD)
    d.text((70,78),title_text,font=font(38,True),fill=TEXT)
    d.text((1508,42),f'{number:02}',font=font(18,True),fill=GOLD)
    d.line((70,142,1530,142),fill=LINE,width=2)
    return im,d

def footer(d,text='W STUDIO • ĐỒ ÁN'):d.text((70,858),text,font=font(14,True),fill=MUTED)

def add_card(d,x,y,w,h,title_text,body=None,color=PANEL):
    round_box(d,(x,y,x+w,y+h),fill=color,outline=(82,70,62))
    d.text((x+24,y+20),title_text,font=font(24,True),fill=GOLD)
    if body:
        wrap_text(d,body,(x+24,y+62,x+w-24,y+h-16),font(17),fill=MUTED,spacing=5)

def save(name,im):
    path=os.path.join(SLIDE_DIR,name)
    im.save(path,quality=95)
    return path

paths=[]
# 1 cover
im=Image.new('RGB',(W,H),BG);d=ImageDraw.Draw(im)
d.text((800,55),'TRƯỜNG CAO ĐẲNG CÔNG NGHỆ CAO HÀ NỘI',font=font(25,True),fill=GOLD,anchor='ma')
d.text((800,210),'W STUDIO',font=font(82,True),fill=CREAM,anchor='ma')
d.text((800,325),'WEBSITE THƯƠNG MẠI ĐIỆN TỬ THỜI TRANG',font=font(34),fill=TEXT,anchor='ma')
round_box(d,(220,430,1380,610),fill=PANEL,outline=WINE,radius=30)
d.text((800,470),'SINH VIÊN THỰC HIỆN',font=font(20,True),fill=GOLD,anchor='ma')
d.text((800,525),'Bùi Đức Ân  •  Lê Văn Cường  •  Hà Văn Thiện',font=font(28,True),fill=TEXT,anchor='ma')
d.text((800,575),'Hà Nội — 2026',font=font(22),fill=MUTED,anchor='ma')
paths.append(save('01-cover.png',im))

# 2 overview
im,d=base('01 • Tổng quan','Giới thiệu đề tài',2)
items=[('NỀN TẢNG','Kết hợp cửa hàng, tư vấn, chat và quản trị'),('MUA HÀNG','COD, voucher, đánh giá, đổi/trả'),('CÔNG NGHỆ','ASP.NET Core, EF Core, Identity'),('NỘI DUNG','Stylist AI, W-video, tạp chí thời trang')]
for i,(a,b) in enumerate(items):
    x=70+(i%2)*750;y=180+(i//2)*260
    add_card(d,x,y,710,220,a,b)
footer(d);paths.append(save('02-overview.png',im))

# 3 architecture
im,d=base('02 • Kiến trúc','Công nghệ và kiến trúc hệ thống',3)
levels=[('TRÌNH BÀY','Razor Views • CSS • JavaScript'),('ĐIỀU KHIỂN','MVC Controllers • API Controllers'),('NGHIỆP VỤ','AI • Promotion • Media • Mail • Presence'),('DỮ LIỆU','EF Core • SQLite / PostgreSQL')]
for i,(a,b) in enumerate(levels):
    y=190+i*155
    round_box(d,(240,y,1360,y+115),fill=PANEL,outline=(WINE if i in (0,2) else LINE),radius=22)
    d.text((280,y+25),a,font=font(22,True),fill=GOLD)
    d.text((650,y+28),b,font=font(20),fill=TEXT)
    if i<3:d.text((800,y+120),'▼',font=font(18,True),fill=GOLD,anchor='ma')
footer(d);paths.append(save('03-architecture.png',im))

# 4 use case detailed
im,d=base('03 • Use Case','Sơ đồ Use Case chi tiết',4)
# actors
round_box(d,(38,270,210,430),fill=PANEL,outline=GOLD,radius=20)
d.text((124,300),'KHÁCH',font=font(20,True),fill=TEXT,anchor='ma');d.text((124,340),'HÀNG',font=font(20,True),fill=TEXT,anchor='ma')
round_box(d,(1390,250,1565,400),fill=PANEL,outline=GOLD,radius=20)
d.text((1478,290),'NHÂN',font=font(18,True),fill=TEXT,anchor='ma');d.text((1478,326),'VIÊN',font=font(18,True),fill=TEXT,anchor='ma')
round_box(d,(1390,560,1565,710),fill=PANEL,outline=GOLD,radius=20)
d.text((1478,600),'QUẢN TRỊ',font=font(18,True),fill=TEXT,anchor='ma');d.text((1478,636),'VIÊN',font=font(18,True),fill=TEXT,anchor='ma')
# system boundary
round_box(d,(270,170,1350,780),fill=(250,247,240),outline=(60,52,47),radius=18,width=3)
d.text((810,188),'HỆ THỐNG W STUDIO',font=font(22,True),fill=WINE,anchor='ma')
# use cases
ucs=[
(360,245,220,60,'Đăng ký / Đăng nhập'),(360,330,220,60,'Tìm kiếm sản phẩm'),(360,415,220,60,'Giỏ hàng / đặt COD'),(360,500,220,60,'Theo dõi đơn hàng'),(360,585,220,60,'Đánh giá / đổi trả'),
(810,245,220,60,'Stylist AI'),(810,330,220,60,'Chuyển chat nhân viên'),(810,415,220,60,'Xem W-video'),(810,500,220,60,'Xem tạp chí'),(810,585,220,60,'Cập nhật hồ sơ'),
(1060,245,220,60,'Nhận & trả lời chat'),(1060,330,220,60,'Xử lý đơn hàng'),(1060,415,220,60,'Cập nhật tồn kho'),(1060,500,220,60,'Quản lý đối tác'),(1060,585,220,60,'Quản lý danh mục'),(1060,670,220,60,'Quản lý tài khoản / video')]
for x,y,w,h,lab in ucs:
    d.ellipse((x,y,x+w,y+h),fill=(255,253,249),outline=WINE,width=2)
    d.text((x+w/2,y+h/2),lab,font=font(15,True),fill=(55,47,43),anchor='mm')
# association lines
for y in [275,360,445,530,615]: d.line((248,y,360,y),fill=(120,110,102),width=2)
for y in [275,360,445,530,615,700]: d.line((1280,y if y<650 else 700,1390,325 if y<470 else 630),fill=(120,110,102),width=2)
footer(d);paths.append(save('04-use-case.png',im))

# 5 use case ordering
im,d=base('04 • Use Case','Đặc tả Use Case đặt hàng và hỗ trợ',5)
add_card(d,70,180,735,280,'UC-01 • ĐẶT HÀNG COD','Tác nhân: Khách hàng\nTiền điều kiện: Đã đăng nhập, giỏ hàng hợp lệ\nLuồng chính: Chọn size → giỏ hàng → voucher → xác thực giá/tồn → tạo đơn → trừ tồn → thông báo')
add_card(d,835,180,695,280,'UC-02 • CHUYỂN CHAT NHÂN VIÊN','Tác nhân: Khách hàng, Nhân viên\nTiền điều kiện: Đang đăng nhập\nLuồng chính: Chat AI → bấm chuyển tiếp → tạo cuộc hỗ trợ → gán nhân viên → trả lời / đóng')
add_card(d,70,500,735,220,'LUỒNG NGOẠI LE','Giỏ trống • size không hợp lệ • tồn không đủ • voucher hết hiệu lực • AI/provider lỗi • chưa có nhân viên online')
add_card(d,835,500,695,220,'KẾT QUẢ','Lưu đơn COD và lịch sử trạng thái • lưu cuộc hội thoại • cập nhật tồn kho hoặc giữ trạng thái chờ')
footer(d);paths.append(save('05-usecase-spec.png',im))

# 6 role
im,d=base('05 • Vai trò','Phân quyền người dùng',6)
roles=[('KHÁCH HÀNG','Mua hàng và thanh toán COD\nChat AI / nhân viên\nYêu thích, đánh giá, đổi/trả'),('NHÂN VIÊN','Nhận inbox hỗ trợ\nXử lý đơn hàng\nCập nhật tồn kho'),('QUẢN TRỊ VIÊN','Sản phẩm và vai trò\nĐối tác, nhập kho, chi phí\nVoucher và W-video')]
for i,(a,b) in enumerate(roles):
    x=70+i*500
    add_card(d,x,190,460,500,a,b)
footer(d);paths.append(save('06-roles.png',im))

# 7 order activity
im,d=base('06 • Hoạt động','Sơ đồ hoạt động đặt hàng COD',7)
steps=['Bắt đầu','Kiểm tra đăng nhập','Kiểm tra giỏ hàng','Kiểm tra size và tồn kho','Tính voucher và tổng tiền','Tạo đơn trong transaction','Trừ tồn kho','Gửi xác nhận / email','Kết thúc']
ys=[170,250,330,410,490,570,650,730,810]
for i,(lab,y) in enumerate(zip(steps,ys)):
    if i==0:
        d.ellipse((780,y-20,820,y+20),fill=TEXT,outline=GOLD,width=4);d.text((840,y),lab,font=font(16,True),fill=TEXT,anchor='lm')
    elif i==len(steps)-1:
        d.ellipse((780,y-20,820,y+20),fill=PANEL,outline=GOLD,width=4);d.ellipse((790,y-10,810,y+10),fill=GOLD);d.text((840,y),lab,font=font(16,True),fill=TEXT,anchor='lm')
    else:
        round_box(d,(610,y-34,990,y+34),fill=PANEL,outline=WINE,radius=18)
        d.text((800,y),lab,font=font(16,True),fill=TEXT,anchor='mm')
        d.polygon([(790,y+34),(810,y+34),(800,y+48)],fill=GOLD)
# exceptions
round_box(d,(1050,400,1480,630),fill=(55,48,44),outline=GOLD,radius=18)
d.text((1070,430),'Rẽ nhánh lỗi',font=font(20,True),fill=GOLD)
wrap_text(d,'Chưa đăng nhập\nGiỏ trống\nSize/tồn không hợp lệ\nVoucher không hợp lệ',(1070,470,1450,610),font(17),fill=TEXT,spacing=7)
footer(d);paths.append(save('07-order-activity.png',im))

# 8 support activity
im,d=base('07 • Hoạt động','Sơ đồ hoạt động chuyển chat AI sang nhân viên',8)
step_boxes=[(70,330,'Khách nhập tin'),(270,330,'Stylist AI xử lý'),(470,330,'Chuyển nhân viên'),(690,330,'Tạo SupportConversation'),(930,330,'Tìm nhân viên online'),(1150,330,'Gán Active hoặc Waiting'),(1370,330,'Nhân viên trả lời')]
for i,(x,y,lab) in enumerate(step_boxes):
    round_box(d,(x,y,x+150,y+95),fill=PANEL,outline=WINE,radius=18)
    wrap_text(d,lab,(x+12,y+18,x+138,y+78),font(15,True),fill=TEXT,align='center',spacing=2)
    if i<len(step_boxes)-1:d.text((x+158,y+47),'→',font=font(26,True),fill=GOLD,anchor='mm')
# branches
round_box(d,(300,560,760,740),fill=PANEL,outline=LINE,radius=18)
d.text((530,590),'AI lỗi / hết quota',font=font(20,True),fill=GOLD,anchor='ma');d.text((530,640),'Hiển thị thông báo và giữ chat AI',font=font(16),fill=TEXT,anchor='ma')
round_box(d,(840,560,1300,740),fill=PANEL,outline=LINE,radius=18)
d.text((1070,590),'Không có nhân viên online',font=font(20,True),fill=GOLD,anchor='ma');d.text((1070,640),'Conversation ở trạng thái Waiting',font=font(16),fill=TEXT,anchor='ma')
footer(d);paths.append(save('08-support-activity.png',im))

# 9 admin
im,d=base('08 • Quản trị','Chức năng quản trị và nhân viên',9)
add_card(d,70,180,710,250,'QUẢN TRỊ VIÊN','Sản phẩm • vai trò • đối tác • nhập kho • chi phí • voucher • W-video • tài khoản nhân viên')
add_card(d,820,180,710,250,'NHÂN VIÊN','Inbox chat • nhận cuộc hỗ trợ • xử lý đơn • cập nhật trạng thái • tồn kho • phản hồi đánh giá')
add_card(d,70,470,710,250,'NHẬP KHO','Chọn đối tác • dán dữ liệu từ Excel • tối đa 200 dòng • tạo mã PN • cộng tồn trong transaction')
add_card(d,820,470,710,250,'GIAO DIỆN','Form mở bằng cửa sổ nổi • danh sách cuộn riêng • mobile một cột • không đẩy nội dung')
footer(d);paths.append(save('09-admin.png',im))

# 10 database
im,d=base('09 • Cơ sở dữ liệu','Nhóm thực thể chính',10)
entities=[('ApplicationUser','Tài khoản, hồ sơ, vai trò'),('Product','Thông tin, giá, tồn, size, ảnh'),('CustomerOrder / Item','Đơn hàng và snapshot'),('SupportConversation','Phiên hỗ trợ và nhân viên'),('SupportMessage','Nội dung tin nhắn'),('BusinessPartner','Đối tác nhập hàng'),('StockReceipt','Phiếu nhập kho'),('OperatingExpense','Chi phí vận hành'),('Promotion','Voucher'),('FashionVideo','Video, caption, sản phẩm')]
for i,(a,b) in enumerate(entities):
    c=i%2;r=i//2;x=70+c*760;y=170+r*125
    round_box(d,(x,y,x+720,y+95),fill=PANEL,outline=(82,70,62),radius=18)
    d.text((x+24,y+20),a,font=font(18,True),fill=GOLD)
    d.text((x+24,y+55),b,font=font(16),fill=TEXT)
footer(d);paths.append(save('10-database.png',im))

# 11 interface
im,d=base('10 • Giao diện','Responsive và trải nghiệm người dùng',11)
add_card(d,70,180,460,230,'TRANG CHỦ','Hero • mùa • phối màu • tạp chí • Stylist AI')
add_card(d,570,180,460,230,'W-VIDEO','Tìm kiếm • lướt video • overlay mờ • phù hợp mobile')
add_card(d,1070,180,460,230,'ADMIN','Tab • bảng cuộn • form cửa sổ nổi')
add_card(d,70,470,460,230,'MOBILE','Một cột • không tràn ngang • điểm chạm lớn')
add_card(d,570,470,460,230,'STAFF','Đơn hàng • inbox • tồn kho • phản hồi')
add_card(d,1070,470,460,230,'TRỰC QUAN','Chữ lớn • màu tương phản • khoảng cách rõ')
footer(d);paths.append(save('11-interface.png',im))

# 12 testing
im,d=base('11 • Kiểm thử','Ca kiểm thử tiêu biểu',12)
rows=[('TC01','Đăng ký hợp lệ','Tạo tài khoản Customer'),('TC02','Đặt hàng đủ tồn','Tạo đơn và trừ tồn'),('TC03','Size/tồn không hợp lệ','Từ chối giữ nguyên tồn'),('TC05','Nhập kho nhiều dòng','Cập nhật transaction'),('TC06','Chuyển chat nhân viên','Tạo SupportConversation'),('TC10','320–1440 px','Không tràn hoặc che nội dung')]
y=170
for i,(code,name,result) in enumerate(rows):
    fill=PANEL if i%2==0 else (38,34,32)
    round_box(d,(70,y,1530,y+90),fill=fill,outline=(82,70,62),radius=14)
    d.text((100,y+25),code,font=font(18,True),fill=GOLD)
    d.text((260,y+25),name,font=font(18),fill=TEXT)
    d.text((1120,y+25),result,font=font(18,True),fill=TEXT)
    y+=105
footer(d);paths.append(save('12-testing.png',im))

# 13 deployment
im,d=base('12 • Triển khai','Môi trường và bảo mật',13)
items=[('LOCAL','SQLite • dotnet run'),('PRODUCTION','PostgreSQL • Render'),('CONTAINER','Docker • .NET publish'),('MEDIA','S3-compatible / persistent disk'),('BẢO MẬT','Antiforgery • Identity • secure cookie'),('CẤU HÌNH','User Secrets • Environment variables')]
for i,(a,b) in enumerate(items):
    x=70+(i%3)*500;y=180+(i//3)*260
    add_card(d,x,y,460,220,a,b)
footer(d);paths.append(save('13-deployment.png',im))

# 14 limitations
im,d=base('13 • Đánh giá','Hạn chế và hướng phát triển',14)
add_card(d,70,180,710,520,'HẠN CHẾ','Chưa tích hợp thanh toán online\nChưa có test project tự động\nEmail vận chuyển còn phụ thuộc SMTP\nPresence nhân viên process-local\nLocal storage có thể mất khi redeploy')
add_card(d,820,180,710,520,'HƯỚNG PHÁT TRIỂN','EF Core migrations + test project\nThanh toán và vận chuyển\nSignalR cho chat realtime\nRedis cho nhiều instance\nDashboard doanh thu/lợi nhuận\nGiám sát và backup tự động')
footer(d);paths.append(save('14-future.png',im))

# 15 conclusion
im,d=base('14 • Kết luận','Tổng kết đồ án',15)
round_box(d,(180,220,1420,500),fill=PANEL,outline=WINE,radius=30)
d.text((800,280),'W STUDIO đã hoàn thiện',font=font(38,True),fill=GOLD,anchor='ma')
wrap_text(d,'Cửa hàng • đặt hàng • Stylist AI • chat nhân viên • quản lý kho • đối tác • chi phí • W-video • tạp chí',(300,350,1300,470),font(27,True),fill=TEXT,align='center',spacing=12)
d.text((800,610),'CẢM ƠN THẦY/CÔ VÀ CÁC BẠN ĐÃ LẮNG NGHE',font=font(24,True),fill=GOLD,anchor='ma')
footer(d);paths.append(save('15-conclusion.png',im))

# Remove old PPT before creating replacement
old=os.path.join(ROOT,'Thuyet-trinh-du-an-W-Studio.pptx')
if os.path.exists(old):os.remove(old)

prs=Presentation();prs.slide_width=Inches(13.333333);prs.slide_height=Inches(7.5)
for path in paths:
    slide=prs.slides.add_slide(prs.slide_layouts[6])
    slide.shapes.add_picture(path,0,0,width=prs.slide_width,height=prs.slide_height)
prs.save(OUT)
print(OUT)
print(len(prs.slides))
