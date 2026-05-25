using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace web.Controllers;

[Authorize]
public class VejledningerController : Controller
{
    public IActionResult Index() => View();
}
