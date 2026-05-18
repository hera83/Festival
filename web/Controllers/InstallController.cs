using Microsoft.AspNetCore.Mvc;

namespace web.Controllers;

public class InstallController : Controller
{
    [HttpGet("/install/checkin")]
    public IActionResult CheckIn() => View();

    [HttpGet("/install/frivillig")]
    public IActionResult Frivillig() => View();
}
