using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using web.Models;

namespace web.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            var model = new ErrorViewModel
            {
                StatusCode = 500,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Title = "Der opstod en uventet fejl",
                Message = "Vi kunne ikke behandle din anmodning. Prøv igen om lidt."
            };

            if (exceptionFeature?.Error is not null)
            {
                // Vi logger ikke manuelt her, men kunne udvides senere med ILogger.
            }

            Response.StatusCode = 500;
            return View("Error", model);
        }

        [Route("Error/{statusCode:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCodeHandler(int statusCode)
        {
            var model = statusCode switch
            {
                404 => new ErrorViewModel
                {
                    StatusCode = 404,
                    Title = "Side ikke fundet",
                    Message = "Siden du leder efter findes ikke eller er blevet flyttet.",
                    RequestId = HttpContext.TraceIdentifier
                },
                403 => new ErrorViewModel
                {
                    StatusCode = 403,
                    Title = "Adgang nægtet",
                    Message = "Du har ikke rettigheder til at se denne side.",
                    RequestId = HttpContext.TraceIdentifier
                },
                _ => new ErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Der opstod en fejl",
                    Message = "Der opstod en fejl under behandlingen af din anmodning.",
                    RequestId = HttpContext.TraceIdentifier
                }
            };

            Response.StatusCode = statusCode;
            return View("Error", model);
        }
    }
}
