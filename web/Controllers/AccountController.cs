using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using web.Models;

namespace web.Controllers;

public class AccountController(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var canCreateFirstAdmin = !await userManager.Users.AnyAsync();
        if (canCreateFirstAdmin)
        {
            return RedirectToAction(nameof(CreateFirstAdmin));
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { CanCreateFirstAdmin = canCreateFirstAdmin });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        model.CanCreateFirstAdmin = !await userManager.Users.AnyAsync();
        if (model.CanCreateFirstAdmin)
        {
            return RedirectToAction(nameof(CreateFirstAdmin));
        }

        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await userManager.FindByNameAsync(model.Username);
            if (user != null)
            {
            user.LastLogin = DateTime.Now;
                await userManager.UpdateAsync(user);
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Forkert login.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> CreateFirstAdmin()
    {
        if (await userManager.Users.AnyAsync())
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new CreateFirstAdminViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFirstAdmin(CreateFirstAdminViewModel model)
    {
        if (await userManager.Users.AnyAsync())
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Administrator");
        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MakeMeAdmin()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!await userManager.IsInRoleAsync(user, "Administrator"))
            await userManager.AddToRoleAsync(user, "Administrator");

        // Log ud og ind igen så claims opdateres
        await signInManager.RefreshSignInAsync(user);
        return Content("Du er nu Administrator. <a href='/Admin'>Gå til Admin</a>", "text/html");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
