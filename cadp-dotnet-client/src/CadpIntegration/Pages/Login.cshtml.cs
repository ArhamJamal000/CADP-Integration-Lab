using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CadpIntegration.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string Username { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var expectedUser = Environment.GetEnvironmentVariable("WEB_USERNAME") ?? "admin";
            var expectedPass = Environment.GetEnvironmentVariable("WEB_PASSWORD") ?? "Credo@123#";

            if (Username == expectedUser && Password == expectedPass)
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.Name, Username) };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                
                return RedirectToPage("/Index");
            }
            
            ModelState.AddModelError("Auth", "Invalid username or password");
            return Page();
        }
    }
}
