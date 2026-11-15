using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace WebApplication1.Models;

public sealed class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? AvatarImageId { get; set; }

    [StringLength(30)]
    public string? Gender { get; set; }

    [StringLength(240)]
    public string? Address { get; set; }

    public bool PhoneNumberIsPublic { get; set; }
}
