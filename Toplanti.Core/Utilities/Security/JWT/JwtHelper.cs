using Core.Utilities.Security.Encrytion;
using Microsoft.Extensions.Configuration;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace Core.Utilities.Security.JWT
{
    public class JwtHelper : ITokenHelper
    {
        public IConfiguration Configuration { get; }
        private TokenOptions _tokenOptions;
        private TokenOptions _zoomTokenOptions;
        private DateTime _accessTokenExpiration;
        public JwtHelper(IConfiguration configuration)
        {
            Configuration = configuration;
            _tokenOptions = Configuration.GetSection("TokenOptions").Get<TokenOptions>();
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
    }
}
