using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using System;
using Toplanti.Business.Abstract;
using Toplanti.Core.Entities.Concrete;
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.Concrete
{
    public class LdapManager : ILdapService
    {
        private readonly LdapSettings _ldapSettings;

        public LdapManager(IConfiguration configuration)
        {
            _ldapSettings = configuration.GetSection("LdapSettings").Get<LdapSettings>() ?? new LdapSettings();
        }

        public LdapUser? GetUserDetails(string username)
        {
            var user = new LdapUser();
            using (var connection = new LdapConnection())
            {
                try
                {
                    connection.Connect(_ldapSettings.Host ?? "localhost", _ldapSettings.Port);
                    // Bind with service account or anonymous if allowed, or re-bind with user credentials if we had them here.
                    // For now, assuming we can search anonymously or the previous bind persists in a real scenario (but here we open new conn).
                    // In production, you typically bind with a service account to search.
                    // For this implementation, we will try to bind with the user's domain info if possible or just catch the exception.
                    
                    // Simplification: logic to extract user details would go here.
                    // For now returning a placeholder based on input as requested to keep it simple.
                    
                    user.Username = username;
                    user.Email = $"{username}@{_ldapSettings.Domain ?? "example.com"}";
                    user.Name = username;
                    user.Surname = "";
                }
                catch (LdapException)
                {
                    // Handle exception or log
                    return null!;
                }
            }
            return user;
        }

        public bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            using (var connection = new LdapConnection())
            {
                try
                {
                    connection.Connect(_ldapSettings.Host ?? "localhost", _ldapSettings.Port);
                    // Create the DN (Distinguished Name) using the domain and username
                     // Modify this format based on your actual AD structure. 
                     // Common formats: "domain\\username" or "cn=username,dc=example,dc=com" or "username@domain"
                    string userDn = $"{username}@{_ldapSettings.Domain ?? "example.com"}";
                    
                    connection.Bind(userDn, password);
                    return connection.Bound;
                }
                catch (LdapException)
                {
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
