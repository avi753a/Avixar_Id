using Avixar.Domain;
using Avixar.Entity;
using Avixar.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace Avixar.UI
{
    /// <summary>
    /// OAuth2/OIDC Connect Controller
    /// Handles authorization code flow, token exchange, userinfo, and logout
    /// </summary>
    [Route("connect")]
    public class ConnectController : Controller
    {
        private readonly IConnectService _connectService;
        private readonly ILogger<ConnectController> _logger;

        public ConnectController(IConnectService connectService, ILogger<ConnectController> logger)
        {
            _connectService = connectService;
            _logger = logger;
        }

        /// <summary>
        /// OAuth2 Authorization Endpoint
        /// GET /connect/authorize?client_id=xxx&redirect_uri=xxx&response_type=code&scope=openid profile&state=xxx
        /// </summary>
        [HttpGet("authorize")]
        [Authorize] // User must be authenticated via cookie
        public async Task<IActionResult> Authorize([FromQuery] AuthorizeRequestDto request)
        {
            try
            {
                _logger.LogInformation("OAuth2 Authorize request from client: {ClientId}", request.ClientId);

                // Convert to ExternalLoginRequest format (existing service expects this)
                var externalRequest = new ExternalLoginRequest
                {
                    client_id = request.ClientId,
                    redirect_uri = request.RedirectUri,
                    response_type = request.ResponseType,
                    state = request.State,
                    nonce = ""
                };

                var result = await _connectService.AuthorizeAsync(externalRequest, User);

                if (!result.Status)
                {
                    if (result.Message == "NotAuthenticated")
                    {
                        return RedirectToAction("Login", "Auth", new { returnUrl = Request.Path + Request.QueryString });
                    }

                    return BadRequest(new { error = "invalid_request", error_description = result.Message });
                }

                // Redirect to client with authorization code
                return Redirect(result.Data!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OAuth2 authorize endpoint");
                return StatusCode(500, new { error = "server_error", error_description = "Internal server error" });
            }
        }

        /// <summary>
        /// OAuth2 Token Endpoint
        /// POST /connect/token
        /// Body: grant_type=authorization_code&code=xxx&client_id=xxx&client_secret=xxx&redirect_uri=xxx
        /// </summary>
        [HttpPost("token")]
        public async Task<IActionResult> Token([FromForm] TokenRequestDto request)
        {
            try
            {
                _logger.LogInformation("OAuth2 Token request - GrantType: {GrantType}, ClientId: {ClientId}", 
                    request.GrantType, request.ClientId);

                if (request.GrantType == "authorization_code")
                {
                    if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.RedirectUri))
                    {
                        return BadRequest(new { error = "invalid_request", error_description = "Missing code or redirect_uri" });
                    }

                    var result = await _connectService.ExchangeTokenAsync(
                        request.ClientId, 
                        request.ClientSecret, 
                        request.Code, 
                        request.RedirectUri
                    );

                    if (!result.Status)
                    {
                        return BadRequest(new { error = "invalid_grant", error_description = result.Message });
                    }

                    return Ok(result.Data);
                }
                else if (request.GrantType == "refresh_token")
                {
                    // TODO: Implement refresh token flow
                    return BadRequest(new { error = "unsupported_grant_type", error_description = "Refresh token not yet implemented" });
                }
                else
                {
                    return BadRequest(new { error = "unsupported_grant_type", error_description = $"Grant type '{request.GrantType}' not supported" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OAuth2 token endpoint");
                return StatusCode(500, new { error = "server_error", error_description = "Internal server error" });
            }
        }

        /// <summary>
        /// OIDC UserInfo Endpoint
        /// GET /connect/userinfo
        /// Header: Authorization: Bearer {access_token}
        /// </summary>
        [HttpGet("userinfo")]
        [Authorize(AuthenticationSchemes = "Bearer")] // Requires JWT token
        public async Task<IActionResult> UserInfo()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "invalid_token", error_description = "User identifier missing" });
                }

                _logger.LogInformation("UserInfo request for user: {UserId}", userId);

                var result = await _connectService.GetUserInfoAsync(userId);

                if (!result.Status)
                {
                    return NotFound(new { error = "user_not_found", error_description = result.Message });
                }

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UserInfo endpoint");
                return StatusCode(500, new { error = "server_error", error_description = "Internal server error" });
            }
        }

        /// <summary>
        /// OAuth2 Logout Endpoint
        /// GET /connect/logout?post_logout_redirect_uri=xxx&client_id=xxx
        /// </summary>
        [HttpGet("logout")]
        public async Task<IActionResult> Logout([FromQuery] string? post_logout_redirect_uri, [FromQuery] string? client_id)
        {
            try
            {
                _logger.LogInformation("OAuth2 Logout request - ClientId: {ClientId}", client_id);

                // Clear authentication cookie
                await HttpContext.SignOutAsync();

                // Validate post_logout_redirect_uri if provided
                if (!string.IsNullOrEmpty(post_logout_redirect_uri) && !string.IsNullOrEmpty(client_id))
                {
                    var isValid = await _connectService.ValidateLogoutUriAsync(client_id, post_logout_redirect_uri);
                    
                    if (isValid)
                    {
                        return Redirect(post_logout_redirect_uri);
                    }
                }

                // Default redirect to login page
                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OAuth2 logout endpoint");
                return RedirectToAction("Login", "Auth");
            }
        }
    }
}
