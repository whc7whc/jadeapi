using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Team.Backend.Models;
using Team.Backend.Models.EfModel;
using Microsoft.Extensions.Logging;

namespace Team.Backend.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
            : base(context, logger)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // 重定向到儀表板
            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
