using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;
using QuanLyNhaTro.ViewModels;

namespace QuanLyNhaTro.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AccountController(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Room");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = model.Username.Trim();
        var user = await _context.Users.SingleOrDefaultAsync(item => item.Username == username);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View(model);
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
            await _context.SaveChangesAsync();
        }

        await SignInUserAsync(user);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Room");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var user = await GetSignedInUserAsync();
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        return View(new AccountSettingsViewModel
        {
            Username = user.Username,
            Profile = new UpdateAccountProfileViewModel
            {
                FullName = user.FullName
            }
        });
    }

    // Keep the previous URL working for saved bookmarks.
    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return RedirectToAction(nameof(Settings));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(
        [Bind(Prefix = "Profile")] UpdateAccountProfileViewModel model)
    {
        var user = await GetSignedInUserAsync();
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", new AccountSettingsViewModel
            {
                Username = user.Username,
                Profile = model
            });
        }

        user.FullName = model.FullName.Trim();
        await _context.SaveChangesAsync();

        // Refresh the authentication cookie so User.Identity.Name immediately reflects the new name.
        await SignInUserAsync(user);

        TempData["ProfileStatusMessage"] = "Đã cập nhật họ và tên tài khoản.";
        return RedirectToAction(nameof(Settings));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = "Password")] ChangePasswordViewModel model)
    {
        var user = await GetSignedInUserAsync();
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View("Settings", new AccountSettingsViewModel
            {
                Username = user.Username,
                Profile = new UpdateAccountProfileViewModel { FullName = user.FullName },
                Password = model
            });
        }

        var currentPasswordVerification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, model.CurrentPassword);

        if (currentPasswordVerification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(
                "Password.CurrentPassword",
                "Mật khẩu hiện tại không chính xác.");
            return View("Settings", new AccountSettingsViewModel
            {
                Username = user.Username,
                Profile = new UpdateAccountProfileViewModel { FullName = user.FullName },
                Password = model
            });
        }

        var newPasswordVerification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, model.NewPassword);

        if (newPasswordVerification != PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(
                "Password.NewPassword",
                "Mật khẩu mới phải khác mật khẩu hiện tại.");
            return View("Settings", new AccountSettingsViewModel
            {
                Username = user.Username,
                Profile = new UpdateAccountProfileViewModel { FullName = user.FullName },
                Password = model
            });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        await _context.SaveChangesAsync();

        TempData["PasswordStatusMessage"] = "Đổi mật khẩu thành công.";
        return RedirectToAction(nameof(Settings));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task<User?> GetSignedInUserAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return await _context.Users.FindAsync(userId);
    }

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("Username", user.Username)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });
    }
}
