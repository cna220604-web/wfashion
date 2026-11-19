from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.section import WD_SECTION
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from pptx import Presentation
from pptx.util import Inches as PInches, Pt as PPt
from pptx.dml.color import RGBColor as PRGB
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE, MSO_CONNECTOR
from pptx.enum.dml import MSO_THEME_COLOR
import os
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Ellipse, Circle, Polygon

ROOT = r'D:\aac\wfashion'
WORD_OUT = os.path.join(ROOT, 'Bao-cao-do-an-W-Studio.docx')
PPT_OUT = os.path.join(ROOT, 'Thuyet-trinh-W-Studio.pptx')
ASSET_DIR = os.path.join(ROOT, 'session-state', 'files')

# ---------- Shared diagram images ----------
def save_usecase_diagram(path):
    fig, ax = plt.subplots(figsize=(12, 7.2), dpi=180)
    ax.set_xlim(0, 12); ax.set_ylim(0, 7.2); ax.axis('off')
    ax.add_patch(FancyBboxPatch((3.1, 0.35), 6.7, 6.45, boxstyle='round,pad=0.02,rounding_size=0.12', facecolor='#FAF7F2', edgecolor='#2F2A27', lw=1.5))
    ax.text(6.45, 6.58, 'HỆ THỐNG W STUDIO', ha='center', va='center', fontsize=12, fontweight='bold', color='#773C3D')
    def actor(x,y,label):
        ax.add_patch(Circle((x,y+0.43),0.12,facecolor='#2F2A27'))
        ax.plot([x,x],[y+0.31,y],[x-0.18,x+0.18],[y-0.12,y-0.12],color='#2F2A27',lw=1.8)
        ax.text(x,y-0.42,label,ha='center',fontsize=9,fontweight='bold')
    def uc(x,y,w,label):
        ax.add_patch(Ellipse((x,y),w,0.54,facecolor='#FFF',edgecolor='#773C3D',lw=1.2))
        ax.text(x,y,label,ha='center',va='center',fontsize=8)
    actor(1.0,5.45,'Khách hàng'); actor(10.95,5.45,'Nhân viên'); actor(10.95,1.15,'Quản trị viên')
    # customer
    uc(4.7,5.75,2.1,'Đăng ký / Đăng nhập')
    uc(4.7,4.95,2.2,'Tìm kiếm sản phẩm')
    uc(4.7,4.15,2.0,'Quản lý giỏ hàng')
    uc(4.7,3.35,2.1,'Đặt hàng COD')
    uc(4.7,2.55,2.0,'Theo dõi đơn hàng')
    uc(4.7,1.75,2.0,'Đánh giá / yêu cầu đổi')
    uc(7.7,5.15,2.15,'Stylist AI')
    uc(7.7,4.25,2.35,'Chuyển chat nhân viên')
    uc(7.7,3.35,2.15,'Xem W-video')
    uc(7.7,2.45,2.15,'Xem tạp chí thời trang')
    uc(7.7,1.55,2.05,'Cập nhật hồ sơ')
    # staff
    uc(7.7,5.15,2.15,'Nhận inbox hỗ trợ'); uc(7.7,1.15,2.15,'Cập nhật tồn kho')
    uc(7.7,2.05,2.15,'Xử lý đơn hàng')
    # admin
    uc(7.7,0.95,2.15,'Quản lý danh mục')
    uc(7.7,1.95,2.15,'Quản lý đối tác')
    uc(7.7,2.85,2.15,'Quản lý tài khoản')
    uc(7.7,3.75,2.15,'Quản lý video')
    uc(7.7,4.65,2.15,'Xem báo cáo / chi phí')
    for y in [5.75,4.95,4.15,3.35,2.55,1.75]: ax.plot([1.12,3.65],[y,y],color='#777',lw=1)
    for y in [5.15,2.05,1.15]: ax.plot([8.75,10.8],[y,y],color='#777',lw=1)
    for y in [4.65,3.75,2.85,1.95,0.95]: ax.plot([8.75,10.8],[y,y],color='#777',lw=1)
    plt.tight_layout(); plt.savefig(path,bbox_inches='tight',facecolor='white'); plt.close()

def save_activity_diagram(path):
    fig, ax=plt.subplots(figsize=(9,10),dpi=180); ax.set_xlim(0,9); ax.set_ylim(0,10); ax.axis('off')
    def box(x,y,w,h,text,fill='#FFF'):
        ax.add_patch(FancyBboxPatch((x,y),w,h,boxstyle='round,pad=0.03,rounding_size=0.08',facecolor=fill,edgecolor='#773C3D',lw=1.2)); ax.text(x+w/2,y+h/2,text,ha='center',va='center',fontsize=8.5,wrap=True)
    def arr(x1,y1,x2,y2): ax.annotate('',xy=(x2,y2),xytext=(x1,y1),arrowprops=dict(arrowstyle='-|>',color='#2F2A27',lw=1.2))
    ax.add_patch(Circle((4.5,9.55),.12,facecolor='#2F2A27')); ax.text(4.5,9.18,'Khách hàng gửi yêu cầu đặt hàng',ha='center',fontsize=9)
    arr(4.5,9.05,4.5,8.65); box(3.25,8.25,2.5,.38,'Kiểm tra đăng nhập')
    arr(4.5,8.25,4.5,7.78); box(3.25,7.38,2.5,.38,'Kiểm tra giỏ hàng')
    arr(4.5,7.38,4.5,6.91); box(3.25,6.51,2.5,.38,'Xác thực giá, size, tồn kho')
    arr(4.5,6.51,4.5,6.03); box(3.25,5.63,2.5,.38,'Tính voucher và tổng tiền')
    arr(4.5,5.63,4.5,5.15); box(3.25,4.75,2.5,.38,'Tạo đơn và trừ tồn kho')
    arr(4.5,4.75,4.5,4.27); box(3.25,3.87,2.5,.38,'Ghi lịch sử trạng thái')
    arr(4.5,3.87,4.5,3.39); box(3.25,2.99,2.5,.38,'Gửi thông báo email')
    arr(4.5,2.99,4.5,2.51); box(3.25,2.11,2.5,.38,'Hiển thị xác nhận đơn hàng')
    arr(4.5,2.11,4.5,1.63); ax.add_patch(Circle((4.5,1.42),.20,facecolor='#FFF',edgecolor='#2F2A27',lw=1.5)); ax.add_patch(Circle((4.5,1.42),.09,facecolor='#2F2A27'))
    ax.text(.35,9.55,'LUỒNG CHÍNH',fontsize=9,fontweight='bold',color='#773C3D')
    plt.tight_layout(); plt.savefig(path,bbox_inches='tight',facecolor='white'); plt.close()

def save_support_activity(path):
    fig,ax=plt.subplots(figsize=(10,7),dpi=180); ax.set_xlim(0,10);ax.set_ylim(0,7);ax.axis('off')
    def box(x,y,w,h,text): ax.add_patch(FancyBboxPatch((x,y),w,h,boxstyle='round,pad=0.03,rounding_size=.07',facecolor='#FFF',edgecolor='#773C3D',lw=1.2));ax.text(x+w/2,y+h/2,text,ha='center',va='center',fontsize=8)
    def arr(x1,y1,x2,y2):ax.annotate('',xy=(x2,y2),xytext=(x1,y1),arrowprops=dict(arrowstyle='-|>',lw=1.2,color='#333'))
    ax.add_patch(Circle((1,5.8),.12,facecolor='#222'))
    box(.35,4.9,1.3,.42,'Nhập tin AI'); arr(1,4.9,1,4.35); box(.35,3.75,1.3,.42,'Stylist xử lý')
    arr(1.65,3.96,3.0,3.96); box(3.0,3.75,1.7,.42,'Giữ chat AI')
    arr(4.7,3.96,5.25,3.96); box(5.25,3.75,1.55,.42,'Bấm chuyển nhân viên')
    arr(6.8,3.96,7.25,3.96); box(7.25,3.75,1.55,.42,'Tạo cuộc hỗ trợ')
    arr(8.02,3.75,8.02,3.15); box(7.25,2.55,1.55,.42,'Gán nhân viên online')
    arr(8.02,2.55,8.02,1.95); box(7.25,1.35,1.55,.42,'Nhân viên trả lời')
    arr(7.25,1.56,5.55,1.56); box(4.0,1.35,1.55,.42,'Khách nhận tin')
    arr(4,1.56,3.25,1.56); ax.add_patch(Circle((2.95,1.56),.18,facecolor='#FFF',edgecolor='#222'));ax.add_patch(Circle((2.95,1.56),.08,facecolor='#222'))
    plt.tight_layout();plt.savefig(path,bbox_inches='tight',facecolor='white');plt.close()

UC_IMG=os.path.join(ASSET_DIR,'use-case-diagram.png'); ACT_IMG=os.path.join(ASSET_DIR,'activity-order.png'); SUP_IMG=os.path.join(ASSET_DIR,'activity-support.png')
save_usecase_diagram(UC_IMG); save_activity_diagram(ACT_IMG); save_support_activity(SUP_IMG)

# ---------- Word report ----------
doc=Document()
sec=doc.sections[0]; sec.top_margin=Inches(.65);sec.bottom_margin=Inches(.65);sec.left_margin=Inches(.75);sec.right_margin=Inches(.75)
styles=doc.styles
styles['Normal'].font.name='Times New Roman'; styles['Normal'].font.size=Pt(12)
styles['Title'].font.name='Times New Roman';styles['Title'].font.size=Pt(22)
for st in ['Heading 1','Heading 2','Heading 3']:
    styles[st].font.name='Times New Roman';styles[st].font.color.rgb=RGBColor(119,60,61)
styles['Heading 1'].font.size=Pt(16);styles['Heading 2'].font.size=Pt(14);styles['Heading 3'].font.size=Pt(12)

def shade(cell,fill):
    tcPr=cell._tc.get_or_add_tcPr(); shd=OxmlElement('w:shd');shd.set(qn('w:fill'),fill);tcPr.append(shd)
def set_repeat_table_header(row):
    trPr=row._tr.get_or_add_trPr(); el=OxmlElement('w:tblHeader');el.set(qn('w:val'),'true');trPr.append(el)
def add_table(headers,rows,widths=None):
    t=doc.add_table(rows=1,cols=len(headers));t.alignment=WD_TABLE_ALIGNMENT.CENTER;t.style='Table Grid'
    for i,h in enumerate(headers):
        c=t.rows[0].cells[i];c.text=str(h);shade(c,'773C3D');
        for r in c.paragraphs[0].runs:r.font.bold=True;r.font.color.rgb=RGBColor(255,255,255)
    set_repeat_table_header(t.rows[0])
    for ridx,row in enumerate(rows):
        cells=t.add_row().cells
        for i,v in enumerate(row):
            cells[i].text=str(v);cells[i].vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
            if ridx%2==1:shade(cells[i],'F4EFE7')
    if widths:
        for row in t.rows:
            for i,w in enumerate(widths):row.cells[i].width=Inches(w)
    doc.add_paragraph()
    return t
def add_heading(text,lvl=1):doc.add_heading(text,level=lvl)
def add_bullets(items):
    for x in items:doc.add_paragraph(x,style='List Bullet')
def add_numbers(items):
    for x in items:doc.add_paragraph(x,style='List Number')

# cover
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
run=p.add_run('TRƯỜNG CAO ĐẲNG CÔNG NGHỆ CAO HÀ NỘI');run.bold=True;run.font.size=Pt(16);run.font.name='Times New Roman'
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
r=p.add_run('BÁO CÁO ĐỒ ÁN');r.bold=True;r.font.size=Pt(26);r.font.color.rgb=RGBColor(119,60,61)
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
r=p.add_run('W STUDIO — XÂY DỰNG WEBSITE THƯƠNG MẠI ĐIỆN TỬ THỜI TRANG');r.bold=True;r.font.size=Pt(20)
doc.add_paragraph('\n')
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
p.add_run('Sinh viên thực hiện:\n').bold=True
for name in ['Bùi Đức Ân','Lê Văn Cường','Hà Văn Thiện']:
    p=doc.add_paragraph(name);p.alignment=WD_ALIGN_PARAGRAPH.CENTER
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
p.add_run('Ngành: Công nghệ Thông tin  |  Năm thực hiện: 2026').bold=True
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;p.add_run('Hà Nội, 2026')
doc.add_page_break()

add_heading('LỜI CAM ĐOAN',1)
doc.add_paragraph('Nhóm sinh viên gồm Bùi Đức Ân, Lê Văn Cường và Hà Văn Thiện xin cam đoan báo cáo và sản phẩm đồ án được hoàn thành trên cơ sở phân tích, thiết kế và cài đặt thực tế. Các kết quả, số liệu và hình ảnh minh họa là kết quả làm việc của nhóm; tài liệu tham khảo được ghi trong phần danh mục tài liệu.')
add_heading('LỜI CẢM ƠN',1)
doc.add_paragraph('Nhóm xin trân trọng cảm ơn Khoa/Trung tâm và giảng viên hướng dẫn đã hỗ trợ kiến thức chuyên môn trong quá trình thực hiện đồ án. Xin cảm ơn gia đình, bạn bè và các đối tượng người dùng đã đóng góp ý kiến để cải thiện chất lượng hệ thống.')
add_heading('TÓM TẮT',1)
doc.add_paragraph('W Studio là ứng dụng thương mại điện tử thời trang được xây dựng bằng ASP.NET Core MVC, ASP.NET Core Identity và Entity Framework Core. Hệ thống hỗ trợ duyệt sản phẩm, giỏ hàng, COD, mã khuyến mãi, đánh giá, đổi/trả, Stylist AI, chuyển chat sang nhân viên, W-video, tạp chí thời trang, quản lý kho, đối tác, tài chính và phân quyền. Cơ sở dữ liệu hỗ trợ SQLite ở môi trường phát triển và PostgreSQL ở môi trường triển khai.')
add_heading('MỤC LỤC',1)
p=doc.add_paragraph();p.add_run('Hãy cập nhật mục lục trong Microsoft Word: Chọn toàn bộ tài liệu → References → Update Table.').italic=True

for title in ['1. GIỚI THIỆU ĐỒ ÁN','2. CƠ SỞ LÝ THUYẾT','3. PHÂN TÍCH YÊU CẦU','4. XÁC ĐỊNH NGHIỆP VỤ','5. THIẾT KẾ HỆ THỐNG','6. CƠ SỞ DỮ LIỆU','7. CÀI ĐẶT VÀ TRÌNH BÀY CHỨC NĂNG','8. KIỂM THỬ','9. TRIỂN KHAI','10. ĐÁNH GIÁ VÀ HƯỚNG PHÁT TRIỂN','11. KẾT LUẬN','PHỤ LỤC','DANH MỤC TÀI LIỆU THAM KHẢO']:
    doc.add_paragraph(title)
doc.add_page_break()

add_heading('1. GIỚI THIỆU ĐỒ ÁN',1)
add_heading('1.1. Lý do chọn đề tài',2)
doc.add_paragraph('Thương mại điện tử và mua sắm thời trang trên thiết bị di động ngày càng phổ biến. Bên cạnh danh mục và thanh toán, người mua cần tư vấn, nội dung lookbook và hỗ trợ trực tiếp. W Studio được xây dựng để kết hợp cửa hàng, Stylist AI, chat nhân viên, quản lý kho và vận hành trong một hệ thống.')
add_heading('1.2. Mục tiêu',2)
add_bullets(['Xây dựng website thời trang responsive, dễ sử dụng trên máy tính và điện thoại.','Tạo luồng mua hàng COD đầy đủ: chọn size, giỏ hàng, voucher, đặt hàng, thanh toán và theo dõi.','Phân quyền rõ ràng cho Khách hàng, Nhân viên và Quản trị viên.','Tích hợp chat AI và chuyển tiếp cuộc hội thoại cho nhân viên.','Quản lý sản phẩm, tồn kho, phiếu nhập, đối tác, chi phí, video và voucher.','Đảm bảo xác thực phía máy chủ, dữ liệu bền vững và bảo mật cơ bản.'])
add_heading('1.3. Đối tượng và phạm vi',2)
add_table(['Đối tượng','Chức năng chính'],[
('Khách hàng','Đăng ký, đăng nhập, xem sản phẩm, yêu thích, giỏ hàng, đặt hàng, đánh giá, đổi/trả, chat AI/nhân viên.'),
('Nhân viên','Xử lý đơn hàng, cập nhật tồn kho, phản hồi đánh giá, nhận và trả lời chat.'),
('Quản trị viên','Quản trị danh mục, vai trò, đối tác, phiếu nhập, chi phí, voucher, video và thống kê.')])

add_heading('2. CƠ SỞ LÝ THUYẾT',1)
add_heading('2.1. Kiến trúc ASP.NET Core MVC',2)
doc.add_paragraph('Ứng dụng sử dụng mô hình MVC: Controller điều hướng luồng nghiệp vụ, Model biểu diễn dữ liệu và Razor View hiển thị giao diện. Entity Framework Core thực hiện truy cập cơ sở dữ liệu qua DbContext và LINQ. ASP.NET Core Identity quản lý tài khoản, mật khẩu, vai trò và cookie đăng nhập.')
add_heading('2.2. Phân quyền',2)
doc.add_paragraph('Ba vai trò chính: Customer, Employee và Admin. Các Controller và API được bảo vệ bằng Authorize hoặc Authorize(Roles=...). Phương thức thay đổi dữ liệu có antiforgery token để hạn chế request giả mạo.')
add_heading('2.3. Cơ sở dữ liệu',2)
doc.add_paragraph('SQLite được dùng khi phát triển nội bộ; PostgreSQL được dùng trên môi trường triển khai. EF Core vẫn giữ cùng mô hình nghiệp vụ và ánh xạ thực thể.')
add_heading('2.4. Tích hợp AI và lưu trữ',2)
doc.add_paragraph('Stylist AI gửi ngữ cảnh tin nhắn và danh mục đang bán tới provider. Kết quả được kiểm tra lại mã sản phẩm, tồn kho và size trước khi gợi ý. Hệ thống hỗ trợ nhiều provider dự phòng và lưu media vào S3-compatible storage hoặc ổ đĩa bền vững.')

add_heading('3. PHÂN TÍCH YÊU CẦU',1)
add_heading('3.1. Yêu cầu chức năng',2)
add_table(['Nhóm','Yêu cầu chức năng'],[
('Tài khoản','Đăng ký, đăng nhập, quên mật khẩu, đăng nhập Google/Facebook, hồ sơ, avatar.'),
('Cửa hàng','Danh mục, tìm kiếm, lọc, phân trang, chi tiết size/màu, yêu thích, giỏ hàng.'),
('Đơn hàng','Xác thực tồn kho/giá, tạo COD, voucher, theo dõi, hủy, xác nhận nhận hàng, đổi/trả.'),
('Chat','Stylist AI, gợi ý sản phẩm, chuyển chat sang nhân viên, polling tin nhắn.'),
('Nội dung','Tạp chí thời trang theo nguồn RSS, W-video tìm theo tên sản phẩm/chú thích.'),
('Quản trị','Sản phẩm, vai trò, đối tác, nhập kho, chi phí, voucher, video, thống kê.'),
('Nhân viên','Inbox hỗ trợ, nhận cuộc chat, trả lời, đóng chat, cập nhật đơn/tồn kho.')])
add_heading('3.2. Yêu cầu phi chức năng',2)
add_bullets(['Giao diện responsive, thân thiện trên desktop và mobile.','Thời gian phản hồi hợp lý; giới hạn 10 yêu cầu chat/phút theo IP.','Session đăng nhập kéo dài 30 ngày, cookie HttpOnly và khóa xác thực qua DB.','API mutating cần antiforgery; endpoint quản trị kiểm tra vai trò.','Thông tin bí mật không lưu trong source hoặc JavaScript.','Lưu video S3 hoặc persistent disk để tránh mất khi redeploy.'])

add_heading('4. XÁC ĐỊNH NGHIỆP VỤ',1)
add_heading('4.1. Đặc tả Use Case tổng quát',2)
doc.add_picture(UC_IMG,width=Inches(7.0))
p=doc.paragraphs[-1];p.alignment=WD_ALIGN_PARAGRAPH.CENTER
doc.add_paragraph('Hình 4.1. Sơ đồ Use Case tổng quát',style='Caption')
add_heading('4.2. Use Case đặt hàng',3)
add_table(['Mục','Nội dung'],[
('Tên Use Case','Đặt hàng COD'),('Tác nhân chính','Khách hàng'),('Điều kiện trước','Đã đăng nhập và giỏ hàng có ít nhất một dòng hợp lệ'),('Luồng chính','Chọn sản phẩm/size; mở giỏ hàng; xem tổng tiền; nhập thông tin nhận hàng; áp voucher; xác nhận; hệ thống kiểm tra giá/tồn kho; tạo đơn; trừ tồn kho; gửi thông báo.'),('Luồng thay thế','Giỏ trống; size không hợp lệ; tồn kho không đủ; voucher không còn hiệu lực; chưa đăng nhập.'),('Điều kiện sau','Tạo đơn PENDING, lưu snapshot sản phẩm và hiển thị xác nhận.')])
add_heading('4.3. Use Case chat và chuyển nhân viên',3)
add_table(['Mục','Nội dung'],[
('Tên Use Case','Stylist AI và chuyển chat nhân viên'),('Tác nhân chính','Khách hàng'),('Tác nhân liên quan','Nhân viên hoặc Quản trị viên'),('Luồng chính','Khách gửi tin; AI xử lý câu hỏi và danh mục; khách bấm “Chuyển chat cho nhân viên”; hệ thống tạo Conversation; gán nhân viên online; nhân viên nhận và trả lời; tin nhắn được cập nhật theo polling.'),('Luồng thay thế','Chưa đăng nhập; AI lỗi/quá hạn mức; chưa có nhân viên online; cuộc chat đã đóng.'),('Điều kiện sau','Cuộc hội thoại chuyển sang Active hoặc Waiting và được lưu bền vững.')])
add_heading('4.4. Use Case xử lý nhập kho',3)
add_table(['Mục','Nội dung'],[
('Tên Use Case','Lập phiếu nhập hàng'),('Tác nhân chính','Quản trị viên'),('Điều kiện trước','Đối tác đang hoạt động và sản phẩm tồn tại'),('Luồng chính','Chọn đối tác/ngày nhập; nhập hoặc dán danh sách; hệ thống tính tổng và tồn kho; lưu phiếu mã PN-xxxxxx; cộng tồn từng dòng trong transaction.'),('Luồng thay thế','Thiếu đối tác; mã sản phẩm sai; số lượng/giá không hợp lệ; dòng vượt giới hạn.'),('Điều kiện sau','Tồn kho tăng và lịch sử nhập hàng được ghi lại.')])

add_heading('5. THIẾT KẾ HỆ THỐNG',1)
add_heading('5.1. Kiến trúc tổng thể',2)
add_table(['Lớp','Thành phần','Vai trò'],[
('Trình bày','Razor Views, CSS, JavaScript','Responsive UI, tương tác, polling chat, tìm kiếm.'),
('Điều khiển','MVC Controllers, API Controllers','Xử lý request, xác thực quyền và trả view/JSON.'),
('Dịch vụ','AI, Promotion, Media, Mail, Presence','Tách logic tích hợp và nghiệp vụ.'),
('Truy cập dữ liệu','EF Core DbContext','CRUD, truy vấn và transaction.'),
('Dữ liệu','SQLite/PostgreSQL','Lưu tài khoản, sản phẩm, đơn, chat, tài chính.')])
add_heading('5.2. Sơ đồ hoạt động đặt hàng',2)
doc.add_picture(ACT_IMG,width=Inches(6.2));doc.paragraphs[-1].alignment=WD_ALIGN_PARAGRAPH.CENTER
doc.add_paragraph('Hình 5.1. Sơ đồ hoạt động đặt hàng COD',style='Caption')
add_heading('5.3. Sơ đồ hoạt động chuyển chat',2)
doc.add_picture(SUP_IMG,width=Inches(6.8));doc.paragraphs[-1].alignment=WD_ALIGN_PARAGRAPH.CENTER
doc.add_paragraph('Hình 5.2. Sơ đồ hoạt động chuyển chat AI sang nhân viên',style='Caption')
add_heading('5.4. Thiết kế giao diện',2)
add_bullets(['Trang chủ có hero, danh mục mùa, phối màu, tạp chí và Stylist AI.','Trang W-video có thanh tìm kiếm và danh sách video dạng lướt, tối ưu mobile.','Admin và Staff dùng dashboard có tab, bảng cuộn độc lập và form mở bằng cửa sổ nổi.','Form dài được chia vùng cuộn; danh sách ưu tiên không gian hiển thị.'])

add_heading('6. CƠ SỞ DỮ LIỆU',1)
add_heading('6.1. Nhóm thực thể chính',2)
add_table(['Thực thể','Thuộc tính/quan hệ chính'],[
('ApplicationUser','Id, email, tên hiển thị, avatar, vai trò Identity.'),
('Product','Slug, tên, danh mục, giá, tồn kho, màu, size, ảnh, IsActive.'),
('CustomerOrder','CustomerId, snapshot thông tin giao hàng, trạng thái, tổng tiền.'),
('OrderItem','OrderId, ProductSlug, tên/giá/size snapshot, số lượng.'),
('SupportConversation','CustomerUserId, EmployeeUserId, trạng thái, session tab.'),
('SupportMessage','ConversationId, người gửi, vai trò, nội dung, thời gian.'),
('BusinessPartner','Mã, tên, địa chỉ, liên hệ, trạng thái.'),
('StockReceipt','Phiếu nhập, ngày nhập, nhà cung cấp, tổng tiền, dòng nhập.'),
('OperatingExpense','Nhóm, số tiền, ngày, mô tả, đơn vị nhận.'),
('FashionVideo','Sản phẩm gắn, tên file, chú thích, trạng thái.'),
('Promotion','Mã, loại, mức giảm, đơn tối thiểu, thời gian, lượt dùng.')])

add_heading('7. CÀI ĐẶT VÀ TRÌNH BÀY CHỨC NĂNG',1)
add_heading('7.1. Công nghệ sử dụng',2)
add_table(['Thành phần','Công nghệ'],[
('.NET/.NET SDK',' .NET 10'),('Framework','ASP.NET Core MVC, Razor Views'),('Xác thực','ASP.NET Core Identity'),('Cơ sở dữ liệu','EF Core 10; SQLite/PostgreSQL'),('Giao diện','HTML5, CSS3, JavaScript'),('AI','Gemini/OpenAI-compatible/Groq'),('Lưu trữ','Local storage, AWS SDK S3-compatible'),('Triển khai','Docker, Render')])
add_heading('7.2. Các chức năng tiêu biểu',2)
add_numbers(['Đăng ký, đăng nhập và phân quyền bằng Identity.','Tìm kiếm, lọc, yêu thích, giỏ hàng và đặt COD.','Voucher, snapshot đơn hàng, cập nhật tồn kho và trạng thái.','Stylist AI gợi ý tối đa 3 sản phẩm hợp lệ.','Chuyển chat AI sang nhân viên và cập nhật tin nhắn.','Quản lý sản phẩm, đối tác, nhập kho, chi phí, voucher và W-video.','Tạp chí thời trang refresh RSS; W-video responsive.','Xử lý đổi/trả, đánh giá và thông báo email theo cấu hình.'])

add_heading('8. KIỂM THỬ',1)
add_heading('8.1. Phương pháp kiểm thử',2)
doc.add_paragraph('Sử dụng kiểm thử thủ công tích hợp giao diện, kiểm tra request/response, kiểm tra dữ liệu sau mỗi hành động và kiểm tra quyền truy cập theo vai trò.')
add_heading('8.2. Ca kiểm thử chính',2)
add_table(['Mã','Chức năng','Điều kiện đầu vào','Kết quả mong đợi'],[
('TC01','Đăng ký','Email hợp lệ, mật khẩu theo cấu hình','Tạo tài khoản Customer'),('TC02','Đặt hàng','Giỏ hợp lệ, tồn đủ','Tạo đơn và trừ tồn'),('TC03','Đặt hàng','Tồn/size không hợp lệ','Từ chối, không trừ tồn'),('TC04','Voucher','Mã hết hạn/không đủ điều kiện','Không áp dụng'),('TC05','Nhập kho','Phiếu nhiều dòng hợp lệ','Cộng tồn trong transaction'),('TC06','Chat AI','Chỉ định chuyển nhân viên','Tạo SupportConversation'),('TC07','Chat nhân viên','Nhân viên nhận cuộc chờ','AssignedEmployee và Active'),('TC08','W-video','Lọc tên sản phẩm','Hiển thị đúng video'),('TC09','Phân quyền','Customer gọi Admin action','403/redirect truy cập bị từ chối'),('TC10','Responsive','320–1440 px','Không che nội dung hoặc tràn ngang')])

add_heading('9. TRIỂN KHAI',1)
add_heading('9.1. Môi trường chạy',2)
add_bullets(['SQLite: môi trường phát triển local.','PostgreSQL: môi trường Render.','Docker: build/publish .NET và chạy trên container.','HTTPS, HTTPS redirect và secure cookies ở production.','S3-compatible storage cho video/avatar khi cần lưu bền.'])
add_heading('9.2. Hướng dẫn chạy',2)
add_numbers(['Mở thư mục dự án bằng Visual Studio hoặc terminal.','Đặt `ConnectionStrings__DefaultConnection` hoặc cấu hình SQLite mặc định.','Chạy `dotnet restore` và `dotnet run`.','Mở URL được ứng dụng hiển thị.','Thiết lập tài khoản Admin bằng biến bootstrap hoặc tài khoản seed theo môi trường.'])

add_heading('10. ĐÁNH GIÁ VÀ HƯỚNG PHÁT TRIỂN',1)
add_heading('10.1. Đánh giá',2)
doc.add_paragraph('Dự án đã hoàn thiện bộ luồng thương mại cốt lõi và bàn làm việc theo vai trò. Giao diện đã áp dụng responsive, quản trị có danh sách cuộn riêng và form cửa sổ nổi. Hệ thống kết hợp AI, chat nhân viên, tạp chí, W-video và quản lý tài chính tạo nên sản phẩm gần với kịch bản cửa hàng thực tế.')
add_heading('10.2. Hạn chế',2)
add_bullets(['Thanh toán trực tuyến chưa tích hợp cổng thanh toán.','Chưa có kiểm thử đơn vị/tích hợp tự động persistent trong repository.','Biên lai, vận chuyển và email mới ở mức cấu hình hoặc ghi log.','Presence nhân viên đang process-local nên cần kiến trúc tập trung khi nhiều instance.','Local storage có thể mất trên host free nếu không persistent/S3.'])
add_heading('10.3. Hướng phát triển',2)
add_bullets(['Thêm EF Core migrations và test project.','Tích hợp thanh toán online, vận chuyển và tracking.','Thêm SignalR để chat realtime thay polling.','Thêm Redis/PresenceStore cho nhiều instance.','Xây dựng dashboard báo cáo doanh thu/lợi nhuận theo biểu đồ.','Thêm đăng nhập email xác thực và reset password qua SMTP production.'])

add_heading('11. KẾT LUẬN',1)
doc.add_paragraph('Nhóm đã xây dựng thành công ứng dụng W Studio với đầy đủ chức năng cửa hàng, tài khoản theo vai trò, quy trình đặt hàng, Stylist AI, chat nhân viên, quản lý kho/tài chính và nội dung video/tạp chí. Sản phẩm đáp ứng mục tiêu đồ án và thể hiện khả năng kết hợp ASP.NET Core, EF Core, Identity, AI, media và responsive UI. Một số tích hợp thực tế cần thêm dịch vụ bên ngoài và kiểm thử tự động trước khi đưa vào vận hành quy mô lớn.')

add_heading('PHỤ LỤC',1)
add_heading('A. API và luồng chính',2)
add_table(['Nhóm','Endpoint tiêu biểu'],[
('Tài khoản','/Account/Register, /Account/Login, /Account/ForgotPassword'),('Sản phẩm','/Home/Index, /Home/Magazine, /FashionVideos/Index'),('Đơn hàng','POST /Orders/Create, /Orders/ConfirmReceived/{id}, /Orders/RequestReturn/{id}, /Orders/Cancel/{id}'),('AI','POST /api/chat'),('Support','GET/POST /api/support-chat/...'),('Admin','POST /Admin/RecordStockReceipt, /Admin/SaveProduct, /Admin/CreateEmployee, /Admin/UploadFashionVideo'),('Nhân viên','/Staff/Index, POST /Staff/UpdateOrderStatus, /Staff/UpdateStock')])
add_heading('B. Vai trò và phân quyền',2)
add_table(['Vai trò','Quyền'],[
('Customer','Mua hàng, đánh giá, yêu cầu trả hàng, chat, hồ sơ.'),('Employee','Inbox, chat, xử lý đơn, cập nhật tồn kho, phản hồi đánh giá.'),('Admin','Toàn bộ quản trị; tạo nhân viên; quản lý danh mục, đối tác, tài chính, video, vai trò.')])
add_heading('C. Danh sách source chính',2)
add_table(['Thư mục','Nội dung'],[
('Controllers','Điều hướng MVC và API nghiệp vụ.'),('Models','Thực thể, view model, vai trò.'),('Services','AI, storage, promotion, email, magazine, presence.'),('Views','Giao diện cửa hàng, dashboard admin/staff, tài khoản.'),('wwwroot/css, wwwroot/js','Responsive CSS và tương tác JavaScript.'),('Data','DbContext, seed dữ liệu.')])

add_heading('DANH MỤC TÀI LIỆU THAM KHẢO',1)
add_numbers(['Microsoft Learn. ASP.NET Core documentation.','Microsoft Learn. ASP.NET Core Identity documentation.','Microsoft Learn. Entity Framework Core documentation.','Microsoft Learn. Razor Pages and MVC Views.','MDN Web Docs. HTML, CSS and JavaScript.','OWASP. Authentication and Authorization Cheat Sheets.','S3-compatible storage providers documentation.'])

doc.save(WORD_OUT)

# ---------- PowerPoint ----------
prs=Presentation();prs.slide_width=PInches(13.333);prs.slide_height=PInches(7.5)
BG=PRGB(30,26,24);CREAM=PRGB(248,242,233);WINE=PRGB(119,60,61);GOLD=PRGB(215,180,133);DARK=PRGB(47,42,39);WHITE=PRGB(255,255,255)

def text_box(slide,x,y,w,h,text,size=20,color=DARK,bold=False,align=PP_ALIGN.LEFT,font='Aptos'):
    sh=slide.shapes.add_textbox(PInches(x),PInches(y),PInches(w),PInches(h));tf=sh.text_frame;tf.clear();tf.word_wrap=True
    p=tf.paragraphs[0];p.text=text;p.alignment=align
    for r in p.runs:r.font.name=font;r.font.size=PPt(size);r.font.bold=bold;r.font.color.rgb=color
    return sh
def rect(slide,x,y,w,h,fill=CREAM,line=CREAM,rounded=True):
    shape=slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE if rounded else MSO_SHAPE.RECTANGLE,PInches(x),PInches(y),PInches(w),PInches(h));shape.fill.solid();shape.fill.fore_color.rgb=fill;shape.line.color.rgb=line;return shape
def title(slide,kicker,title_text,number):
    text_box(slide,.55,.28,10.9,.28,kicker.upper(),10,GOLD,True)
    text_box(slide,.55,.62,11.7,.65,title_text,28,CREAM,True)
    text_box(slide,12.25,.45,.5,.35,f'{number:02}',10,GOLD,True,PP_ALIGN.RIGHT)
def add_bullets_slide(slide,items,cols=1):
    if cols==1:
        y=.8
        for item in items:
            rect(slide,.65,y,11.9,.68,fill=PRGB(255,251,247),line=PRGB(224,214,203));text_box(slide,.9,y+.12,11.4,.42,item,17,DARK,False);y+=.82
    else:
        y=1.45
        split=(len(items)+1)//2
        for idx,item in enumerate(items):
            col=0 if idx<split else 1; row=idx if idx<split else idx-split
            yy=1.45+row*1.15
            xx=.65 if col==0 else 6.78
            rect(slide,xx,yy,5.9,.92,PRGB(255,251,247),PRGB(224,214,203));text_box(slide,xx+.22,yy+.16,5.45,.58,item,15,DARK)
def blank_slide(kicker,heading,number):
    slide=prs.slides.add_slide(prs.slide_layouts[6]);bg=slide.background.fill;bg.solid();bg.fore_color.rgb=BG;title(slide,kicker,heading,number);return slide

# 1 cover
slide=prs.slides.add_slide(prs.slide_layouts[6]);slide.background.fill.solid();slide.background.fill.fore_color.rgb=BG
text_box(slide,.7,.7,11.9,.35,'TRƯỜNG CAO ĐẲNG CÔNG NGHỆ CAO HÀ NỘI',13,GOLD,True,PP_ALIGN.CENTER)
text_box(slide,.7,2.05,11.9,.8,'W STUDIO',38,CREAM,True,PP_ALIGN.CENTER)
text_box(slide,.7,2.85,11.9,.55,'Website thương mại điện tử thời trang',24,CREAM,False,PP_ALIGN.CENTER)
rect(slide,2.25,4.05,8.8,1.25,PRGB(43,38,35),PRGB(119,60,61))
text_box(slide,2.55,4.28,8.2,.25,'Sinh viên thực hiện',11,GOLD,True,PP_ALIGN.CENTER)
text_box(slide,2.55,4.58,8.2,.38,'Bùi Đức Ân  •  Lê Văn Cường  •  Hà Văn Thiện',16,CREAM,True,PP_ALIGN.CENTER)
text_box(slide,.7,6.85,11.9,.25,'Đồ án tốt nghiệp / đồ án môn học  •  2026',11,GOLD,False,PP_ALIGN.CENTER)

# 2 overview
slide=blank_slide('01 • Tổng quan','Giới thiệu đề tài',2)
add_bullets_slide(slide,['Kết nối cửa hàng, tư vấn, chat nhân viên và quản trị trong một nền tảng.','Hỗ trợ thanh toán COD và quy trình xử lý đơn hàng thực tế.','Thêm Stylist AI, W-video, tạp chí thời trang và phối màu.','Ba vai trò: Khách hàng – Nhân viên – Quản trị viên.','Responsive trên desktop, tablet và mobile.'])

# 3 technology
slide=blank_slide('02 • Công nghệ','Kiến trúc công nghệ',3)
items=[('ASP.NET Core MVC','Controller, Razor View'),('Identity','Tài khoản, cookie, vai trò'),('EF Core','SQLite / PostgreSQL'),('HTML/CSS/JS','Giao diện responsive'),('AI Provider','Gemini, Groq, OpenAI-compatible'),('Object Storage','S3-compatible / local storage')]
for i,(a,b) in enumerate(items):
    col=i%3;row=i//3;x=.65+col*4.25;y=1.55+row*2.25
    rect(slide,x,y,3.75,1.55,PRGB(255,251,247),PRGB(224,214,203));text_box(slide,x+.25,y+.28,3.25,.34,a,17,WINE,True);text_box(slide,x+.25,y+.75,3.25,.44,b,14,DARK)

# 4 usecase
slide=blank_slide('03 • Phân tích','Sơ đồ Use Case chi tiết',4)
slide.shapes.add_picture(UC_IMG,PInches(.55),PInches(1.25),width=PInches(12.23),height=PInches(5.85))

# 5 roles
slide=blank_slide('04 • Vai trò','Phân quyền người dùng',5)
roles=[('KHÁCH HÀNG',['Tìm kiếm và mua hàng','Chat AI / nhân viên','Theo dõi, đánh giá, đổi/trả']),('NHÂN VIÊN',['Nhận cuộc chat','Cập nhật đơn hàng','Cập nhật tồn kho']),('QUẢN TRỊ VIÊN',['Quản lý danh mục','Đối tác, nhập kho, chi phí','Tài khoản, voucher, video'])]
for i,(name,funcs) in enumerate(roles):
    x=.65+i*4.25
    rect(slide,x,1.48,3.75,4.65,PRGB(255,251,247),PRGB(224,214,203));text_box(slide,x+.25,1.78,3.25,.35,name,16,WINE,True,PP_ALIGN.CENTER)
    y=2.55
    for f in funcs:text_box(slide,x+.45,y,2.85,.55,'• '+f,15,DARK);y+=.78

# 6 order usecase
slide=blank_slide('05 • Use Case','Đặt hàng COD — luồng chi tiết',6)
steps=['Khách đăng nhập','Chọn size và thêm giỏ','Xem giỏ / áp voucher','Kiểm tra giá và tồn kho','Tạo đơn và trừ tồn','Gửi xác nhận / email']
for i,s in enumerate(steps):
    x=.55+i*2.08
    rect(slide,x,1.75,1.75,1.05,PRGB(255,251,247),PRGB(224,214,203));text_box(slide,x+.12,1.98,1.51,.38,f'{i+1}. {s}',14,DARK,True,PP_ALIGN.CENTER)
    if i<5:text_box(slide,x+1.76,2.08,.32,.25,'→',18,GOLD,True,PP_ALIGN.CENTER)
text_box(slide,.7,3.35,11.9,.35,'LUỒNG NGOẠI LE',11,GOLD,True)
add_bullets_slide(slide,['Giỏ trống hoặc chưa đăng nhập.','Size không hợp lệ hoặc tồn kho không đủ.','Voucher hết hạn, chưa hiệu lực hoặc không đủ điều kiện.','Giá trên máy chủ khác giá hiển thị → chấp nhận lại giá mới hoặc hủy.'],1)

# 7 activity order
slide=blank_slide('06 • Hoạt động','Sơ đồ hoạt động: đặt hàng',7)
slide.shapes.add_picture(ACT_IMG,PInches(2.15),PInches(1.25),width=PInches(9),height=PInches(5.9))

# 8 chat usecase
slide=blank_slide('07 • Use Case','Stylist AI & chuyển chat nhân viên',8)
add_bullets_slide(slide,['Khách gửi nhu cầu tới Stylist AI.','AI lọc danh mục còn bán và gợi ý tối đa 3 sản phẩm.','Khách bấm nút chuyển chat cho nhân viên.','Hệ thống tạo cuộc hội thoại, gán nhân viên online hoặc trạng thái chờ.','Nhân viên nhận cuộc chat, trả lời và đóng khi hoàn tất.','Polling cập nhật tin nhắn theo khoảng 3 giây.'],2)

# 9 support activity
slide=blank_slide('08 • Hoạt động','Sơ đồ hoạt động: chuyển chat',9)
slide.shapes.add_picture(SUP_IMG,PInches(.65),PInches(1.2),width=PInches(12),height=PInches(5.9))

# 10 admin
slide=blank_slide('09 • Quản trị','Bảng điều khiển quản trị viên',10)
add_bullets_slide(slide,['Sản phẩm: thêm, sửa, ẩn và quản lý tồn kho.','Đối tác: tạo/sửa/ngừng sử dụng không làm mất lịch sử.','Nhập kho: phiếu PN, nhiều dòng, dán dữ liệu bảng tính.','Tài chính: ghi chi phí vận hành và theo dõi lịch sử.','Voucher: tạo mã phần trăm hoặc số tiền cố định.','W-video: upload MP4/WebM, ẩn/hiện và xóa.','Tài khoản: tạo nhân viên và phân vai trò.'],2)

# 11 database
slide=blank_slide('10 • Dữ liệu','Nhóm thực thể cơ sở dữ liệu',11)
entities=[('NGƯỜI DÙNG','ApplicationUser'),('SẢN PHẨM','Product'),('ĐƠN HÀNG','CustomerOrder / OrderItem'),('CHAT','SupportConversation / Message'),('KHO','Partner / StockReceipt'),('TÀI CHÍNH','OperatingExpense / Promotion'),('NỘI DUNG','FashionVideo / FashionMagazine')]
for i,(a,b) in enumerate(entities):
    col=i%2;row=i//2;x=.65+col*6.1;y=1.35+row*1.25
    rect(slide,x,y,5.7,.9,PRGB(255,251,247),PRGB(224,214,203));text_box(slide,x+.22,y+.14,2.0,.25,a,12,WINE,True);text_box(slide,x+2.15,y+.14,3.25,.45,b,14,DARK)

# 12 interface
slide=blank_slide('11 • Giao diện','Responsive & bố cục',12)
items=['Trang chủ nhiều khu vực, dễ mở rộng','W-video: tìm kiếm, video responsive','Admin: danh sách cuộn và form cửa sổ nổi','Mobile: giao diện 1 cột, không che nội dung','Cửa sổ form không đẩy nội dung khác','Cỡ chữ, padding và vuốt phù hợp thiết bị']
add_bullets_slide(slide,items,2)

# 13 testing
slide=blank_slide('12 • Kiểm thử','Một số ca kiểm thử tiêu biểu',13)
add_table=add_table if False else None
rows=[('TC01','Đăng ký hợp lệ','Tạo Customer'),('TC02','Đặt hàng đủ tồn','Tạo đơn / trừ tồn'),('TC03','Size/tồn không hợp lệ','Từ chối'),('TC05','Nhập kho nhiều dòng','Cập nhật trong transaction'),('TC06','Chuyển chat nhân viên','Tạo SupportConversation'),('TC10','Màn hình 320–1440 px','Không tràn / che')]
y=1.35
rect(slide,.65,y,12.0,.55,WINE,WINE);text_box(slide,.85,y+.12,1.2,.25,'Mã',12,WHITE,True);text_box(slide,2.25,y+.12,4.4,.25,'Chức năng',12,WHITE,True);text_box(slide,7.2,y+.12,4.9,.25,'Kết quả',12,WHITE,True)
y+=.68
for i,row in enumerate(rows):
    fill=PRGB(255,251,247) if i%2==0 else PRGB(244,238,231)
    rect(slide,.65,y,12.0,.58,fill,fill,False);text_box(slide,.85,y+.14,1.2,.22,row[0],12,DARK,True);text_box(slide,2.25,y+.14,4.4,.28,row[1],12,DARK);text_box(slide,7.2,y+.14,4.9,.28,row[2],12,DARK);y+=.66

# 14 deployment
slide=blank_slide('13 • Triển khai','Hạ tầng triển khai',14)
add_bullets_slide(slide,['SQLite cho phát triển local.','PostgreSQL cho môi trường production.','Docker build và Render deploy.','HTTPS, secure cookie, HSTS và antiforgery.','Secret chỉ lưu trong User Secrets hoặc Environment.','S3-compatible cho video/avatar bền vững.','Persistent disk/database backup và restore.'])

# 15 future
slide=blank_slide('14 • Mở rộng','Hạn chế và hướng phát triển',15)
add_bullets_slide(slide,['Thêm thanh toán trực tuyến và vận chuyển.','Bổ sung EF migrations và test project.','SignalR thay polling cho chat realtime.','Redis hoặc store tập trung cho employee presence.','Dashboard doanh thu/lợi nhuận theo thời gian.','Bổ sung xác thực email và reset password qua SMTP.','Giám sát lỗi, logging và backup tự động.'])

# 16 conclusion
slide=blank_slide('15 • Kết luận','Tổng kết',16)
rect(slide,1.15,1.65,11.05,3.35,PRGB(43,38,35),PRGB(119,60,61))
text_box(slide,1.55,2.02,10.25,.38,'W Studio đã hoàn thành',23,GOLD,True,PP_ALIGN.CENTER)
text_box(slide,1.55,2.66,10.25,1.4,'Cửa hàng • Đặt hàng • Stylist AI • Chat nhân viên\nQuản lý kho • Đối tác • Chi phí • W-video • Tạp chí',19,CREAM,True,PP_ALIGN.CENTER)
text_box(slide,.7,6.45,11.9,.35,'CẢM ƠN THẦY/CÔ VÀ CÁC BẠN ĐÃ LẮNG NGHE',13,GOLD,True,PP_ALIGN.CENTER)

prs.save(PPT_OUT)
print(WORD_OUT)
print(PPT_OUT)
