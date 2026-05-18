using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace web.Controllers;

[AllowAnonymous]
public class FrivilligAppController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Overblik()
    {
        return PartialView("_Overblik");
    }

    [HttpGet]
    public IActionResult Vagter()
    {
        return PartialView("_Vagter");
    }

    [HttpGet]
    public IActionResult Observationer()
    {
        return PartialView("_Observationer");
    }

    [HttpGet]
    public IActionResult Profil()
    {
        return PartialView("_Profil");
    }

    [HttpGet]
    public IActionResult Telefonbog()
    {
        return PartialView("_Telefonbog");
    }
}
