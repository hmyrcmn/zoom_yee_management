using Toplanti.Core.Utilities.Security.Encrytion;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toplanti.Core.Entities.Concrete;

namespace Toplanti.Core.Utilities.Security.JWT
{
    public class JwtHelper : ITokenHelper
    {
        public IConfiguration Configuration { get; }
        private TokenOptions _zoomTokenOptions;
        private TokenOptions _tokenOptions;
        private DateTime _accessTokenExpiration;
        public JwtHelper(IConfiguration configuration)
        {
            Configuration = configuration;
            _zoomTokenOptions = Configuration.GetSection("TokenZoom").Get<TokenOptions>();
            _tokenOptions = Configuration.GetSection("TokenOptions").Get<TokenOptions>();
        }

        public AccessToken CreateZoomToken()
        {
            _accessTokenExpiration = DateTime.Now.AddMinutes(_zoomTokenOptions.AccessTokenExpiration);
            var securityKey = SecurityKeyHelper.CreateSecurityKey(_zoomTokenOptions.SecurityKey);
            var signingCredentials = SigningCredentialsHelper.CreateSigningCredentials(securityKey);

            var jwt = new JwtSecurityToken(
                issuer: _zoomTokenOptions.Issuer,
                expires: _accessTokenExpiration,
                signingCredentials: signingCredentials
            ); ;


            jwt.Header.Clear();
            jwt.Header.Add("alg", "HS256");
            jwt.Header.Add("typ", "JWT");
            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
            var token = jwtSecurityTokenHandler.WriteToken(jwt);

            return new AccessToken
            {
                Token = token,
                Expiration = _accessTokenExpiration
            };
        }

        public async Task<string> CreateAccessToken()
        {
            var accountId = Configuration["ZoomOAuth:AccountId"];
            var tokenRequestBody = $"grant_type=account_credentials&account_id={accountId}";
            var tokenUrl = "https://zoom.us/oauth/token";

            var clientId = Configuration["ZoomOAuth:ClientId"];
            var clientSecret = Configuration["ZoomOAuth:ClientSecret"];
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException("ZoomOAuth configuration is missing AccountId/ClientId/ClientSecret.");
            }

            var requestContent = new StringContent(tokenRequestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

            var response = await client.PostAsync(tokenUrl, requestContent);
            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Zoom OAuth token request failed ({(int)response.StatusCode}): {result}");
            }

            var accessToken = JObject.Parse(result)["access_token"]?.ToString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException($"Zoom OAuth token response does not contain access_token. Response: {result}");
            }

            return accessToken;
        }

        public AccessToken CreateToken(User user, List<OperationClaim> operationClaims, string? department = null)
        {
            _accessTokenExpiration = DateTime.Now.AddMinutes(_tokenOptions.AccessTokenExpiration);
            var securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
            var signingCredentials = SigningCredentialsHelper.CreateSigningCredentials(securityKey);

            var claims = new List<System.Security.Claims.Claim>();
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()));
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email));
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
            if (!string.IsNullOrWhiteSpace(department))
            {
                claims.Add(new System.Security.Claims.Claim("department", department));
            }
            
            foreach (var role in operationClaims)
            {
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role.Name));
            }

            var jwt = new JwtSecurityToken(
                issuer: _tokenOptions.Issuer,
                audience: _tokenOptions.Audience,
                expires: _accessTokenExpiration,
                claims: claims,
                signingCredentials: signingCredentials
            );

            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
            var token = jwtSecurityTokenHandler.WriteToken(jwt);

            return new AccessToken
            {
                Token = token,
                Expiration = _accessTokenExpiration,
                Department = department ?? string.Empty
            };
        }
    }
}
