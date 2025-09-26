using Gateway.API.Dtos;
using Gateway.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;

    public AuthController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequestDto loginRequest)
    {
        var user = GetUserFromDatabase(loginRequest.Username, loginRequest.Password);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new LoginResponseDto { User = user, Token = token });
    }

    private static UserDto? GetUserFromDatabase(string username, string password)
    {
        return (username, password) switch
        {
            ("admin", "admin123") => new UserDto { Id = 1, Username = username, Role = "Admin" },
            ("vendedor", "vendedor123") => new UserDto { Id = 2, Username = username, Role = "Seller" },
            _ => null
        };
    }
}
