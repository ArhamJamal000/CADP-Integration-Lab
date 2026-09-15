using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        public IActionResult OnPost()
        {
            if (Username == "admin" && Password == "admin")
            {
                // Basic mock redirect
                return RedirectToPage("/Index");
            }
            
            ModelState.AddModelError("Auth", "Invalid username or password");
            return Page();
        }
    }
}
