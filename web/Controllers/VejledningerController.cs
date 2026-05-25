using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace web.Controllers;

[Authorize]
public class VejledningerController : Controller
{
    private readonly IWebHostEnvironment _env;

    public VejledningerController(IWebHostEnvironment env)
    {
        _env = env;
    }

    public IActionResult Index()
    {
        var vejledningerPath = Path.Combine(_env.ContentRootPath, "App_files", "Vejledninger");

        // Render SystemBeskrivelse.MD
        var mdPath = Path.Combine(vejledningerPath, "SystemBeskrivelse.MD");
        var mdHtml = string.Empty;
        if (System.IO.File.Exists(mdPath))
        {
            var mdText = System.IO.File.ReadAllText(mdPath);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            mdHtml = Markdown.ToHtml(mdText, pipeline);
        }

        // List PDF files
        var pdfFiles = Directory.Exists(vejledningerPath)
            ? Directory.GetFiles(vejledningerPath, "*.pdf")
                .Select(f => Path.GetFileName(f))
                .OrderBy(f => f)
                .ToList()
            : new List<string>();

        ViewBag.MarkdownHtml = mdHtml;
        ViewBag.PdfFiles = pdfFiles;
        return View();
    }

    [HttpGet]
    public IActionResult GetPdf(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains(".."))
            return BadRequest();

        var path = Path.Combine(_env.ContentRootPath, "App_files", "Vejledninger", fileName);

        if (!System.IO.File.Exists(path) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return File(stream, "application/pdf");
    }
}
