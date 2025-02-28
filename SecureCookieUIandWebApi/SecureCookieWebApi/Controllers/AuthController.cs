using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace SecureCookieWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginRequest loginRequest)
        {
            // Simulate a successful login
            if (loginRequest.Username == "test" && loginRequest.Password == "password")
            {
                var claims = new[] { new System.Security.Claims.Claim("Username", loginRequest.Username) };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "CookieAuth");
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);

                HttpContext.SignInAsync("CookieAuth", principal).Wait(); // Set the cookie
                return Ok("Login successful");
            }

            return Unauthorized("Invalid credentials");
        }

        [HttpPost("Logout")]
        [Authorize]
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync("CookieAuth").Wait(); // Remove the cookie
            return Ok("Logout successful");
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

}