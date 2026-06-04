using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailureResponse("Invalid request", 
                ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()));
        }

        var result = await _authService.AuthenticateAsync(request);
        if (result == null)
        {
            return Unauthorized(ApiResponse.FailureResponse("Invalid username or password"));
        }

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result, "Login successful"));
    }

    [HttpGet("validate")]
    [Authorize]
    public IActionResult ValidateToken()
    {
        var userId = User.FindFirst("userId")?.Value;
        var role = User.FindFirst("role")?.Value;

        return Ok(ApiResponse<object>.SuccessResponse(new { UserId = userId, Role = role }, "Token is valid"));
    }

    [HttpGet("users/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse.FailureResponse("User not found"));
        }

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            Role = user.Role.ToString(),
            user.ManagerId
        }));
    }

    [HttpGet("users/team/{managerId}")]
    [Authorize]
    public async Task<IActionResult> GetTeamMembers(Guid managerId)
    {
        var members = await _authService.GetTeamMembersAsync(managerId);
        var result = members.Select(u => new
        {
            u.Id,
            u.Username,
            u.FullName,
            u.Email,
            Role = u.Role.ToString()
        });

        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
