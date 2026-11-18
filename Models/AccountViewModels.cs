using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Models;

public sealed class RegisterViewModel
{
    [Required, StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên cần từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Required, DataType(DataType.Password), StringLength(100)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email đăng ký")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100)]
    [Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class EmployeeCreateViewModel
{
    [Required, StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên cần từ 2 đến 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Required, DataType(DataType.Password), StringLength(100)]
    [Display(Name = "Mật khẩu tạm thời")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ProfileViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Họ và tên")]
    public string DisplayName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [StringLength(30)]
    [RegularExpression("^(Nam|Nữ|Khác|Không muốn chia sẻ)?$", ErrorMessage = "Vui lòng chọn giới tính hợp lệ.")]
    [Display(Name = "Giới tính")]
    public string? Gender { get; set; }

    [StringLength(240)]
    [Display(Name = "Địa chỉ nhận hàng mặc định")]
    public string? Address { get; set; }

    [Display(Name = "Công khai số điện thoại")]
    public bool PhoneNumberIsPublic { get; set; }

    public string? AvatarImageId { get; set; }

    public IFormFile? AvatarFile { get; set; }
}

public sealed class CheckoutRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(240, MinimumLength = 5)]
    public string Address { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Note { get; set; }

    [StringLength(30)]
    public string? PromotionCode { get; set; }

    [Required, MinLength(1), MaxLength(30)]
    public List<CheckoutLineRequest> Items { get; set; } = [];
}

public sealed class CheckoutLineRequest
{
    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Size { get; set; } = string.Empty;

    [Range(1, 10)]
    public int Quantity { get; set; }
}

public sealed class ProductReviewSubmissionViewModel
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Vui lòng chọn từ 1 đến 5 sao.")]
    public int Rating { get; set; }

    [StringLength(1000, ErrorMessage = "Nội dung đánh giá tối đa 1.000 ký tự.")]
    public string? Comment { get; set; }
}

public sealed class ProductReviewReplyViewModel
{
    [Range(1, int.MaxValue)]
    public int ReviewId { get; set; }

    [Required, StringLength(1000, MinimumLength = 1, ErrorMessage = "Phản hồi cần từ 1 đến 1.000 ký tự.")]
    public string Reply { get; set; } = string.Empty;
}

public sealed class FashionVideoUploadViewModel
{
    [Required, StringLength(80)]
    public string ProductSlug { get; set; } = string.Empty;

    [Required, StringLength(180)]
    public string Caption { get; set; } = string.Empty;

    [Required]
    public IFormFile? VideoFile { get; set; }
}

public sealed class ProductEditViewModel
{
    [Required, StringLength(20, MinimumLength = 2), RegularExpression("^[A-Za-z0-9-]{2,20}$", ErrorMessage = "Mã sản phẩm chỉ gồm chữ cái, số và dấu gạch ngang; tối đa 20 ký tự.")]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, RegularExpression("^(women|men|accessories)$")]
    public string Category { get; set; } = "women";

    [Range(1000, 100000000)]
    public decimal Price { get; set; }

    public IFormFile? ImageFile { get; set; }

    [StringLength(500)]
    public string? Description { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string SizesCsv { get; set; } = "S,M,L";

    [StringLength(50)]
    public string Color { get; set; } = string.Empty;

    [Range(0, 100000)]
    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;
}
