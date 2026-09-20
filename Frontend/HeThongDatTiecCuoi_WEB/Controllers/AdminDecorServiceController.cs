using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers
{
    public class AdminDecorServiceController : Controller
    {
        private readonly HttpClient _httpClient;

        public AdminDecorServiceController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("RiversideApi");
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}