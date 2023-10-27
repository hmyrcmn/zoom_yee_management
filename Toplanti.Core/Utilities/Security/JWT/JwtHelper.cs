using Core.Utilities.Security.Encrytion;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.JWT
{
    public class JwtHelper : ITokenHelper
    {
        public IConfiguration Configuration { get; }
        private TokenOptions _zoomTokenOptions;
        private DateTime _accessTokenExpiration;
        public JwtHelper(IConfiguration configuration)
        {
            Configuration = configuration;
            _zoomTokenOptions = Configuration.GetSection("TokenZoom").Get<TokenOptions>();
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
            var accountId = "JA-PkZ3iRl6j_WL1SbvuEQ";
            var tokenRequestBody = $"grant_type=account_credentials&account_id={accountId}";
            var tokenUrl = "https://zoom.us/oauth/token";

            var clientId = "n15GtcK0R7udggtQQBY_Zw";
            var clientSecret = "zl7d2TwmCTJ9YO5q0WQVkvt1gDXS7FYE";

            var requestContent = new StringContent(tokenRequestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

            var response = await client.PostAsync(tokenUrl, requestContent);
            var result = await response.Content.ReadAsStringAsync();
            var accessToken = JObject.Parse(result)["access_token"].ToString();

            return accessToken;
        }
    }
}
