using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ApplePay.Controllers
{
    [Authorize(Roles = "Admin")]
    public class WebSocketViewController : Controller
    {
        [HttpGet("/websocket-dashboard")]
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}
