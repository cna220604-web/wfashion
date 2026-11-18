using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    OrderNotificationService notifications,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await RedirectSignedInUserAsync(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await RedirectSignedInUserAsync(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            DisplayName = model.DisplayName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim()
        };
        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!roleResult.Succeeded)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            if (!deleteResult.Succeeded)
            {
                AddIdentityErrors(deleteResult);
            }
            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return await RedirectToLocalAsync(returnUrl, user);
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await RedirectSignedInUserAsync(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        await PopulateExternalLoginViewDataAsync();
        ViewData["ExternalLoginError"] = TempData["ExternalLoginError"] as string;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await RedirectSignedInUserAsync(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        await PopulateExternalLoginViewDataAsync();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await userManager.FindByEmailAsync(model.Email.Trim());
            return await RedirectToLocalAsync(returnUrl, user);
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút."
            : "Email hoặc mật khẩu không chính xác.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
    {
        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        if (!schemes.Any(scheme => string.Equals(scheme.Name, provider, StringComparison.Ordinal)))
        {
            return BadRequest("Nhà cung cấp đăng nhập không được bật.");
        }

        var callbackUrl = Url.Action(nameof(ExternalLoginCallback), values: new { returnUrl });
        if (callbackUrl is null)
        {
            throw new InvalidOperationException("Could not generate the external login callback URL.");
        }

        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            logger.LogWarning("External sign-in provider returned an error: {ProviderError}", remoteError);
            TempData["ExternalLoginError"] = "Không thể hoàn tất đăng nhập qua nhà cung cấp. Vui lòng thử lại.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            TempData["ExternalLoginError"] = "Không nhận được thông tin đăng nhập từ nhà cung cấp. Vui lòng thử lại.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            var signedInUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            return await RedirectToLocalAsync(returnUrl, signedInUser);
        }

        if (signInResult.IsLockedOut)
        {
            TempData["ExternalLoginError"] = "Tài khoản tạm khóa. Vui lòng thử lại sau 15 phút.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)
            ?? info.Principal.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ExternalLoginError"] = "Nhà cung cấp không chia sẻ email. Hãy cấp quyền email hoặc đăng nhập bằng email và mật khẩu.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var emailIsVerified = string.Equals(
            info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            if (!emailIsVerified || !existingUser.EmailConfirmed)
            {
                TempData["ExternalLoginError"] = "Email này đã có tài khoản. Hãy đăng nhập bằng mật khẩu trước; chỉ email được nhà cung cấp xác minh mới có thể liên kết an toàn.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var linkResult = await userManager.AddLoginAsync(existingUser, info);
            if (!linkResult.Succeeded)
            {
                logger.LogWarning("Could not link external login {Provider} to the matching account: {Errors}",
                    info.LoginProvider, string.Join("; ", linkResult.Errors.Select(error => error.Code)));
                TempData["ExternalLoginError"] = "Không thể liên kết tài khoản đăng nhập này. Hãy đăng nhập bằng email và mật khẩu.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            await signInManager.SignInAsync(existingUser, isPersistent: false);
            return await RedirectToLocalAsync(returnUrl, existingUser);
        }

        var displayName = info.Principal.FindFirstValue(ClaimTypes.Name)
            ?? info.Principal.FindFirstValue("name")
            ?? email;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailIsVerified,
            DisplayName = displayName.Length <= 100 ? displayName : displayName[..100]
        };
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            logger.LogWarning("Could not create an account from external provider {Provider}: {Errors}",
                info.LoginProvider, string.Join("; ", createResult.Errors.Select(error => error.Code)));
            TempData["ExternalLoginError"] = "Không thể tạo tài khoản. Email có thể đã được sử dụng; hãy thử đăng nhập bằng email và mật khẩu.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var externalLoginResult = await userManager.AddLoginAsync(user, info);
        var roleResult = externalLoginResult.Succeeded
            ? await userManager.AddToRoleAsync(user, AppRoles.Customer)
            : IdentityResult.Failed(externalLoginResult.Errors.ToArray());
        if (!externalLoginResult.Succeeded || !roleResult.Succeeded)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                logger.LogError("Could not remove incomplete external account {UserId}: {Errors}",
                    user.Id, string.Join("; ", deleteResult.Errors.Select(error => error.Code)));
            }

            logger.LogWarning("External account setup failed for provider {Provider}: {Errors}",
                info.LoginProvider, string.Join("; ", roleResult.Errors.Select(error => error.Code)));
            TempData["ExternalLoginError"] = "Không thể hoàn tất tạo tài khoản. Vui lòng thử lại hoặc đăng ký bằng email.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return await RedirectToLocalAsync(returnUrl, user);
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { email = user.Email, token },
                Request.Scheme);
            if (resetUrl is null)
            {
                logger.LogError("Could not generate a password-reset URL for an account recovery request");
            }
            else
            {
                await notifications.SendPasswordResetAsync(user.Email, resetUrl, HttpContext.RequestAborted);
            }
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        var model = new ResetPasswordViewModel { Email = email ?? string.Empty, Token = token ?? string.Empty };
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Token))
        {
            ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu liên kết mới.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu liên kết mới.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        AddIdentityErrors(result);
        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task PopulateExternalLoginViewDataAsync()
    {
        ViewData["ExternalLogins"] = await signInManager.GetExternalAuthenticationSchemesAsync();
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private async Task<IActionResult> RedirectSignedInUserAsync(string? returnUrl)
    {
        var user = await userManager.GetUserAsync(User);
        return await RedirectToLocalAsync(returnUrl, user);
    }

    private async Task<IActionResult> RedirectToLocalAsync(string? returnUrl, ApplicationUser? user)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (user is not null)
        {
            if (await userManager.IsInRoleAsync(user, AppRoles.Admin))
            {
                return RedirectToAction("Index", "Admin");
            }
            if (await userManager.IsInRoleAsync(user, AppRoles.Employee))
            {
                return RedirectToAction("Index", "Staff");
            }
        }

        return RedirectToAction("Index", "Home");
    }
}
