using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web.Models;

namespace web.Controllers;

[Authorize]
public class ProfileController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IWebHostEnvironment env) : Controller
{
    private string AvatarDirectory =>
        Path.Combine(env.ContentRootPath, "App_files", "avatars");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Account");

        return View(new ProfileViewModel
        {
            DisplayName    = user.DisplayName,
            UserName       = user.UserName ?? string.Empty,
            Email          = user.Email ?? string.Empty,
            PhoneNumber    = user.PhoneNumber,
            AvatarFileName = user.AvatarFileName,
            ColorMode      = user.ColorMode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        // Fjern server-side krav om CurrentPassword — den eksisterer ikke mere
        ModelState.Remove(nameof(model.CroppedImageBase64));

        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction("Login", "Account");

        var hasErrors = false;

        // ── Navn ──────────────────────────────────────────────────
        user.DisplayName = model.DisplayName;

        // ── Farvetema ─────────────────────────────────────────────
        user.ColorMode = model.ColorMode is "light" or "dark" ? model.ColorMode : "system";

        // ── Brugernavn ────────────────────────────────────────────
        if (!string.Equals(user.UserName, model.UserName, StringComparison.OrdinalIgnoreCase))
        {
            var setUserName = await userManager.SetUserNameAsync(user, model.UserName);
            if (!setUserName.Succeeded)
            {
                foreach (var e in setUserName.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                hasErrors = true;
            }
        }

        // ── Telefon ───────────────────────────────────────────────
        user.PhoneNumber = model.PhoneNumber;

        // ── Email ─────────────────────────────────────────────────
        if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await userManager.SetEmailAsync(user, model.Email);
            if (!setEmail.Succeeded)
            {
                foreach (var e in setEmail.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                hasErrors = true;
            }
        }

        // ── Profilbillede (base64 fra cropper) ────────────────────
        if (!string.IsNullOrWhiteSpace(model.CroppedImageBase64))
        {
            var saveResult = await SaveAvatarAsync(user, model.CroppedImageBase64);
            if (saveResult is not null)
                ModelState.AddModelError(string.Empty, saveResult);
            else
                hasErrors = false; // ikke fejl herfra
        }

        // ── Gem bruger ────────────────────────────────────────────
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            hasErrors = true;
        }

        // ── Adgangskode (admin-reset, ingen nuværende kræves) ─────
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token  = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            model.AvatarFileName = user.AvatarFileName;
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Dine oplysninger er gemt.";
        return RedirectToAction(nameof(Index));
    }

    // ── Skift farvetema (AJAX) ────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetColorMode([FromForm] string mode)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        user.ColorMode = mode == "light" ? "light" : "dark";
        await userManager.UpdateAsync(user);
        return Ok();
    }

    // ── Serve avatar-billede ──────────────────────────────────────
    [HttpGet]
    [Route("Profile/Avatar/{fileName}")]
    public IActionResult Avatar(string fileName)
    {
        var path = Path.Combine(AvatarDirectory, Path.GetFileName(fileName));
        if (!System.IO.File.Exists(path)) return NotFound();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mime = ext == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime);
    }

    // ─────────────────────────────────────────────────────────────
    private async Task<string?> SaveAvatarAsync(AppUser user, string base64)
    {
        try
        {
            // base64 kan indeholde data-URL prefix: "data:image/png;base64,..."
            var commaIdx = base64.IndexOf(',');
            var raw      = commaIdx >= 0 ? base64[(commaIdx + 1)..] : base64;
            var ext      = base64.Contains("image/png") ? ".png" : ".jpg";

            var bytes = Convert.FromBase64String(raw);
            if (bytes.Length > 5 * 1024 * 1024)
                return "Billedet må maksimalt være 5 MB.";

            Directory.CreateDirectory(AvatarDirectory);

            // Slet gammelt billede
            if (!string.IsNullOrEmpty(user.AvatarFileName))
            {
                var old = Path.Combine(AvatarDirectory, user.AvatarFileName);
                if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            }

            var fileName = $"{user.Id}{ext}";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(AvatarDirectory, fileName), bytes);

            user.AvatarFileName    = fileName;
            user.AvatarContentType = ext == ".png" ? "image/png" : "image/jpeg";
            user.AvatarFileSize    = bytes.Length;
            user.AvatarUploadedAt  = DateTime.Now;

            return null; // ingen fejl
        }
        catch
        {
            return "Billedet kunne ikke gemmes. Prøv igen.";
        }
    }
}
